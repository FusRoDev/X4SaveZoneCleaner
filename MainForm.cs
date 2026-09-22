using System.ComponentModel;

namespace X4SaveZoneCleaner;

public sealed class MainForm : Form
{
    private readonly TextBox _path = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly NumericUpDown _threshold = new() { Minimum = 1_000, Maximum = 1_000_000_000, Value = 1_000_000, Increment = 100_000, ThousandsSeparator = true, Width = 150 };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
    private readonly Button _analyse = new() { Text = "Analyse", Enabled = false, AutoSize = true };
    private readonly Button _write = new() { Text = "Remove selected zone blocks...", Enabled = false, AutoSize = true };
    private readonly Button _preview = new() { Text = "Preview selected XML...", Enabled = false, AutoSize = true };
    private readonly CheckBox _backup = new() { Text = "Create an additional source backup next to the output file", Checked = true, AutoSize = true };
    private readonly Label _status = new() { AutoSize = true, Text = "Choose an X4 save file to begin." };
    private readonly Panel _busyOverlay = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(95, 95, 95), Visible = false };
    private readonly Label _busyText = new() { AutoSize = true, ForeColor = Color.White, Font = new Font("Segoe UI", 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
    private readonly ProgressBar _busyProgress = new() { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Width = 230 };
    private SaveDocument? _save;
    private BindingList<ZoneRow> _rows = new();

    public MainForm()
    {
        Text = "X4 Save Zone Cleaner";
        MinimumSize = new Size(940, 530);
        Size = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterScreen;

        var choose = new Button { Text = "Open save file...", AutoSize = true };
        choose.Click += ChooseFile;
        _analyse.Click += Analyse;
        _write.Click += Write;
        _preview.Click += Preview;

        var top = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12), ColumnCount = 4 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(choose, 0, 0);
        top.Controls.Add(_path, 1, 0);
        top.Controls.Add(new Label { Text = "Threshold (m):", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        top.Controls.Add(_threshold, 3, 0);
        top.Controls.Add(_analyse, 3, 1);
        top.SetColumnSpan(_analyse, 1);

        ConfigureGrid();
        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(12), ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_status, 0, 0);
        bottom.SetColumnSpan(_status, 2);
        bottom.Controls.Add(_backup, 0, 1);
        bottom.SetColumnSpan(_backup, 2);
        var actions = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Anchor = AnchorStyles.Right };
        actions.Controls.Add(_preview);
        actions.Controls.Add(_write);
        bottom.Controls.Add(actions, 1, 2);
        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(top);
        var busyLayout = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        busyLayout.Controls.Add(_busyText);
        busyLayout.Controls.Add(_busyProgress);
        _busyOverlay.Controls.Add(busyLayout);
        _busyOverlay.Resize += (_, _) => busyLayout.Location = new Point((_busyOverlay.Width - busyLayout.Width) / 2, (_busyOverlay.Height - busyLayout.Height) / 2);
        Controls.Add(_busyOverlay);
    }

    private void ConfigureGrid()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(ZoneRow.Selected), HeaderText = "Remove", Width = 72 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ZoneRow.Code), HeaderText = "Code", Width = 105, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ZoneRow.Id), HeaderText = "Zone ID", Width = 115, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ZoneRow.Position), HeaderText = "Position (X / Y / Z)", Width = 280, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ZoneRow.Content), HeaderText = "Contents", Width = 150, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ZoneRow.Reason), HeaderText = "Reason", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, ReadOnly = true });
        _grid.DataSource = _rows;
    }

    private async void ChooseFile(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "X4 saves (*.xml;*.gz)|*.xml;*.gz|All files (*.*)|*.*", Title = "Choose X4 save file" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ShowBusy("Loading save file...");
            _save = await Task.Run(() => SaveDocument.Load(dialog.FileName));
            _path.Text = dialog.FileName;
            _analyse.Enabled = true;
            _write.Enabled = false;
            _preview.Enabled = false;
            _rows = new BindingList<ZoneRow>();
            _grid.DataSource = _rows;
            _status.Text = _save.IsGzip ? "Compressed save loaded. No changes have been made." : "Uncompressed save loaded. No changes have been made.";
        }
        catch (Exception ex) { ShowError("The save file could not be read as XML.", ex); }
        finally { HideBusy(); }
    }

    private async void Analyse(object? sender, EventArgs e)
    {
        if (_save is null) return;
        try
        {
            ShowBusy("Analysing zones...");
            var records = await Task.Run(() => _save.FindSuspiciousZones((double)_threshold.Value));
            _rows = new BindingList<ZoneRow>(records.Select(x => new ZoneRow(x)).ToList());
            _grid.DataSource = _rows;
            _write.Enabled = _rows.Count > 0;
            _preview.Enabled = _rows.Count > 0;
            _status.Text = _rows.Count == 0 ? "No zones above the threshold were found. The save file was not changed." : $"Found {_rows.Count} suspicious zone(s). Review the selection.";
        }
        catch (Exception ex) { ShowError("Analysis failed.", ex); }
        finally { HideBusy(); }
    }

    private async void Write(object? sender, EventArgs e)
    {
        if (_save is null) return;
        _grid.EndEdit();
        var selected = _rows.Where(x => x.Selected).Select(x => x.Record).ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Select at least one zone.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var description = string.Join(Environment.NewLine, selected.Select(x => $"• {x.Code}  {x.Id}  ({x.Content})"));
        if (MessageBox.Show(this, $"{selected.Count} complete zone block(s) will be removed:\n\n{description}\n\nThe original file will not be overwritten. Continue?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        using var dialog = new SaveFileDialog { Title = "Save cleaned save file", FileName = Path.GetFileName(_save.DefaultOutputPath()), InitialDirectory = Path.GetDirectoryName(_save.SourcePath), Filter = _save.IsGzip ? "GZip save file (*.xml.gz)|*.xml.gz" : "XML save file (*.xml)|*.xml" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            ShowBusy("Writing cleaned save file...");
            await Task.Run(() => _save.WriteCleanedCopy(selected, dialog.FileName, _backup.Checked));
            _status.Text = "Done: a new file was written; the original was left unchanged.";
            MessageBox.Show(this, $"The cleaned save file was created:\n{dialog.FileName}\n\nPlease test the new save separately first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { ShowError("No cleaned save file was written.", ex); }
        finally { HideBusy(); }
    }

    private async void Preview(object? sender, EventArgs e)
    {
        if (_save is null) return;
        _grid.EndEdit();
        var selected = _rows.Where(x => x.Selected).Select(x => x.Record).ToList();
        if (selected.Count == 0) { MessageBox.Show(this, "Select at least one zone.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        try
        {
            ShowBusy("Generating XML preview...");
            var preview = await Task.Run(() => _save.GetRemovalPreview(selected));
            ShowPreviewDialog(preview, selected.Count);
        }
        catch (Exception ex) { ShowError("The XML preview could not be generated.", ex); }
        finally { HideBusy(); }
    }

    private void ShowPreviewDialog(string xml, int zoneCount)
    {
        using var dialog = new Form { Text = $"XML preview — {zoneCount} zone block(s) selected for removal", StartPosition = FormStartPosition.CenterParent, Size = new Size(980, 680), MinimumSize = new Size(700, 430) };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 10f), Text = xml };
        var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true };
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Padding = new Padding(8), FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        footer.Controls.Add(close);
        dialog.Controls.Add(text);
        dialog.Controls.Add(footer);
        dialog.AcceptButton = close;
        dialog.ShowDialog(this);
    }

    private void ShowError(string title, Exception ex) => MessageBox.Show(this, title + "\n\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void ShowBusy(string message)
    {
        _busyText.Text = message;
        _busyOverlay.Visible = true;
        _busyOverlay.BringToFront();
        UseWaitCursor = true;
        _busyOverlay.Refresh();
    }

    private void HideBusy()
    {
        _busyOverlay.Visible = false;
        UseWaitCursor = false;
    }

    private sealed class ZoneRow
    {
        public ZoneRecord Record { get; }
        public bool Selected { get; set; }
        public string Id => Record.Id; public string Code => Record.Code; public string Position => Record.Position; public string Content => Record.Content; public string Reason => Record.Reason;
        public ZoneRow(ZoneRecord record) => Record = record;
    }
}
