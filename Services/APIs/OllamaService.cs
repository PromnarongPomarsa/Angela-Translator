using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace WPF_Translator_Screen.Services.API
{
    public class OllamaService
    {
        private readonly HttpClient _http = new();
        private readonly string _serverUrl;
        private readonly IConfiguration _configuration;


        public OllamaService(string serverUrl = null)
        {
            var config = new ConfigurationBuilder()
             .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
             .AddJsonFile("appsettings.json")
             .Build();
            _serverUrl = serverUrl = config["Ollama:url"];
            _configuration = config;
        }

        public async Task<string> RefineTranslationAsync(
            string originalText,
            string srcLang,
            string targetLanguage,
            string model = "gemma3:4b",
            CancellationToken cancellationToken = default) // gemma3:4b
        {
            cancellationToken.ThrowIfCancellationRequested();
            originalText = Regex.Replace(originalText, @"[「」ゝ]", "");

            srcLang = $"{MapGemma3Lang(srcLang)} ({srcLang})";
            targetLanguage = $"{MapGemma3Lang(targetLanguage)} ({targetLanguage})";

            var prompt = $"Translate into natural {targetLanguage}:\n<text>\n{originalText}\n</text>";

            var payload = new
            {
                model = model,
                system = $"You are a master of {srcLang} language translator. Fix OCR errors silently. Reply with translation only.Translate EVERY sentence completely, do not skip or summarize any part. Preserve the original sentence structure and paragraph format. Do not add or remove any information. Translate word by word meaning, not summarize",
                //prompt = $"Translate into natural {targetLanguage}:\n<text>\n{originalText}\n</text>",
                prompt = prompt,
                keep_alive = -1,
                stream = false,
                options = new
                {
                    temperature = 0.1,
                    top_p = 0.9,
                    //num_predict = 300   // จำกัด output token ไม่ให้คิดนานเกิน
                }
            };
            var overlaySw = Stopwatch.StartNew();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_serverUrl}/api/generate", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Ollama error {(int)response.StatusCode}: {errorBody}");
            }
            response.EnsureSuccessStatusCode();
            overlaySw.Stop();
            Debug.WriteLine($"[Ollama] Request done in {overlaySw.ElapsedMilliseconds} ms, Model={model}");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);

            return doc.RootElement.GetProperty("response").GetString()?.Trim() ?? originalText;
        }

        // GPU Mode — ส่งภาพตรงให้ Vision model
        public async Task<string> TranslateFromImageAsync(
            byte[] imageBytes,
            string targetLanguage = "Thai",
            string model = "qwen2.5vl:3b",
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string base64Image = Convert.ToBase64String(imageBytes);

            string prompt = $@"You are a game subtitle translator.
                    Read ALL Japanese text visible in this game screenshot.
                    Translate everything to {targetLanguage}.
                    Output translated text only.
                    Keep proper nouns (item names, skill names) in original language.
                    Do NOT explain anything.";

            var payload = new
            {
                model = model,
                prompt = prompt,
                images = new[] { base64Image },  // <-- ส่วนสำคัญ
                stream = false,
                options = new
                {
                    temperature = 0.1,
                    top_p = 0.9,
                    num_predict = 150
                }
            };

            var sw = Stopwatch.StartNew();
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _http.PostAsync($"{_serverUrl}/api/generate", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            sw.Stop();
            Debug.WriteLine($"[Ollama Vision] Done in {sw.ElapsedMilliseconds} ms");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("response").GetString()?.Trim() ?? "";
        }
        private string MapGemma3Lang(string? isoLanguage)
        {
            return isoLanguage?.ToLower().Trim() switch
            {
                "en" => "English",
                "ja" => "Japanese",
                "ch" => "Chinese", 
                "ko" => "Korean",
                "th" => "Thai",
                _ => "English" 
            };
        }
    }
}
