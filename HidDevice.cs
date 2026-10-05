using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace D200xDirect;

public sealed record HidInterface(string Path, int UsagePage, int Usage, int InputLength, int OutputLength, int FeatureLength, int[] InputReportIds, int[] OutputReportIds);

public static class HidDevice
{
    const uint Present = 2, DeviceInterface = 16;
    const uint Read = 0x80000000, Write = 0x40000000, Overlapped = 0x40000000;

    public static IReadOnlyList<HidInterface> Enumerate()
    {
        Native.HidD_GetHidGuid(out var guid);
        var set = Native.SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, Present | DeviceInterface);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var result = new List<HidInterface>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var entry = new Native.InterfaceData { Size = Marshal.SizeOf<Native.InterfaceData>() };
                if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, index, ref entry))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259) break;
                    throw new Win32Exception(error);
                }
                Native.SetupDiGetDeviceInterfaceDetail(set, ref entry, IntPtr.Zero, 0, out var length, IntPtr.Zero);
                var detail = Marshal.AllocHGlobal((int)length);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref entry, detail, length, out _, IntPtr.Zero))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    var path = Marshal.PtrToStringUni(detail + 4)!;
                    if (!path.Contains("vid_2207&pid_0019", StringComparison.OrdinalIgnoreCase)) continue;
                    using var handle = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot inspect {path}");
                    if (!Native.HidD_GetPreparsedData(handle, out var preparsed))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    var caps = Marshal.AllocHGlobal(64);
                    try
                    {
                        if (Native.HidP_GetCaps(preparsed, caps) != 0x00110000)
                            throw new IOException("Could not read HID report capabilities.");
                        result.Add(new(path, (ushort)Marshal.ReadInt16(caps, 2), (ushort)Marshal.ReadInt16(caps, 0),
                            (ushort)Marshal.ReadInt16(caps, 4), (ushort)Marshal.ReadInt16(caps, 6), (ushort)Marshal.ReadInt16(caps, 8),
                            ReportIds(preparsed, caps, 0, 46, 48), ReportIds(preparsed, caps, 1, 52, 54)));
                    }
                    finally { Marshal.FreeHGlobal(caps); Native.HidD_FreePreparsedData(preparsed); }
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    static int[] ReportIds(IntPtr preparsed, IntPtr caps, short type, int buttonsOffset, int valuesOffset)
    {
        var ids = new HashSet<int>();
        foreach (var (offset, size, button) in new[] { (buttonsOffset, 72, true), (valuesOffset, 72, false) })
        {
            var count = (ushort)Marshal.ReadInt16(caps, offset);
            if (count == 0) continue;
            var buffer = Marshal.AllocHGlobal(count * size);
            try
            {
                var status = button ? Native.HidP_GetButtonCaps(type, buffer, ref count, preparsed) : Native.HidP_GetValueCaps(type, buffer, ref count, preparsed);
                if (status != 0x00110000) throw new IOException("Could not inspect HID report IDs.");
                for (var index = 0; index < count; index++) ids.Add(Marshal.ReadByte(buffer, index * size + 2));
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        return ids.Order().ToArray();
    }

    public static FileStream Open(HidInterface device, bool writable)
    {
        var handle = Native.CreateFile(device.Path, Read | (writable ? Write : 0), 3, IntPtr.Zero, 3, Overlapped, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Cannot open the D200X HID interface.");
        try { return new FileStream(handle, writable ? FileAccess.ReadWrite : FileAccess.Read, 1, true); }
        catch { handle.Dispose(); throw; }
    }

    static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct InterfaceData { public int Size; public Guid Class; public int Flags; public IntPtr Reserved; }
        [DllImport("hid.dll")] internal static extern void HidD_GetHidGuid(out Guid guid);
        [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
        [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool HidD_FreePreparsedData(IntPtr data);
        [DllImport("hid.dll")] internal static extern int HidP_GetCaps(IntPtr data, IntPtr caps);
        [DllImport("hid.dll")] internal static extern int HidP_GetButtonCaps(short type, IntPtr caps, ref ushort count, IntPtr preparsed);
        [DllImport("hid.dll")] internal static extern int HidP_GetValueCaps(short type, IntPtr caps, ref ushort count, IntPtr preparsed);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string? enumerator, IntPtr parent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid guid, uint index, ref InterfaceData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr info);
        [DllImport("setupapi.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    }
}
