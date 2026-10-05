using System.Diagnostics;
using System.Text.Json;

namespace D200xDirect.App;

internal sealed partial class MainForm
{
    readonly Label status = Theme.Label("Stopped", 11, Theme.Muted, true);
    readonly Label sessionHint = Theme.Label("Start listening to run your controls and keep-awake updates.", 9, Theme.Muted);
    readonly Label profileName = Theme.Label("Starter", 16, bold: true);
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
        BackColor = Theme.Surface, ForeColor = Theme.Muted, BorderStyle = BorderStyle.None, Font = new Font("Cascadia Mono", 9) };
    readonly TableLayoutPanel grid = new() { ColumnCount = 5, RowCount = 3, Dock = DockStyle.Fill, BackColor = Theme.Surface };
    readonly TableLayoutPanel otherControls = new() { ColumnCount = 5, RowCount = 1, Dock = DockStyle.Fill, BackColor = Theme.Surface };
    readonly ModernButton start = new() { Text = "Start listening", Primary = true, Width = 144 };
    readonly ModernButton stop = new() { Text = "Stop listening", Width = 144, Visible = false };
    readonly ModernButton sendPage = new() { Text = "Send to device", Width = 144 };
    readonly Toggle actions = new() { Text = "Run actions", Width = 150 };
    readonly Toggle keepAwake = new() { Text = "Keep awake", Width = 150 };
    readonly Label editorTitle = Theme.Label("Button 00", 17, bold: true);
    readonly TextBox labelInput = Theme.Input();
    readonly TextBox colorInput = Theme.Input();
    readonly Label labelCaption = Theme.Label("Label", 9, Theme.Muted);
    readonly Label colorCaption = Theme.Label("Display color", 9, Theme.Muted);
    readonly Label gestureCaption = Theme.Label("Trigger", 9, Theme.Muted);
    readonly ModernSelect gestureInput = Theme.Select();
    readonly ModernSelect actionInput = Theme.Select();
    readonly TextBox valueInput = Theme.Input();
    readonly Label valueCaption = Theme.Label("Shortcut", 9, Theme.Muted);
    readonly Label actionHelp = Theme.Label("", 9, Theme.Muted);
    readonly Label editorFeedback = Theme.Label("Saved locally. Send to device to update labels.", 9, Theme.Muted);
    readonly ModernButton browse = new() { Text = "Choose application…", Width = 242, Visible = false };
    readonly Dictionary<int, DeckTile> tiles = [];
    int selectedIndex;
    bool loadingEditor;

    void BuildLayout()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "D200X Direct"; ClientSize = new Size(1180, 850); MinimumSize = new Size(940, 650);
        Font = new Font("Segoe UI", 10); BackColor = Theme.Background; ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen; DoubleBuffered = true;
        HandleCreated += (_, _) => Theme.DarkCaption(this);
        Shown += (_, _) =>
        {
            var available = Screen.FromControl(this).WorkingArea;
            if (Width > available.Width || Height > available.Height)
            {
                Size = new Size(Math.Min(Width, available.Width - 24), Math.Min(Height, available.Height - 24));
                Location = new Point(available.X + (available.Width - Width) / 2, available.Y + (available.Height - Height) / 2);
            }
        };
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(24), BackColor = Theme.Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        var brand = Stack(); brand.Controls.Add(Theme.Label("D200X Direct", 24, bold: true));
        header.Controls.Add(brand, 0, 0);
        var hide = Button("Hide to tray", Hide, 120); hide.Anchor = AnchorStyles.Top | AnchorStyles.Right; header.Controls.Add(hide, 1, 0);
        root.Controls.Add(header, 0, 0);

        var sessionPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 4, 0, 12) };
        sessionPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); sessionPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        bar.Controls.AddRange([start, stop, sendPage, Button("Inspect USB", Inspect, 116), keepAwake, actions]);
        keepAwake.Margin = new Padding(16, 2, 0, 0); actions.Margin = new Padding(6, 2, 0, 0);
        sessionPanel.Controls.Add(bar, 0, 0);
        var state = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(0, 7, 0, 0) };
        status.Margin = new Padding(0, 0, 18, 0); state.Controls.AddRange([status, sessionHint]); sessionPanel.Controls.Add(state, 0, 1);
        root.Controls.Add(sessionPanel, 0, 1);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 298));
        var deck = new SurfacePanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 16, 12) };
        var deckScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Surface };
        var deckLayout = new TableLayoutPanel { Dock = DockStyle.Top, MinimumSize = new Size(0, 420), Height = 420, ColumnCount = 1, RowCount = 5, BackColor = Theme.Surface };
        deckScroll.Resize += (_, _) =>
        {
            deckLayout.Height = Math.Max(deckLayout.MinimumSize.Height, deckScroll.ClientSize.Height);
        };
        deckLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); deckLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        deckLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); deckLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); deckLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        deckLayout.Controls.Add(profileName, 0, 0); deckLayout.Controls.Add(Theme.Label("YOUR DECK   /   Select a control to edit its mapping", 9, Theme.Muted), 0, 1);
        for (var i = 0; i < 5; i++) { grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); otherControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20)); }
        for (var i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        for (var i = 0; i < 14; i++)
        {
            var tile = CreateTile(i); grid.Controls.Add(tile, i % 5, i / 5); if (i == 13) grid.SetColumnSpan(tile, 2);
        }
        deckLayout.Controls.Add(grid, 0, 2);
        var dialHeading = Theme.Label("DIALS & SIDE BUTTONS", 9, Theme.Muted); dialHeading.Margin = new Padding(4, 10, 0, 0); deckLayout.Controls.Add(dialHeading, 0, 3);
        var indices = new[] { 17, 18, 19, 15, 16 };
        for (var i = 0; i < indices.Length; i++) otherControls.Controls.Add(CreateTile(indices[i]), i, 0);
        deckLayout.Controls.Add(otherControls, 0, 4); deckScroll.Controls.Add(deckLayout); deck.Controls.Add(deckScroll); body.Controls.Add(deck, 0, 0);

        var inspector = new SurfacePanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
        var inspectorLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Theme.Surface };
        inspectorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); inspectorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        inspectorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); inspectorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        inspectorLayout.Controls.Add(editorTitle, 0, 0);
        var fields = Stack(); fields.Dock = DockStyle.Fill; fields.AutoScroll = true; fields.BackColor = Theme.Surface;
        fields.Controls.Add(labelCaption); fields.Controls.Add(labelInput); labelInput.MaxLength = 64;
        fields.Controls.Add(colorCaption); fields.Controls.Add(colorInput); colorInput.MaxLength = 7;
        fields.Controls.Add(gestureCaption); fields.Controls.Add(gestureInput);
        AddField(fields, "Action", actionInput);
        actionInput.DisplayMember = nameof(ActionDescriptor.Title);
        foreach (var descriptor in ActionCatalog.Default.Descriptors) actionInput.Items.Add(descriptor);
        actionHelp.AutoSize = false; actionHelp.Size = new Size(242, 46); fields.Controls.Add(actionHelp);
        fields.Controls.Add(valueCaption); fields.Controls.Add(valueInput); fields.Controls.Add(browse);
        valueInput.Margin = new Padding(0, 0, 0, 12); browse.Margin = new Padding(0, 0, 0, 12);
        var save = Button("Save mapping", SaveMapping, 242); save.Primary = true; save.Dock = DockStyle.Fill; save.Margin = new Padding(0, 2, 0, 8);
        editorFeedback.AutoSize = false; editorFeedback.Dock = DockStyle.Fill; editorFeedback.Margin = Padding.Empty;
        inspectorLayout.Controls.Add(fields, 0, 1); inspectorLayout.Controls.Add(save, 0, 2); inspectorLayout.Controls.Add(editorFeedback, 0, 3);
        inspector.Controls.Add(inspectorLayout); body.Controls.Add(inspector, 1, 0); root.Controls.Add(body, 0, 2);

        var activity = new SurfacePanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(18, 12, 18, 12) };
        var activityLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Theme.Surface };
        activityLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); activityLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Surface };
        var activityTitle = Theme.Label("Activity", 11, bold: true); activityTitle.Margin = new Padding(0, 8, 18, 0); tools.Controls.Add(activityTitle);
        tools.Controls.AddRange([Button("Edit JSON", () => OpenText(ProfilePath), 98), Button("Reload", ReloadProfile, 80),
            Button("Profile folder", () => Process.Start(new ProcessStartInfo(profileDirectory) { UseShellExecute = true }), 112),
            Button("LLM guide", () => OpenText(Path.Combine(AppContext.BaseDirectory, "docs", "customization.md")), 100)]);
        activityLayout.Controls.Add(tools, 0, 0); activityLayout.Controls.Add(log, 0, 1); activity.Controls.Add(activityLayout); root.Controls.Add(activity, 0, 3);
        Controls.Add(root);
        ResumeLayout(performLayout: false);

        actionInput.SelectedIndexChanged += (_, _) => { if (!loadingEditor) ConfigureActionField(clear: true); };
        gestureInput.SelectedIndexChanged += (_, _) => { if (!loadingEditor) LoadEditorAction(); };
        browse.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Filter = "Windows applications (*.exe)|*.exe", CheckFileExists = true, Title = "Choose an application" };
            if (picker.ShowDialog(this) == DialogResult.OK) valueInput.Text = picker.FileName;
        };
    }

    static FlowLayoutPanel Stack() => new() { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = false, Dock = DockStyle.Fill, Margin = Padding.Empty };
    static void AddField(FlowLayoutPanel fields, string caption, Control input) { fields.Controls.Add(Theme.Label(caption, 9, Theme.Muted)); fields.Controls.Add(input); }
    ModernButton Button(string text, Action action, int width)
    {
        var button = new ModernButton { Text = text, Width = width };
        button.Click += (_, _) => { try { action(); } catch (Exception error) { Log(error.Message); } };
        return button;
    }

    DeckTile CreateTile(int index)
    {
        var tile = new DeckTile { Index = index }; tiles.Add(index, tile);
        tile.Click += (_, _) => SelectControl(index); return tile;
    }

    void SetStatus(string title, string hint, bool active = false)
    {
        status.Text = title; status.ForeColor = active ? Theme.Success : Theme.Muted; sessionHint.Text = hint;
    }

    void RefreshDeck()
    {
        profileName.Text = profile.Name;
        foreach (var (index, tile) in tiles)
        {
            if (index <= 13)
            {
                var key = profile.Keys.FirstOrDefault(k => k.Index == index) ?? new KeyConfig { Index = index };
                tile.Caption = string.IsNullOrEmpty(key.Label) ? $"Key {index:D2}" : key.Label;
                tile.Detail = ActionCatalog.Default.Describe(key.Action.Type).Title; tile.Stripe = ColorTranslator.FromHtml(key.Background);
            }
            else if (index >= 17)
            {
                var dial = profile.Dials.FirstOrDefault(d => d.Index == index);
                tile.Caption = string.IsNullOrEmpty(dial?.Label) ? $"Dial {index - 16}" : dial.Label;
                tile.Detail = "Turn · Press"; tile.Stripe = Theme.Accent;
            }
            else
            {
                var side = profile.SideButtons.FirstOrDefault(s => s.Index == index);
                tile.Caption = $"Side {index - 14}"; tile.Detail = ActionCatalog.Default.Describe(side?.Action.Type ?? "none").Title; tile.Stripe = Theme.Muted;
            }
            tile.Selected = index == selectedIndex; tile.Invalidate();
        }
    }

    void SelectControl(int index)
    {
        loadingEditor = true;
        try
        {
            selectedIndex = index;
            foreach (var tile in tiles.Values) { tile.Selected = tile.Index == index; tile.Invalidate(); }
            editorTitle.Text = index <= 13 ? $"Button {index:D2}" : index >= 17 ? $"Dial {index - 16}" : $"Side button {index - 14}";
            labelInput.Enabled = index is not (15 or 16); colorInput.Enabled = index <= 13;
            labelInput.Visible = labelCaption.Visible = index is not (15 or 16);
            colorInput.Visible = colorCaption.Visible = index <= 13;
            gestureInput.Visible = gestureCaption.Visible = index >= 17;
            labelInput.Text = index <= 13 ? profile.Keys.FirstOrDefault(k => k.Index == index)?.Label ?? "" : profile.Dials.FirstOrDefault(d => d.Index == index)?.Label ?? "";
            colorInput.Text = index <= 13 ? profile.Keys.FirstOrDefault(k => k.Index == index)?.Background ?? "#102038" : "";
            gestureInput.Items.Clear();
            gestureInput.Items.AddRange(index >= 17 ? ["Turn left", "Turn right", "Press"] : ["Press"]);
            gestureInput.SelectedIndex = 0; gestureInput.Enabled = index >= 17;
            editorFeedback.Text = "Save your mapping here, or edit the JSON with your LLM."; editorFeedback.ForeColor = Theme.Muted;
            LoadEditorAction();
        }
        finally { loadingEditor = false; }
    }

    string Gesture => gestureInput.SelectedItem?.ToString() switch { "Turn left" => "left", "Turn right" => "right", _ => "press" };
    void LoadEditorAction()
    {
        var wasLoading = loadingEditor; loadingEditor = true;
        try
        {
            var input = new InputEvent(selectedIndex, selectedIndex <= 13 ? "key" : selectedIndex >= 17 ? "dial" : "side", Gesture);
            var action = Profiles.ActionFor(profile, input) ?? new DeckAction();
            actionInput.SelectedItem = actionInput.Items.Cast<ActionDescriptor>().Single(d => d.Id == action.Type);
            ConfigureActionField(clear: false);
            valueInput.Text = action.Type switch { "hotkey" => string.Join(" + ", action.Keys), "open-url" => action.Url ?? "", "launch" => action.Path ?? "", _ => "" };
        }
        finally { loadingEditor = wasLoading; }
    }

    void ConfigureActionField(bool clear)
    {
        if (actionInput.SelectedItem is not ActionDescriptor descriptor) return;
        actionHelp.Text = descriptor.Description;
        valueInput.Visible = valueCaption.Visible = descriptor.ValueKind != ActionValueKind.None;
        browse.Visible = descriptor.ValueKind == ActionValueKind.Application;
        valueCaption.Text = descriptor.ValueKind switch { ActionValueKind.Shortcut => "Shortcut / media key", ActionValueKind.Url => "Website address", _ => "Application path" };
        valueInput.PlaceholderText = descriptor.ValueKind switch { ActionValueKind.Shortcut => "Ctrl + Alt + Shift + M", ActionValueKind.Url => "https://example.com", _ => @"C:\Path\Application.exe" };
        if (clear) valueInput.Clear();
    }

    void SaveMapping()
    {
        try
        {
            if (actionInput.SelectedItem is not ActionDescriptor descriptor) throw new ArgumentException("Select an action.");
            var action = new DeckAction { Type = descriptor.Id };
            switch (descriptor.ValueKind)
            {
                case ActionValueKind.Shortcut: action.Keys = valueInput.Text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList(); break;
                case ActionValueKind.Url: action.Url = valueInput.Text.Trim(); break;
                case ActionValueKind.Application: action.Path = valueInput.Text.Trim(); break;
            }
            // Respect edits made outside the UI rather than overwriting a stale in-memory copy.
            if (new FileInfo(ProfilePath).Length > 1_048_576) throw new IOException("Profile exceeds 1 MB.");
            var originalJson = File.ReadAllText(ProfilePath);
            var current = Profiles.Parse(originalJson);
            var candidate = ProfileEditing.Update(current, selectedIndex, Gesture, labelInput.Text, colorInput.Text, action);
            var temporary = Path.Combine(profileDirectory, $".profile-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(candidate, Profiles.JsonOptions));
                if (new FileInfo(ProfilePath).Length > 1_048_576 || File.ReadAllText(ProfilePath) != originalJson)
                    throw new IOException("Your profile changed during this edit. Reload it and try again.");
                File.Move(temporary, ProfilePath, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            Volatile.Write(ref profile, candidate); RefreshDeck();
            editorFeedback.ForeColor = Theme.Success; editorFeedback.Text = "Saved. Send to device to update labels. Enable Run actions to use the mapping.";
            Log($"Saved {descriptor.Title} for control {selectedIndex}, {Gesture}. Device display unchanged.");
        }
        catch (Exception error) { editorFeedback.ForeColor = Color.FromArgb(255, 159, 159); editorFeedback.Text = error.Message; Log($"Mapping rejected: {error.Message}"); }
    }

    // Used only with --ui-check's isolated profile directory; no USB or action execution.
    internal void CheckEditor()
    {
        SelectControl(17); gestureInput.SelectedIndex = 1;
        actionInput.SelectedItem = actionInput.Items.Cast<ActionDescriptor>().Single(d => d.Id == "hotkey"); valueInput.Text = "VolumeUp";
        SaveMapping();
        var saved = Profiles.Parse(File.ReadAllText(ProfilePath));
        if (saved.Dials.Single(d => d.Index == 17).Right.Keys.Single() != "VolumeUp") throw new IOException("Dial editor save failed.");
        var before = File.ReadAllText(ProfilePath); valueInput.Text = "Ctrl"; SaveMapping();
        if (File.ReadAllText(ProfilePath) != before) throw new IOException("Invalid editor mapping changed the profile.");
        SelectControl(13); labelInput.Text = "Wide screen"; SaveMapping();
        if (Profiles.Parse(File.ReadAllText(ProfilePath)).Keys.Single(k => k.Index == 13).Label != "Wide screen") throw new IOException("Wide-screen editor save failed.");
        SelectControl(15); actionInput.SelectedItem = actionInput.Items.Cast<ActionDescriptor>().Single(d => d.Id == "open-url");
        valueInput.Text = "https://example.com"; SaveMapping();
        if (Profiles.Parse(File.ReadAllText(ProfilePath)).SideButtons.Single(s => s.Index == 15).Action.Url != "https://example.com") throw new IOException("Side-button editor save failed.");
        SelectControl(17); gestureInput.SelectedIndex = 1;
        SetBusy(true);
        if (start.Visible || !stop.Visible || keepAwake.Enabled || sendPage.Enabled) throw new IOException("Running-session controls are inconsistent.");
        SetBusy(false);
    }

    internal void CheckCompactLayout()
    {
        if (grid.Height < 210 * DeviceDpi / 96 || tiles.Values.Any(tile => tile.Height < 60 * DeviceDpi / 96))
            throw new IOException("Compact layout collapsed physical control cards.");
    }
}
