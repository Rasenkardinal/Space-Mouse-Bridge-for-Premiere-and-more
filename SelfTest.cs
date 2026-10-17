using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace SpaceMouseBridge
{
    internal static class SelfTest
    {
        private static void Equal(long actual, long expected, string name)
        {
            if (actual != expected)
                throw new InvalidOperationException(name + ": " + actual + " statt " + expected);
        }

        internal static int Run()
        {
            Equal(Decoder.SignedValue(0, 16, -350), 0, "Neutral");
            Equal(Decoder.SignedValue(350, 16, -350), 350, "Positive Achse");
            Equal(Decoder.SignedValue(65186, 16, -350), -350, "Negative Achse");
            Equal(Decoder.SignedValue(0xfff, 12, -2048), -1, "12-Bit-Vorzeichen");
            Equal(Decoder.SignedValue(uint.MaxValue, 32, int.MinValue), -1, "32-Bit-Vorzeichen");
            Equal(Decoder.SignedValue(uint.MaxValue, 32, 0), uint.MaxValue, "Unsigned 32 Bit");
            Equal(Decoder.SignedValue(255, 8, 0), 255, "Unsigned 8 Bit");
            var reports = Decoder.SplitReports(new byte[] { 1, 2, 3, 4, 5, 6 }, 3, 2);
            Equal(reports.Count, 2, "Mehrere Reports");
            Equal(reports[1][0], 4, "Reportgrenze");
            bool rejected = false;
            try { Decoder.SplitReports(new byte[5], 3, 2); }
            catch (InvalidDataException) { rejected = true; }
            Equal(rejected ? 1 : 0, 1, "Abgeschnittener Report");
            rejected = false;
            try { Decoder.SplitReports(new byte[0], 0, 1); }
            catch (InvalidDataException) { rejected = true; }
            Equal(rejected ? 1 : 0, 1, "Leerer Report");
            rejected = false;
            try { Decoder.SignedValue(0, 0, 0); }
            catch (InvalidDataException) { rejected = true; }
            Equal(rejected ? 1 : 0, 1, "Ungueltige Bitbreite");
            Equal(Marshal.SizeOf(typeof(Native.Header)), IntPtr.Size == 8 ? 24 : 16, "RAWINPUTHEADER ABI");
            Equal(Marshal.SizeOf(typeof(Native.Registration)), IntPtr.Size == 8 ? 16 : 12, "RAWINPUTDEVICE ABI");
            Equal(Marshal.SizeOf(typeof(Native.DeviceList)), IntPtr.Size == 8 ? 16 : 8, "RAWINPUTDEVICELIST ABI");
            Equal(Marshal.SizeOf(typeof(Native.KeyboardInput)), 40, "INPUT x64 ABI");
            var gesture = new RotationGesture();
            Equal(gesture.Update(0.8), 0, "Start ausgelenkt gesperrt");
            Equal(gesture.Update(0), 0, "Neutral schaltet frei");
            Equal(gesture.Update(0.44), 0, "Unter Ausloeseschwelle");
            Equal(gesture.Update(0.45), 1, "Positive Schwelle");
            Equal(gesture.Update(0.9), 0, "Halten wiederholt nicht");
            Equal(gesture.Update(-0.8), 0, "Richtungswechsel ohne Neutral gesperrt");
            Equal(gesture.Update(0.13), 0, "Ausserhalb Neutralzone");
            Equal(gesture.Update(-0.8), 0, "Weiterhin gesperrt");
            Equal(gesture.Update(-0.12), 0, "Negative Neutralgrenze");
            Equal(gesture.Update(-0.45), -1, "Negative Schwelle");
            Equal(gesture.Update(0.12), 0, "Positive Neutralgrenze");
            gesture.Reset();
            Equal(gesture.Update(0.8), 0, "Reset fordert Neutral");
            Equal(gesture.Update(0), 0, "Nach Reset neutral");
            Equal(gesture.Update(0.8), 1, "Nach Reset freigegeben");
            gesture.Update(0);
            gesture.Expire();
            Equal(gesture.Update(0.8), 1, "Neutral bleibt nach Sendepause freigegeben");
            gesture.Expire();
            Equal(gesture.Update(-0.8), 0, "Sendepause ausgelenkt bleibt gesperrt");
            gesture.Update(0);
            gesture.Update(0.2);
            gesture.Expire();
            Equal(gesture.Update(0.8), 0, "Unterbrochene Bewegung fordert erneut Neutral");
            var navigation = BrowserCommand.Inputs(BrowserAction.Forward);
            Equal(navigation.Length, 4, "Eigener Alt-Akkord");
            Equal(navigation[0].Key, 0x12, "Eigener Alt-Down");
            Equal(navigation[3].Flags, 2, "Eigener Alt-Up");
            Equal(navigation[1].Key, 0x27, "Pfeil rechts");
            Equal(navigation[1].Flags, 1, "Pfeil extended");
            Equal(navigation[2].Flags, 3, "Pfeil extended key-up");
            Equal(BrowserCommand.ReleasePartial(navigation, 1)[0].Key, 0x12, "Nur eigenes Alt freigeben");
            Equal(BrowserCommand.ReleasePartial(navigation, 2).Length, 2, "Beide eigenen Tasten freigeben");
            Equal(BrowserCommand.ReleasePartial(navigation, 0).Length, 0, "Nichts eingefuegt, nichts freigeben");
            var gate = new TabReleaseGate();
            DateTime now = new DateTime(2026, 10, 16);
            gate.Request(1, now);
            Equal(gate.Update(false, now), 0, "Keine Ausgabe bei Auslenkung/Modifier");
            Equal(gate.Update(false, now.AddMilliseconds(59)), 0, "Waehrend Drehung warten");
            Equal(gate.Update(false, now.AddMilliseconds(60)), 0, "Waehrend Strg warten");
            Equal(gate.Update(true, now.AddMilliseconds(100)), 1, "Sofort bei Freigabe wechseln");
            Equal(gate.Update(true, now.AddMilliseconds(101)), 0, "Keine doppelte Ausgabe");
            Equal(gate.Update(false, now.AddMilliseconds(160)), 0, "Keine Vormerkung mehr");
            Equal(gate.Update(true, now.AddMilliseconds(600)), 0, "Keine Wiederholung");
            gate.Request(-1, now);
            gate.Update(false, now);
            Equal(gate.Update(true, now.AddMilliseconds(1)), -1, "Vorheriger Tab ohne Zusatzverzoegerung");
            gate.Request(1, now);
            gate.Reset();
            Equal(gate.Update(true, now.AddSeconds(1)), 0, "Fokuswechsel verwirft Vormerkung");
            gate.Request(1, now);
            Equal(gate.Update(true, now.AddSeconds(6)), 0, "Keine verspaetete Ausgabe");
            Equal(gate.Expired ? 1 : 0, 1, "Ablauf erkennbar");
            MappingTests.Run();
            ProfileTests.Run();
            JogTests.Run();
            ThemeTests.Run();
            PremiereBindingTests.Run();
            return 0;
        }

        private static void HidCheck(int status)
        {
            if (status != Native.HidSuccess)
                throw new InvalidDataException("HID-Teststatus: 0x" + status.ToString("X8"));
        }

        internal static int RunDescriptors(string output)
        {
            // These HID reports exist only in memory; nothing is sent to the device.
            var checks = new List<string>();
            using (var reader = new InputReader())
            {
                reader.Refresh();
                if (!reader.Devices.Values.Any(d => d.Axes.Count == 6))
                    throw new InvalidOperationException("Kein SpaceMouse-Deskriptor mit sechs Achsen vorhanden.");
                foreach (Device device in reader.Devices.Values)
                {
                    foreach (Axis axis in device.Axes)
                    {
                        foreach (int value in new[] { axis.Minimum, 0, axis.Maximum })
                        {
                            var report = new byte[device.ReportLength];
                            HidCheck(Native.HidP_InitializeReportForID(0, axis.ReportId,
                                device.Preparsed, report, (uint)report.Length));
                            HidCheck(Native.HidP_SetUsageValue(0, 1, axis.Link, axis.Usage,
                                unchecked((uint)value), device.Preparsed, report, (uint)report.Length));
                            reader.Decode(device, report);
                            if (!axis.Value.HasValue) throw new InvalidOperationException("Achsenwert fehlt.");
                            Equal(axis.Value.Value, value, axis.Name);
                            checks.Add(axis.Name + " = " + value);
                        }
                    }
                    foreach (ButtonGroup group in device.Buttons)
                    {
                        foreach (ushort button in group.Available)
                        {
                            var report = new byte[device.ReportLength];
                            HidCheck(Native.HidP_InitializeReportForID(0, group.ReportId,
                                device.Preparsed, report, (uint)report.Length));
                            uint count = 1;
                            HidCheck(Native.HidP_SetUsages(0, 9, group.Link, new[] { button },
                                ref count, device.Preparsed, report, (uint)report.Length));
                            reader.Decode(device, report);
                            Equal(group.Pressed.Contains(button) ? 1 : 0, 1, "Taste " + button);
                            HidCheck(Native.HidP_InitializeReportForID(0, group.ReportId,
                                device.Preparsed, report, (uint)report.Length));
                            reader.Decode(device, report);
                            Equal(group.Pressed.Count, 0, "Taste losgelassen");
                            checks.Add("Taste " + button + " gedrueckt/losgelassen");
                        }
                    }
                }
                File.WriteAllText(output, new JavaScriptSerializer().Serialize(new {
                    scope = "In-memory HID descriptor round-trip. No reports sent to hardware.",
                    checks = checks.ToArray(), errors = reader.Errors.ToArray()
                }));
                return reader.Errors.Count == 0 ? 0 : 1;
            }
        }
    }
}
