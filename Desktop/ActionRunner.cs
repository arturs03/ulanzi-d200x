using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace D200xDirect.App;

internal sealed class ActionRunner : IActionPlatform
{
    public static readonly ActionRunner Platform = new();
    public static ValueTask RunAsync(DeckAction action, InputEvent input, CancellationToken cancellation)
        => ActionCatalog.Default.ExecuteAsync(action, new ActionContext(input, Platform), cancellation);

    public void OpenUrl(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    public void LaunchApplication(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Configured application does not exist.", path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = false });
    }
    public void SendHotkey(IReadOnlyList<string> keys) => SendKeys(keys);
    static void SendKeys(IReadOnlyList<string> keys)
    {
        var modifiers = new HashSet<string>(["Ctrl", "Alt", "Shift", "Win"], StringComparer.OrdinalIgnoreCase);
        var codes = keys.OrderBy(k => modifiers.Contains(k) ? 0 : 1).Select(k => Profiles.VirtualKeys[k]).ToArray();
        // Do not release modifiers the user is physically holding.
        var alreadyDown = codes.Where(k => (GetAsyncKeyState(k) & 0x8000) != 0).ToHashSet();
        var inputs = codes.Where(k => !alreadyDown.Contains(k)).Select(k => Keyboard(k, false))
            .Concat(codes.Reverse().Where(k => !alreadyDown.Contains(k)).Select(k => Keyboard(k, true))).ToArray();
        if (inputs.Length == 0) return;
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeInput>());
        if (sent != inputs.Length)
        {
            // Best effort to release keys after partial delivery; don't leave injected modifiers down.
            var releases = codes.Where(k => !alreadyDown.Contains(k)).Select(k => Keyboard(k, true)).ToArray();
            SendInput((uint)releases.Length, releases, Marshal.SizeOf<NativeInput>());
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not accept the hotkey. Elevated apps may reject it.");
        }
    }

    static NativeInput Keyboard(ushort key, bool release) => new()
    {
        Type = 1,
        Data = new InputUnion { Keyboard = new KeyboardInput { Key = key, Flags = release ? 2u : 0u } }
    };

    [StructLayout(LayoutKind.Sequential)] struct NativeInput { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X, Y; public uint MouseData, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, NativeInput[] inputs, int size);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
}
