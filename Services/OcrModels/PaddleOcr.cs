using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WPF_Translator_Screen.Models;
using static Google.Apis.Requests.BatchRequest;


namespace WPF_Translator_Screen.Services.OcrModals
{
    public class PaddleOcr : IDisposable
    {
        public record WordBox(string Text, Rectangle Box);

        private readonly HttpClient _http;
        private readonly string _serverUrl;

        public PaddleOcr(string serverUrl = null)
        {
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var config = new ConfigurationBuilder()
                 .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                 .AddJsonFile("appsettings.json")
                 .Build();

            _serverUrl = serverUrl = config["PaddleOcr:url"]!;
        }

        public async Task<List<WordBox>> RecognizeWithOpenCvAsync(byte[] imagePng, string tessLang = "jpn")
        {


            // ส่ง image เป็น base64
            string base64Image = Convert.ToBase64String(imagePng);

            var payload = JsonSerializer.Serialize(new
            {
                image = Convert.ToBase64String(imagePng),
                lang = tessLang
            });

            string jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var overlaySw = Stopwatch.StartNew();
            var response = await _http.PostAsync($"{_serverUrl}/ocr", content);

            response.EnsureSuccessStatusCode();
            overlaySw.Stop();
            Debug.WriteLine($"[OCR] PaddleOCR request done in {overlaySw.ElapsedMilliseconds} ms, Lang={tessLang}");
            var json = await response.Content.ReadAsStringAsync();

            var totalSw = Stopwatch.StartNew();
            PaddleOcrResponse result = JsonSerializer.Deserialize<PaddleOcrResponse>(json);

            var words = new List<WordBox>();

            if (result?.words != null)
            {
                foreach (var w in result.words)
                {
                    if (string.IsNullOrWhiteSpace(w.text))
                        continue;

                    // กรอง confidence ต่ำออก
                    if (w.confidence < 0.5f)
                        continue;

                    var box = new Rectangle(w.box.x, w.box.y, w.box.w, w.box.h);
                    words.Add(new WordBox(w.text.Trim(), box));
                }
            }

            totalSw.Stop();
            Debug.WriteLine($"[OCR] PaddleOCR done in {totalSw.ElapsedMilliseconds} ms, Words={words.Count}, Lang={tessLang}");

            return words;
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
