using System;
using System.Runtime.InteropServices;

namespace SpaceMouseBridge
{
    internal static class Native
    {
        internal const uint DeviceInfo = 0x2000000b;
        internal const uint DeviceName = 0x20000007;
        internal const uint PreparsedData = 0x20000005;
        internal const int HidSuccess = 0x00110000;

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        internal struct KeyboardInput
        {
            [FieldOffset(0)] internal uint Type;
            [FieldOffset(8)] internal ushort Key;
            [FieldOffset(12)] internal uint Flags;
            [FieldOffset(16)] internal int MouseData;
            [FieldOffset(20)] internal uint MouseFlags;
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string message);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr w, IntPtr l);

        internal delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int capacity);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int capacity);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint SendInput(uint count, KeyboardInput[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)]
        internal struct DeviceList
        {
            internal IntPtr Handle;
            internal uint Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Registration
        {
            internal ushort UsagePage;
            internal ushort Usage;
            internal uint Flags;
            internal IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Header
        {
            internal uint Type;
            internal uint Size;
            internal IntPtr Device;
            internal IntPtr WParam;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputDeviceList(
            [Out] DeviceList[] list, ref uint count, uint size);

        [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
        internal static extern uint GetRawInputDeviceInfo(
            IntPtr device, uint command, IntPtr data, ref uint size);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterRawInputDevices(
            Registration[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetRawInputData(
            IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetCaps(IntPtr preparsed, [Out] byte[] caps);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetValueCaps(
            int reportType, [Out] byte[] caps, ref ushort count, IntPtr preparsed);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetButtonCaps(
            int reportType, [Out] byte[] caps, ref ushort count, IntPtr preparsed);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetUsageValue(
            int reportType, ushort page, ushort link, ushort usage, out uint value,
            IntPtr preparsed, byte[] report, uint length);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetUsages(
            int reportType, ushort page, ushort link, [Out] ushort[] usages,
            ref uint count, IntPtr preparsed, byte[] report, uint length);

        [DllImport("hid.dll")]
        internal static extern int HidP_InitializeReportForID(
            int reportType, byte reportId, IntPtr preparsed, [Out] byte[] report, uint length);

        [DllImport("hid.dll")]
        internal static extern int HidP_SetUsageValue(
            int reportType, ushort page, ushort link, ushort usage, uint value,
            IntPtr preparsed, [In, Out] byte[] report, uint length);

        [DllImport("hid.dll")]
        internal static extern int HidP_SetUsages(
            int reportType, ushort page, ushort link, ushort[] usages, ref uint count,
            IntPtr preparsed, [In, Out] byte[] report, uint length);
    }
}
