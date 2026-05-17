using System.Text.Json;

namespace LLMBalanceMonitor;

public class BalanceService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<List<BalanceInfo>> FetchAllAsync(List<ProviderConfig> providers)
    {
        var tasks = new List<Task<BalanceInfo?>>();
        foreach (var p in providers.Where(p => p.Enabled && !string.IsNullOrEmpty(p.ApiKey)))
        {
            tasks.Add(FetchProviderAsync(p));
        }

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).ToList()!;
    }

    private async Task<BalanceInfo?> FetchProviderAsync(ProviderConfig p)
    {
        try
        {
            return p.Name.ToLowerInvariant() switch
            {
                "deepseek" => await FetchDeepSeekAsync(p),
                "moonshot" or "kimi" => await FetchMoonshotAsync(p),
                "openrouter" => await FetchOpenRouterAsync(p),
                "openai" => await FetchOpenAIAsync(p),
                "gemini" => await FetchGeminiAsync(p),
                _ => new BalanceInfo
                {
                    Provider = p.Name,
                    Status = "Unknown",
                    Error = $"Unsupported provider: {p.Name}",
                    CheckedAt = DateTime.Now,
                }
            };
        }
        catch (Exception ex)
        {
            return new BalanceInfo
            {
                Provider = p.Name,
                Status = "Error",
                Error = ex.Message,
                CheckedAt = DateTime.Now,
            };
        }
    }

    // DeepSeek: GET /user/balance
    // {"balance_infos":[{"total_balance":"100.00","granted_balance":"0.00","topped_up_balance":"100.00"}],"is_available":true}
    private async Task<BalanceInfo> FetchDeepSeekAsync(ProviderConfig p)
    {
        var baseUrl = string.IsNullOrEmpty(p.ApiBase) ? "https://api.deepseek.com" : p.ApiBase.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/user/balance");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.ApiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var infos = root.GetProperty("balance_infos").EnumerateArray().FirstOrDefault();
        decimal totalBalance = 0;
        if (infos.ValueKind != JsonValueKind.Undefined)
        {
            totalBalance = decimal.Parse(infos.GetProperty("total_balance").GetString() ?? "0");
        }

        return new BalanceInfo
        {
            Provider = "DeepSeek",
            Balance = totalBalance,
            Currency = "CNY",
            Status = root.GetProperty("is_available").GetBoolean() ? "OK" : "Unavailable",
            Raw = json,
            CheckedAt = DateTime.Now,
        };
    }

    // Moonshot/Kimi: GET /v1/users/me/balance
    // {"data":{"available_balance":99.5,"voucher_balance":0,"cash_balance":99.5},"status":true}
    private async Task<BalanceInfo> FetchMoonshotAsync(ProviderConfig p)
    {
        var baseUrl = string.IsNullOrEmpty(p.ApiBase) ? "https://api.moonshot.cn" : p.ApiBase.TrimEnd('/');
        baseUrl = baseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/v1")) baseUrl = baseUrl[..^3];

        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/users/me/balance");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.ApiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var data = root.GetProperty("data");

        decimal? available = null;
        if (data.TryGetProperty("available_balance", out var ab) && ab.ValueKind == JsonValueKind.Number)
            available = ab.GetDecimal();

        return new BalanceInfo
        {
            Provider = "Kimi",
            Balance = available,
            Currency = "CNY",
            Status = root.TryGetProperty("status", out var st) && st.GetBoolean() ? "OK" : "Unavailable",
            Raw = json,
            CheckedAt = DateTime.Now,
        };
    }

    // OpenRouter: GET /api/v1/auth/key
    // {"data":{"usage":0.0034,"limit_remaining":null,"is_free_tier":true}}
    private async Task<BalanceInfo> FetchOpenRouterAsync(ProviderConfig p)
    {
        var baseUrl = string.IsNullOrEmpty(p.ApiBase) ? "https://openrouter.ai" : p.ApiBase.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/auth/key");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.ApiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");

        decimal usage = 0;
        if (data.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Number)
            usage = u.GetDecimal();

        decimal? limitRemaining = null;
        if (data.TryGetProperty("limit_remaining", out var lr) && lr.ValueKind == JsonValueKind.Number)
            limitRemaining = lr.GetDecimal();

        bool isFree = data.TryGetProperty("is_free_tier", out var fr) && fr.GetBoolean();

        return new BalanceInfo
        {
            Provider = "OpenRouter",
            Balance = limitRemaining,
            Usage = usage,
            Currency = "USD",
            Status = isFree ? "Free Tier" : "OK",
            Raw = json,
            CheckedAt = DateTime.Now,
        };
    }

    // OpenAI: GET https://api.openai.com/v1/dashboard/billing/credit_grants
    // Requires organization-level API key
    private async Task<BalanceInfo> FetchOpenAIAsync(ProviderConfig p)
    {
        var baseUrl = string.IsNullOrEmpty(p.ApiBase) ? "https://api.openai.com" : p.ApiBase.TrimEnd('/');
        var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/organization/credits");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.ApiKey);

        var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // Fallback: try the /dashboard/billing/credit_grants endpoint
            var req2 = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v1/dashboard/billing/credit_grants");
            req2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", p.ApiKey);
            var resp2 = await _http.SendAsync(req2);
            resp2.EnsureSuccessStatusCode();
            json = await resp2.Content.ReadAsStringAsync();
        }

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Parse based on endpoint
        decimal? totalGranted = null;
        if (root.TryGetProperty("total_granted", out var tg)) totalGranted = tg.GetDecimal();
        decimal? totalUsed = null;
        if (root.TryGetProperty("total_used", out var tu)) totalUsed = tu.GetDecimal();
        decimal? totalAvailable = null;
        if (root.TryGetProperty("total_available", out var ta)) totalAvailable = ta.GetDecimal();

        return new BalanceInfo
        {
            Provider = "OpenAI",
            Balance = totalAvailable ?? totalGranted,
            Usage = totalUsed,
            Currency = "USD",
            Status = "OK",
            Raw = json,
            CheckedAt = DateTime.Now,
        };
    }

    // Gemini: no balance API, check key validity by listing models
    private async Task<BalanceInfo> FetchGeminiAsync(ProviderConfig p)
    {
        var baseUrl = string.IsNullOrEmpty(p.ApiBase)
            ? "https://generativelanguage.googleapis.com"
            : p.ApiBase.TrimEnd('/');
        var url = $"{baseUrl}/v1beta1/models?key={p.ApiKey}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        int modelCount = 0;
        if (doc.RootElement.TryGetProperty("models", out var models))
            modelCount = models.GetArrayLength();

        return new BalanceInfo
        {
            Provider = "Gemini",
            Status = "Connected",
            Balance = null,
            Currency = "USD",
            Usage = null,
            Raw = $"{modelCount} models available",
            CheckedAt = DateTime.Now,
        };
    }
}
