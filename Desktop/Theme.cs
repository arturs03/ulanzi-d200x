using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace D200xDirect.App;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(17, 19, 24);
    public static readonly Color Surface = Color.FromArgb(25, 28, 35);
    public static readonly Color Raised = Color.FromArgb(34, 38, 47);
    public static readonly Color Border = Color.FromArgb(49, 55, 67);
    public static readonly Color Text = Color.FromArgb(240, 242, 248);
    public static readonly Color Muted = Color.FromArgb(148, 157, 175);
    public static readonly Color Accent = Color.FromArgb(129, 177, 255);
    public static readonly Color Success = Color.FromArgb(103, 211, 169);

    public static GraphicsPath Round(RectangleF rect, float radius)
    {
        var path = new GraphicsPath(); var diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); return path;
    }

    public static Label Label(string text, float size = 10, Color? color = null, bool bold = false) => new()
    {
        Text = text, AutoSize = true, UseMnemonic = false, ForeColor = color ?? Text, BackColor = Color.Transparent,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = new Padding(0, 0, 0, 6)
    };

    public static TextBox Input() => new()
    {
        BackColor = Raised, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 11), Width = 242, Margin = new Padding(0, 0, 0, 12)
    };

    public static ModernSelect Select() => new()
    {
        BackColor = Surface, ForeColor = Text,
        Font = new Font("Segoe UI", 10), Width = 242, Margin = new Padding(0, 0, 0, 12)
    };

    public static void DarkCaption(Form form)
    {
        try { int enabled = 1; DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)); }
        catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}

internal sealed class ModernSelect : ModernButton
{
    int selectedIndex = -1;
    public List<object> Items { get; } = [];
    public string DisplayMember { get; set; } = "";
    public event EventHandler? SelectedIndexChanged;
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            selectedIndex = value; Text = value >= 0 ? Caption(Items[value]) + "   ▾" : "Select…   ▾";
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty); Invalidate();
        }
    }
    public object? SelectedItem { get => selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : null; set => SelectedIndex = value is null ? -1 : Items.IndexOf(value); }
    public ModernSelect() { Height = 34; TextAlign = ContentAlignment.MiddleLeft; AccessibleRole = AccessibleRole.ComboBox; }
    static string Caption(object item) => item is ActionDescriptor descriptor ? descriptor.Title : item.ToString() ?? "";
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        var menu = new ContextMenuStrip { BackColor = Theme.Raised, ForeColor = Theme.Text, Font = Font, ShowImageMargin = false,
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) };
        foreach (var item in Items)
        {
            var entry = menu.Items.Add(Caption(item)); entry.ForeColor = Theme.Text;
            entry.Click += (_, _) => SelectedItem = item;
        }
        menu.Closed += (_, _) => menu.Dispose(); menu.Show(this, new Point(0, Height));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Up or Keys.Down)
        {
            SelectedIndex = Math.Clamp(selectedIndex + (e.KeyCode == Keys.Down ? 1 : -1), 0, Items.Count - 1); e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}

internal sealed class DarkMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.Raised;
    public override Color MenuBorder => Theme.Border;
    public override Color MenuItemBorder => Theme.Border;
    public override Color MenuItemSelected => Theme.Border;
    public override Color ImageMarginGradientBegin => Theme.Raised;
    public override Color ImageMarginGradientMiddle => Theme.Raised;
    public override Color ImageMarginGradientEnd => Theme.Raised;
}

internal sealed class SurfacePanel : Panel
{
    public SurfacePanel() { DoubleBuffered = true; BackColor = Theme.Background; Padding = new Padding(18); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 12);
        using var fill = new SolidBrush(Theme.Surface); using var pen = new Pen(Theme.Border);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(pen, shape);
    }
}

internal class ModernButton : Button
{
    bool hover;
    public bool Primary { get; set; }
    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
        BackColor = Theme.Background; ForeColor = Theme.Text; Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 10); Size = new Size(134, 38); Margin = new Padding(0, 0, 8, 0);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 7);
        using var fill = new SolidBrush(!Enabled ? Theme.Surface : Primary ? (hover ? Color.FromArgb(157, 195, 255) : Theme.Accent) : hover ? Theme.Border : Theme.Raised);
        using var pen = new Pen(Focused && Enabled ? Theme.Accent : Theme.Border);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(pen, shape);
        var textRect = new Rectangle(12, 0, Width - 24, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, textRect, !Enabled ? Theme.Muted : Primary ? Theme.Background : Theme.Text,
            (TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class Toggle : CheckBox
{
    public Toggle()
    {
        AutoSize = false; Size = new Size(200, 34); Cursor = Cursors.Hand; ForeColor = Theme.Text;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = Theme.Round(new RectangleF(1, 8, 34, 18), 9);
        using var fill = new SolidBrush(Checked ? Theme.Accent : Theme.Border);
        e.Graphics.FillPath(fill, track);
        using var dot = new SolidBrush(Checked ? Theme.Background : Theme.Text);
        e.Graphics.FillEllipse(dot, Checked ? 20 : 4, 11, 12, 12);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(45, 0, Width - 45, Height), Enabled ? Theme.Text : Theme.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle, Theme.Accent, BackColor);
    }
}

internal sealed class DeckTile : Button
{
    public int Index { get; init; }
    public string Caption { get; set; } = "";
    public string Detail { get; set; } = "Unassigned";
    public Color Stripe { get; set; } = Theme.Accent;
    public bool Selected { get; set; }
    bool hover;
    public DeckTile()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Dock = DockStyle.Fill;
        BackColor = Theme.Surface; Margin = new Padding(4); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Surface); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 9);
        using var fill = new SolidBrush(Selected ? Color.FromArgb(39, 52, 72) : hover ? Color.FromArgb(42, 47, 57) : Theme.Raised);
        using var border = new Pen(Selected || Focused ? Theme.Accent : Theme.Border, Selected ? 1.5f : 1);
        e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(border, shape);
        using var small = new Font("Segoe UI", 8.5f); using var title = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        using var stripe = new SolidBrush(Stripe); e.Graphics.FillRectangle(stripe, 12, 14, 3, 12);
        TextRenderer.DrawText(e.Graphics, Index.ToString("D2"), small, new Rectangle(21, 10, Width - 30, 21), Theme.Muted, TextFormatFlags.Left);
        TextRenderer.DrawText(e.Graphics, Caption, title, new Rectangle(12, Height / 2 - 9, Width - 24, 25), Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(e.Graphics, Detail, small, new Rectangle(9, Height - 28, Width - 18, 20), Theme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        AccessibleName = $"Control {Index}: {Caption}, {Detail}";
    }
}
