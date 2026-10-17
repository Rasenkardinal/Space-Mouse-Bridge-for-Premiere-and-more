using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SpaceMouseBridge
{
    internal sealed class Axis
    {
        internal string Name;
        internal byte ReportId;
        internal ushort Link;
        internal ushort Usage;
        internal ushort Bits;
        internal int Minimum;
        internal int Maximum;
        internal long? Value;
        internal long ValueReport;
    }

    internal sealed class ButtonGroup
    {
        internal byte ReportId;
        internal ushort Link;
        internal bool Seen;
        internal long ValueReport;
        internal readonly HashSet<ushort> Available = new HashSet<ushort>();
        internal HashSet<ushort> Pressed = new HashSet<ushort>();
    }

    internal sealed class Device : IDisposable
    {
        internal IntPtr Handle;
        internal string Path;
        internal uint Vendor;
        internal uint Product;
        internal IntPtr Preparsed;
        internal ushort ReportLength;
        internal readonly List<Axis> Axes = new List<Axis>();
        internal readonly List<ButtonGroup> Buttons = new List<ButtonGroup>();
        internal readonly HashSet<string> ReportedErrors = new HashSet<string>();
        internal long Reports;
        internal DateTime? LastReport;
        internal string LastHex = "";
        internal byte LastReportId;
        internal string Description
        {
            get { return string.Format("VID_{0:X4} PID_{1:X4} | {2} | {3} Achsen", Vendor, Product,
                Axes.Count > 0 ? "Navigation" : "Zusatzcollection", Axes.Count); }
        }

        public void Dispose()
        {
            if (Preparsed != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Preparsed);
                Preparsed = IntPtr.Zero;
            }
        }
    }

    internal static class Decoder
    {
        internal static long SignedValue(uint value, ushort bits, int minimum)
        {
            if (bits == 0 || bits > 32)
                throw new InvalidDataException("Ungueltige HID-Bitbreite: " + bits);
            long mask = bits == 32 ? uint.MaxValue : (1L << bits) - 1;
            long result = value & mask;
            if (minimum < 0 && (result & (1L << (bits - 1))) != 0)
                result -= 1L << bits;
            return result;
        }

        internal static List<byte[]> SplitReports(byte[] payload, uint reportSize, uint count)
        {
            if (reportSize == 0 || count == 0 ||
                (ulong)reportSize * count != (ulong)payload.Length)
                throw new InvalidDataException("Ungueltige Raw-HID-Reportlaenge.");
            var reports = new List<byte[]>();
            for (uint i = 0; i < count; i++)
            {
                var report = new byte[checked((int)reportSize)];
                Buffer.BlockCopy(payload, checked((int)(i * reportSize)), report, 0, report.Length);
                reports.Add(report);
            }
            return reports;
        }
    }

    internal sealed class InputReader : IDisposable
    {
        internal readonly Dictionary<IntPtr, Device> Devices = new Dictionary<IntPtr, Device>();
        internal readonly List<string> Errors = new List<string>();
        internal event Action<string> Log;
        internal event Action<Device> ReportReceived;
        private bool registered;
        private readonly HashSet<IntPtr> failedDevices = new HashSet<IntPtr>();
        private static readonly string[] AxisNames = { "X", "Y", "Z", "Rx", "Ry", "Rz" };

        internal void Start(IntPtr window)
        {
            // INPUTSINK receives in the background; DEVNOTIFY reports receiver changes.
            var registration = new Native.Registration {
                UsagePage = 1, Usage = 8, Flags = 0x100 | 0x2000, Target = window
            };
            if (!Native.RegisterRawInputDevices(new[] { registration }, 1,
                (uint)Marshal.SizeOf(typeof(Native.Registration))))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw Input konnte nicht registriert werden.");
            registered = true;
            Refresh();
        }

        internal void Message(string text)
        {
            if (Log != null) Log(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
        }

        internal void Error(string text)
        {
            if (Errors.Count == 100) Errors.RemoveAt(0);
            Errors.Add(text);
            Message("FEHLER: " + text);
        }

        private static IntPtr Info(IntPtr handle, uint command, bool name)
        {
            uint size = 0;
            if (Native.GetRawInputDeviceInfo(handle, command, IntPtr.Zero, ref size) == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Geraeteinformationen konnten nicht gelesen werden.");
            if (size == 0 || size > 1024 * 1024)
                throw new InvalidDataException("Ungueltige Geraeteinformationslaenge.");
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)size * (name ? 2 : 1)));
            try
            {
                if (Native.GetRawInputDeviceInfo(handle, command, buffer, ref size) == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Geraeteinformationen konnten nicht gelesen werden.");
                return buffer;
            }
            catch
            {
                Marshal.FreeHGlobal(buffer);
                throw;
            }
        }

        private static Device Open(IntPtr handle, uint vendor, uint product)
        {
            var device = new Device { Handle = handle, Vendor = vendor, Product = product };
            try
            {
                IntPtr name = Info(handle, Native.DeviceName, true);
                try { device.Path = Marshal.PtrToStringUni(name); }
                finally { Marshal.FreeHGlobal(name); }
                device.Preparsed = Info(handle, Native.PreparsedData, false);
                var caps = new byte[64];
                Check(Native.HidP_GetCaps(device.Preparsed, caps), "HID-Faehigkeiten");
                device.ReportLength = BitConverter.ToUInt16(caps, 4);
                ushort values = BitConverter.ToUInt16(caps, 48);
                if (values > 0)
                {
                    var bytes = new byte[values * 72];
                    Check(Native.HidP_GetValueCaps(0, bytes, ref values, device.Preparsed), "HID-Achsenbeschreibung");
                    for (int i = 0; i < values; i++)
                    {
                        int p = i * 72;
                        if (BitConverter.ToUInt16(bytes, p) != 1) continue;
                        ushort first = BitConverter.ToUInt16(bytes, p + 56);
                        ushort last = bytes[p + 12] != 0 ? BitConverter.ToUInt16(bytes, p + 58) : first;
                        for (ushort usage = 0x30; usage <= 0x35; usage++)
                        {
                            if (usage < first || usage > last) continue;
                            device.Axes.Add(new Axis {
                                Name = AxisNames[usage - 0x30], Usage = usage,
                                ReportId = bytes[p + 2], Link = BitConverter.ToUInt16(bytes, p + 6),
                                Bits = BitConverter.ToUInt16(bytes, p + 18),
                                Minimum = BitConverter.ToInt32(bytes, p + 40),
                                Maximum = BitConverter.ToInt32(bytes, p + 44)
                            });
                        }
                    }
                }
                device.Axes.Sort((a, b) => a.Usage.CompareTo(b.Usage));
                ushort buttons = BitConverter.ToUInt16(caps, 46);
                if (buttons > 0)
                {
                    var bytes = new byte[buttons * 72];
                    Check(Native.HidP_GetButtonCaps(0, bytes, ref buttons, device.Preparsed), "HID-Tastenbeschreibung");
                    for (int i = 0; i < buttons; i++)
                    {
                        int p = i * 72;
                        if (BitConverter.ToUInt16(bytes, p) != 9) continue;
                        byte reportId = bytes[p + 2];
                        ushort link = BitConverter.ToUInt16(bytes, p + 6);
                        ButtonGroup group = device.Buttons.FirstOrDefault(b => b.ReportId == reportId && b.Link == link);
                        if (group == null)
                        {
                            group = new ButtonGroup { ReportId = reportId, Link = link };
                            device.Buttons.Add(group);
                        }
                        ushort first = BitConverter.ToUInt16(bytes, p + 56);
                        ushort last = bytes[p + 12] != 0 ? BitConverter.ToUInt16(bytes, p + 58) : first;
                        for (int usage = first; usage <= last; usage++)
                            group.Available.Add((ushort)usage);
                    }
                }
                return device;
            }
            catch
            {
                device.Dispose();
                throw;
            }
        }

        private static void Check(int status, string operation)
        {
            if (status != Native.HidSuccess)
                throw new InvalidDataException(operation + ": HID-Status 0x" + status.ToString("X8"));
        }

        internal void Refresh()
        {
            uint count = 0;
            uint itemSize = (uint)Marshal.SizeOf(typeof(Native.DeviceList));
            if (Native.GetRawInputDeviceList(null, ref count, itemSize) == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw-Input-Geraeteliste nicht lesbar.");
            var list = new Native.DeviceList[count];
            uint result = Native.GetRawInputDeviceList(list, ref count, itemSize);
            if (result == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw-Input-Geraeteliste wurde waehrend der Abfrage geaendert.");
            var present = new HashSet<IntPtr>();
            for (int i = 0; i < result; i++)
            {
                var entry = list[i];
                if (entry.Type != 2) continue;
                present.Add(entry.Handle);
                if (Devices.ContainsKey(entry.Handle)) continue;
                IntPtr info = Marshal.AllocHGlobal(32);
                try
                {
                    Marshal.WriteInt32(info, 32);
                    uint size = 32;
                    if (Native.GetRawInputDeviceInfo(entry.Handle, Native.DeviceInfo, info, ref size) == uint.MaxValue)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "HID-Geraeteidentifikation fehlgeschlagen.");
                    uint vendor = unchecked((uint)Marshal.ReadInt32(info, 8));
                    uint product = unchecked((uint)Marshal.ReadInt32(info, 12));
                    if ((vendor != 0x256f && vendor != 0x046d) ||
                        Marshal.ReadInt16(info, 20) != 1 || Marshal.ReadInt16(info, 22) != 8) continue;
                    Device device = Open(entry.Handle, vendor, product);
                    Devices.Add(entry.Handle, device);
                    failedDevices.Remove(entry.Handle);
                    Message("Verbunden: " + device.Description);
                }
                catch (Win32Exception ex) { if (failedDevices.Add(entry.Handle)) Error(ex.Message); }
                catch (InvalidDataException ex) { if (failedDevices.Add(entry.Handle)) Error(ex.Message); }
                finally { Marshal.FreeHGlobal(info); }
            }
            foreach (IntPtr handle in Devices.Keys.Where(h => !present.Contains(h)).ToArray())
            {
                Message("Getrennt: " + Devices[handle].Description);
                Devices[handle].Dispose();
                Devices.Remove(handle);
            }
            failedDevices.RemoveWhere(h => !present.Contains(h));
        }

        internal void Read(IntPtr rawInput)
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(Native.Header));
            if (Native.GetRawInputData(rawInput, 0x10000003, IntPtr.Zero, ref size, headerSize) == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw-Input-Laenge nicht lesbar.");
            if (size < headerSize + 8 || size > 1024 * 1024)
                throw new InvalidDataException("Ungueltige Raw-Input-Paketlaenge.");
            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint received = Native.GetRawInputData(rawInput, 0x10000003, buffer, ref size, headerSize);
                if (received == uint.MaxValue)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw Input nicht lesbar.");
                if (received != size) throw new InvalidDataException("Unvollstaendiges Raw-Input-Paket.");
                var header = (Native.Header)Marshal.PtrToStructure(buffer, typeof(Native.Header));
                if (header.Size != received) throw new InvalidDataException("Raw-Input-Headerlaenge stimmt nicht ueberein.");
                if (header.Type != 2) return;
                Device device;
                if (!Devices.TryGetValue(header.Device, out device)) return;
                int offset = (int)headerSize;
                uint reportSize = unchecked((uint)Marshal.ReadInt32(buffer, offset));
                uint reportCount = unchecked((uint)Marshal.ReadInt32(buffer, offset + 4));
                var payload = new byte[checked((int)received - offset - 8)];
                Marshal.Copy(IntPtr.Add(buffer, offset + 8), payload, 0, payload.Length);
                foreach (byte[] report in Decoder.SplitReports(payload, reportSize, reportCount))
                    Decode(device, report);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        internal void Decode(Device device, byte[] report)
        {
            device.Reports++;
            device.LastReport = DateTime.UtcNow;
            device.LastReportId = report[0];
            device.LastHex = BitConverter.ToString(report).Replace('-', ' ');
            if (report.Length != device.ReportLength)
            {
                Once(device, "Reportlaenge " + report.Length + " statt " + device.ReportLength);
                return;
            }
            foreach (Axis axis in device.Axes)
            {
                if (axis.ReportId != report[0]) continue;
                uint value;
                int status = Native.HidP_GetUsageValue(0, 1, axis.Link, axis.Usage,
                    out value, device.Preparsed, report, (uint)report.Length);
                if (status != Native.HidSuccess)
                {
                    Once(device, axis.Name + ": HID-Status 0x" + status.ToString("X8"));
                    continue;
                }
                axis.Value = Decoder.SignedValue(value, axis.Bits, axis.Minimum);
                axis.ValueReport = device.Reports;
            }
            foreach (ButtonGroup group in device.Buttons)
            {
                if (group.ReportId != report[0]) continue;
                var usages = new ushort[512];
                uint count = (uint)usages.Length;
                int status = Native.HidP_GetUsages(0, 9, group.Link, usages, ref count,
                    device.Preparsed, report, (uint)report.Length);
                if (status != Native.HidSuccess)
                {
                    Once(device, "Tasten: HID-Status 0x" + status.ToString("X8"));
                    continue;
                }
                group.Pressed = new HashSet<ushort>(usages.Take((int)count).Where(group.Available.Contains));
                group.Seen = true;
                group.ValueReport = device.Reports;
            }
            if (ReportReceived != null) ReportReceived(device);
        }

        private void Once(Device device, string error)
        {
            if (device.ReportedErrors.Add(error)) Error(device.Description + ": " + error);
        }

        public void Dispose()
        {
            if (registered)
            {
                var removal = new Native.Registration { UsagePage = 1, Usage = 8, Flags = 1 };
                if (!Native.RegisterRawInputDevices(new[] { removal }, 1,
                    (uint)Marshal.SizeOf(typeof(Native.Registration))))
                    Error("Raw-Input-Abmeldung: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
                registered = false;
            }
            foreach (Device device in Devices.Values) device.Dispose();
            Devices.Clear();
        }
    }
}
