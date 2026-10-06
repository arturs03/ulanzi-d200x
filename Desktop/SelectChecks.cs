using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace D200xDirect.App;

internal static class SelectChecks
{
    // Target only this isolated window's HWNDs. No global mouse/keyboard injection.
    public static async Task RunAsync()
    {
        using var host = new Form { ClientSize = new Size(620, 340), ShowInTaskbar = false };
        using var first = Theme.Select(); using var second = Theme.Select();
        first.Location = new Point(20, 20); second.Location = new Point(330, 20);
        first.Items.AddRange(["One", "Two", "Three"]); second.Items.AddRange(["Alpha", "Beta"]);
        first.SelectedIndex = second.SelectedIndex = 0;
        using var outside = new Panel { Location = new Point(20, 200), Size = new Size(500, 80) };
        host.Controls.AddRange([first, second, outside]); host.Show();
        try
        {
            await OpenAsync(first);
            var firstMenu = first.DropDownForCheck!;
            PostClick(firstMenu, Center(firstMenu.Items[1].Bounds));
            await UntilAsync(() => first.SelectedIndex == 1 && !firstMenu.Visible, "Selecting a menu item did not update/close the dropdown.");
            if (firstMenu.IsDisposed) throw new IOException("Selection destroyed the menu during native click dispatch.");

            await OpenAsync(first);
            PostClick(outside, new Point(10, 10));
            await UntilAsync(() => !firstMenu.Visible, "Clicking outside did not dismiss the dropdown.");
            if (first.SelectedIndex != 1) throw new IOException("Dismissal changed the selection.");

            await OpenAsync(first);
            PostClick(second, new Point(second.Width / 2, second.Height / 2));
            await UntilAsync(() => !firstMenu.Visible, "Switching dropdowns left the old menu open.");
            // Hidden package checks cannot rely on a button's screen hit-test/foreground
            // state. Its Click path is the same; menu selection/dismissal still uses HWND messages.
            if (second.DropDownForCheck is not { Visible: true }) await OpenAsync(second);
            var secondMenu = second.DropDownForCheck!;
            PostClick(secondMenu, Center(secondMenu.Items[1].Bounds));
            await UntilAsync(() => second.SelectedIndex == 1 && !secondMenu.Visible, "Switched dropdown selection failed.");

            for (var i = 0; i < 30; i++)
            {
                var select = i % 2 == 0 ? first : second;
                await OpenAsync(select);
                var menu = select.DropDownForCheck!;
                if (!ReferenceEquals(menu, select == first ? firstMenu : secondMenu)) throw new IOException("Repeated opens accumulated new menus.");
                if (i % 3 == 0)
                {
                    var index = (select.SelectedIndex + 1) % select.Items.Count;
                    PostClick(menu, Center(menu.Items[index].Bounds));
                    await UntilAsync(() => select.SelectedIndex == index && !menu.Visible, "Repeated item selection failed.");
                }
                else
                {
                    PostClick(outside, new Point(10, 10));
                    await UntilAsync(() => !menu.Visible, "Repeated outside dismissal failed.");
                }
                if (menu.IsDisposed) throw new IOException("A repeated close disposed a reusable menu.");
            }

            // The key editor repopulates choices between openings; stale handlers must go away.
            first.Items.Clear(); first.Items.AddRange(["New A", "New B"]); first.SelectedIndex = 0;
            await OpenAsync(first);
            if (firstMenu.Items.Count != 2 || firstMenu.Items[1].Text != "New B") throw new IOException("Dropdown kept old choices after repopulation.");
            PostClick(firstMenu, Center(firstMenu.Items[1].Bounds));
            await UntilAsync(() => first.SelectedItem?.ToString() == "New B" && !firstMenu.Visible, "Repopulated selection used a stale handler.");

            await OpenAsync(second);
            second.Dispose();
            await UntilAsync(() => secondMenu.IsDisposed, "Disposing an open dropdown leaked its menu.");

            // Closing/removing the owner from an item callback must also wait for dispatch.
            first.SelectedIndexChanged += (_, _) => first.Dispose();
            await OpenAsync(first);
            PostClick(firstMenu, Center(firstMenu.Items[0].Bounds));
            await UntilAsync(() => first.IsDisposed && firstMenu.IsDisposed, "Removing the select in a selection callback leaked its menu.");
        }
        finally { host.Close(); }
        Console.WriteLine("PASS: native dropdown selection, outside dismissal, switching, 30 reopen/close cycles, repopulation and owner disposal.");
    }

    static async Task OpenAsync(ModernSelect select)
    {
        select.PerformClick();
        await UntilAsync(() => select.DropDownForCheck is { Visible: true }, "Dropdown did not open.");
    }

    static Point Center(Rectangle rectangle) => new(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);

    static void PostClick(Control control, Point point)
    {
        var coordinates = (nint)((point.Y << 16) | (point.X & 0xffff));
        if (!PostMessage(control.Handle, 0x0201, 1, coordinates) || !PostMessage(control.Handle, 0x0202, 0, coordinates))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    static async Task UntilAsync(Func<bool> condition, string failure)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed > TimeSpan.FromSeconds(3)) throw new IOException(failure);
            await Task.Delay(10);
        }
    }

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PostMessage(nint window, uint message, nuint wParam, nint lParam);
}
