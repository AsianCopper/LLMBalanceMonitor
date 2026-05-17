# LLM Balance Monitor

Real-time balance monitoring for multiple LLM API providers.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue?logo=windows)
![.NET 8](https://img.shields.io/badge/.NET-8.0-purple?logo=dotnet)

## Supported Providers

| Provider | Balance | API Endpoint |
|----------|---------|-------------|
| DeepSeek | ¥ balance | `/user/balance` |
| Moonshot / Kimi | ¥ balance | `/v1/users/me/balance` |
| OpenRouter | Usage USD | `/api/v1/auth/key` |
| OpenAI | Credit USD | `/dashboard/billing/credit_grants` |
| Gemini | Connection status | `/v1beta1/models` |

## Features

- Real-time auto-refresh (configurable interval)
- System tray icon with quick access
- Color-coded balance (green = good, yellow = low, red = critical)
- Settings UI for API key management
- Single executable, no installation required

## Usage

1. Download `LLMBalanceMonitor.exe` from Releases
2. Run it — opens to system tray
3. Open the window, go to Settings
4. Enter your API keys for each provider
5. Click Save & Refresh

## Build

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## License

MIT
