using System.Text.Json;

namespace LLMBalanceMonitor;

public class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly BalanceService _service = new();
    private readonly System.Windows.Forms.Timer _refreshTimer;

    private NotifyIcon _trayIcon = null!;
    private DataGridView _grid = null!;
    private Label _lblStatus = null!;
    private Button _btnRefresh = null!;
    private Button _btnSettings = null!;

    private bool _refreshing;

    public MainForm()
    {
        _config = AppConfig.Load();
        EnsureDefaultProviders();
        _config.Save();

        BuildForm();
        BuildTrayIcon();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = _config.RefreshIntervalSeconds * 1000 };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();

        // Initial refresh
        this.Load += async (_, _) => await RefreshAsync();
    }

    private void EnsureDefaultProviders()
    {
        if (_config.Providers.Count > 0) return;

        _config.Providers = new List<ProviderConfig>
        {
            new() { Name = "DeepSeek", ApiBase = "https://api.deepseek.com" },
            new() { Name = "Kimi", ApiBase = "https://api.moonshot.cn" },
            new() { Name = "OpenRouter", ApiBase = "https://openrouter.ai" },
            new() { Name = "OpenAI", ApiBase = "https://api.openai.com", Enabled = false },
            new() { Name = "Gemini", ApiBase = "https://generativelanguage.googleapis.com", Enabled = false },
        };
    }

    private void BuildForm()
    {
        Text = "LLM Balance Monitor";
        Size = new Size(700, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;
        MinimumSize = new Size(500, 300);

        // Grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BorderStyle = BorderStyle.None,
            BackgroundColor = Color.White,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            RowTemplate = new DataGridViewRow { Height = 40 },
            Font = new Font("Segoe UI", 10),
        };

        _grid.Columns.Add("Provider", "Provider");
        _grid.Columns.Add("Balance", "Balance");
        _grid.Columns.Add("Usage", "Usage");
        _grid.Columns.Add("Status", "Status");
        _grid.Columns.Add("Checked", "Last Checked");

        _grid.Columns["Provider"]!.Width = 120;
        _grid.Columns["Balance"]!.Width = 120;
        _grid.Columns["Usage"]!.Width = 100;
        _grid.Columns["Status"]!.Width = 100;
        _grid.Columns["Checked"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        _grid.Columns["Balance"]!.DefaultCellStyle.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        _grid.Columns["Provider"]!.DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _grid.Columns["Balance"]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        _grid.Columns["Checked"]!.DefaultCellStyle.ForeColor = Color.Gray;

        // Bottom panel
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(10, 8, 10, 8) };

        _btnRefresh = new Button { Text = "Refresh", Size = new Size(90, 28), Location = new Point(10, 8) };
        _btnRefresh.Click += async (_, _) => await RefreshAsync();

        _btnSettings = new Button { Text = "Settings", Size = new Size(90, 28), Location = new Point(110, 8) };
        _btnSettings.Click += (_, _) => ShowSettings();

        _lblStatus = new Label
        {
            Location = new Point(220, 12), Size = new Size(450, 22),
            Text = "Ready", TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray,
        };

        bottomPanel.Controls.AddRange(new Control[] { _btnRefresh, _btnSettings, _lblStatus });

        Controls.AddRange(new Control[] { _grid, bottomPanel });
    }

    private void BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Refresh Now", null, async (_, _) => await RefreshAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            _refreshTimer.Stop();
            Application.Exit();
        });

        _trayIcon = new NotifyIcon
        {
            Text = "LLM Balance Monitor",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        try
        {
            _btnRefresh.Enabled = false;
            _lblStatus.Text = "Refreshing...";

            var results = await _service.FetchAllAsync(_config.Providers);
            UpdateGrid(results);

            _lblStatus.Text = $"Last updated: {DateTime.Now:HH:mm:ss}  |  " +
                $"{results.Count(r => r.Status == "OK" || r.Status == "Connected" || r.Status == "Free Tier")}/{results.Count} OK  |  " +
                $"Next refresh in {_config.RefreshIntervalSeconds}s";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
        }
        finally
        {
            _btnRefresh.Enabled = true;
            _refreshing = false;
        }
    }

    private void UpdateGrid(List<BalanceInfo> results)
    {
        _grid.Rows.Clear();

        foreach (var r in results)
        {
            int idx = _grid.Rows.Add();
            var row = _grid.Rows[idx];

            row.Cells["Provider"].Value = r.Provider;
            row.Cells["Status"].Value = r.Status;
            row.Cells["Checked"].Value = r.CheckedAt.ToString("HH:mm:ss");

            if (r.Balance.HasValue)
            {
                string prefix = r.Currency == "CNY" ? "¥" : "$";
                row.Cells["Balance"].Value = $"{prefix}{r.Balance:F2}";
            }
            else
            {
                row.Cells["Balance"].Value = r.Provider == "Gemini" ? "✓" : "-";
            }

            row.Cells["Usage"].Value = r.Usage.HasValue
                ? $"${r.Usage:F4}"
                : (r.Provider == "OpenRouter" ? "-" : "-");

            // Color coding
            string status = r.Status;
            if (status == "OK" || status == "Connected" || status == "Free Tier")
            {
                row.DefaultCellStyle.ForeColor = Color.FromArgb(0, 160, 80); // Green
                if (r.Balance.HasValue && r.Balance < 10)
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 160, 0); // Yellow - low
                if (r.Balance.HasValue && r.Balance < 1)
                    row.DefaultCellStyle.ForeColor = Color.FromArgb(200, 50, 50); // Red - critical
            }
            else if (r.Status == "Error")
            {
                row.DefaultCellStyle.ForeColor = Color.FromArgb(200, 50, 50);
                row.Cells["Status"].Value = "Error";
                row.Cells["Balance"].Value = "!";
            }

            // Set tooltip with raw data
            row.Cells["Provider"].ToolTipText = !string.IsNullOrEmpty(r.Raw) ? r.Raw : r.Error ?? "";
        }
    }

    private void ShowSettings()
    {
        var form = new Form
        {
            Text = "Settings",
            Size = new Size(550, 400),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            Icon = SystemIcons.Application,
        };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };

        foreach (var p in _config.Providers)
        {
            var box = new GroupBox { Text = p.Name, Width = 500, Height = 70, Margin = new Padding(0, 0, 0, 8) };

            var lbl = new Label { Text = "API Key:", Location = new Point(15, 25), Size = new Size(55, 22) };
            var txt = new TextBox
            {
                Text = p.ApiKey,
                Location = new Point(75, 23),
                Size = new Size(340, 22),
                UseSystemPasswordChar = p.ApiKey.Length > 0,
            };
            var toggle = new Button { Text = p.ApiKey.Length > 0 ? "Show" : "", Location = new Point(420, 22), Size = new Size(60, 23) };
            toggle.Click += (_, _) =>
            {
                txt.UseSystemPasswordChar = !txt.UseSystemPasswordChar;
                toggle.Text = txt.UseSystemPasswordChar ? "Show" : "Hide";
            };

            var chk = new CheckBox { Text = "Enabled", Location = new Point(75, 48), Size = new Size(80, 20), Checked = p.Enabled };
            chk.CheckedChanged += (_, _) => p.Enabled = chk.Checked;

            txt.TextChanged += (_, _) => p.ApiKey = txt.Text;

            box.Controls.AddRange(new Control[] { lbl, txt, toggle, chk });
            panel.Controls.Add(box);
        }

        var btnSave = new Button
        {
            Text = "Save & Refresh", Size = new Size(120, 30),
            Margin = new Padding(190, 5, 0, 0),
        };
        btnSave.Click += async (_, _) =>
        {
            foreach (var box in panel.Controls.OfType<GroupBox>())
            {
                var provider = _config.Providers.FirstOrDefault(p => p.Name == box.Text);
                if (provider != null)
                {
                    var txt = box.Controls.OfType<TextBox>().FirstOrDefault();
                    if (txt != null) provider.ApiKey = txt.Text;
                }
            }
            _config.Save();
            form.Close();
            await RefreshAsync();
        };
        panel.Controls.Add(btnSave);

        form.Controls.Add(panel);
        form.ShowDialog(this);
    }
}
