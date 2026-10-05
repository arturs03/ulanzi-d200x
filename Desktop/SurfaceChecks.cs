namespace D200xDirect.App;

internal static class SurfaceChecks
{
    // Exercise WinForms invalidation, not a fresh DrawToBitmap that masks stale borders.
    public static void Run()
    {
        using var host = new Form { ClientSize = new Size(420, 360), ShowInTaskbar = false };
        using var panel = new SurfacePanel { Location = new Point(10, 10), Size = new Size(340, 260) };
        host.Controls.Add(panel); host.Show(); Application.DoEvents();
        var dirty = Rectangle.Empty;
        panel.Invalidated += (_, e) => dirty = dirty.IsEmpty ? e.InvalidRect : Rectangle.Union(dirty, e.InvalidRect);
        foreach (var next in new[] { new Size(340, 200), new Size(300, 200), new Size(300, 290), new Size(360, 290) })
        {
            panel.Update(); dirty = Rectangle.Empty;
            panel.Size = next;
            if (!dirty.Contains(panel.ClientRectangle)) throw new IOException("Resizing a surface did not invalidate its full border/background.");
            Application.DoEvents();
        }
        host.Close();
    }
}
