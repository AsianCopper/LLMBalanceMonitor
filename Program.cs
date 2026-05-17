using System.Text.Json;

namespace LLMBalanceMonitor;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(true, "LLMBalanceMonitor_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("LLM Balance Monitor is already running!\nCheck the system tray icon.",
                "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Run(new MainForm());
    }
}

public class AppConfig
{
    public int RefreshIntervalSeconds { get; set; } = 60;
    public List<ProviderConfig> Providers { get; set; } = new();

    public static string ConfigPath
    {
        get
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LLMBalanceMonitor");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch { }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}

public class ProviderConfig
{
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiBase { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public class BalanceInfo
{
    public string Provider { get; set; } = "";
    public decimal? Balance { get; set; }
    public decimal? Usage { get; set; }
    public string Currency { get; set; } = "CNY";
    public string Status { get; set; } = "OK";
    public string? Error { get; set; }
    public string? Raw { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.Now;
}
