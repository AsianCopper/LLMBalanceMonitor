namespace LLMBalanceMonitor;

public class MainForm : Form
{
    private readonly AppConfig _config;
    private readonly BalanceService _service = new();
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private NotifyIcon _trayIcon = null!;
    private List<BalanceInfo> _lastResults = new();
    private DateTime _lastUpdatedAt;
    private BalancePopup? _currentPopup;
    private bool _refreshing;

    public MainForm()
    {
        _config = AppConfig.Load();
        EnsureDefaultProviders();
        _config.Save();

        BuildTrayIcon();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = _config.RefreshIntervalSeconds * 1000 };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();

        Load += async (_, _) => await RefreshAsync();
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

    private void BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Balances", null, (_, _) => ShowPopup());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Refresh Now", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
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
            Icon = IconGenerator.CreateAppIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _trayIcon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPopup(); };
    }

    private void ShowPopup()
    {
        if (_currentPopup is { IsDisposed: false })
        {
            _currentPopup.Activate();
            return;
        }

        _currentPopup = new BalancePopup(_lastResults, _lastUpdatedAt);
        _currentPopup.Show();
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

    protected override void OnShown(EventArgs e)
    {
        Hide();
        base.OnShown(e);
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;

        try
        {
            var results = await _service.FetchAllAsync(_config.Providers);
            _lastResults = results;
            _lastUpdatedAt = DateTime.Now;

            int ok = results.Count(r => r.Status is "OK" or "Connected" or "Free Tier");
            _trayIcon.Text = $"LLM Balance Monitor\n{ok}/{results.Count} OK";
        }
        catch (Exception ex)
        {
            _trayIcon.Text = $"LLM Balance Monitor\nError: {ex.Message}";
        }
        finally
        {
            _refreshing = false;
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
            Icon = IconGenerator.CreateAppIcon(),
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
            Text = "Save && Refresh", Size = new Size(120, 30),
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
