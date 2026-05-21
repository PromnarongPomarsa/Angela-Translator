using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace WPF_Translator_Screen.Services
{
    public static class LocalRuntimeBootstrap
    {
        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static async Task EnsureAsync(CancellationToken cancellationToken = default)
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var config = new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            await EnsureOcrAsync(baseDirectory, config, cancellationToken);
            await EnsureOllamaModelAsync(config, cancellationToken);
        }

        private static async Task EnsureOcrAsync(string baseDirectory, IConfiguration config, CancellationToken cancellationToken)
        {
            var ocrUrl = config["PaddleOcr:url"] ?? "http://127.0.0.1:5000";
            if (await IsHttpOkAsync($"{ocrUrl.TrimEnd('/')}/health", cancellationToken))
                return;

            var ocrDirectory = Path.Combine(baseDirectory, "ocr");
            var serverScript = Path.Combine(ocrDirectory, "paddle_ocr_server.py");
            var venvPython = Path.Combine(ocrDirectory, ".venv", "Scripts", "python.exe");

            if (!File.Exists(serverScript))
                return;

            if (!File.Exists(venvPython))
            {
                var installScript = Path.Combine(ocrDirectory, "install_ocr_env.bat");
                if (File.Exists(installScript))
                    StartProcess(installScript, ocrDirectory);

                return;
            }

            StartProcess(venvPython, ocrDirectory, "paddle_ocr_server.py");
        }

        private static async Task EnsureOllamaModelAsync(IConfiguration config, CancellationToken cancellationToken)
        {
            var ollamaUrl = (config["Ollama:url"] ?? "http://127.0.0.1:11434").TrimEnd('/');
            var model = config["Ollama:CpuModel"] ?? "gemma3:4b";

            if (!await IsHttpOkAsync($"{ollamaUrl}/api/tags", cancellationToken))
                TryStartOllamaApp();

            if (!await IsHttpOkAsync($"{ollamaUrl}/api/tags", cancellationToken))
                return;

            if (await HasOllamaModelAsync(ollamaUrl, model, cancellationToken))
                return;

            var payload = JsonSerializer.Serialize(new
            {
                name = model,
                stream = false
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            try
            {
                await Http.PostAsync($"{ollamaUrl}/api/pull", content, cancellationToken);
            }
            catch
            {
                // The app can still run with online/API translators even if local model setup fails.
            }
        }

        private static async Task<bool> HasOllamaModelAsync(string ollamaUrl, string model, CancellationToken cancellationToken)
        {
            try
            {
                var body = await Http.GetStringAsync($"{ollamaUrl}/api/tags", cancellationToken);
                using var doc = JsonDocument.Parse(body);

                if (!doc.RootElement.TryGetProperty("models", out var models))
                    return false;

                foreach (var item in models.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var name) &&
                        string.Equals(name.GetString(), model, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static async Task<bool> IsHttpOkAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await Http.GetAsync(url, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static void TryStartOllamaApp()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new[]
            {
                Path.Combine(localAppData, "Programs", "Ollama", "ollama app.exe"),
                Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    StartProcess(candidate, Path.GetDirectoryName(candidate)!);
                    return;
                }
            }
        }

        private static void StartProcess(string fileName, string workingDirectory, string? arguments = null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch
            {
                // Keep startup resilient; OCR/Ollama calls will surface user-visible errors if needed.
            }
        }
    }
}
