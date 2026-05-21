using Google.Cloud.Translation.V2;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace WPF_Translator_Screen.Services.API
{
    public class GoogleOutBoundService : IDisposable
    {
        private readonly string _key;
        private readonly TranslationClient _client;
        private readonly Dictionary<string, string> _translateCache = new();

        public GoogleOutBoundService(string key = null)
        {
            var config = new ConfigurationBuilder()
                 .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                 .AddJsonFile("appsettings.json")
                 .Build();

            _key = key = config["GoogleTranslator:Key"];
            _client = TranslationClient.CreateFromApiKey(_key);
        }

        public Task<string> TranslateAsync(string text, string srcLang, string tgtLang, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                // ถ้าเคยแปลแล้ว ใช้ผลเดิมเลย ไม่ยิง API
                if (_translateCache.TryGetValue(text, out var cached))
                {
                    Debug.WriteLine("[Cache] HIT — ไม่ใช้ API");
                    return cached;
                }

                var response = _client.TranslateText(
                    text: text,
                    targetLanguage: tgtLang,
                    sourceLanguage: srcLang
                );

                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine(response.TranslatedText);
                return response.TranslatedText;
            }, cancellationToken);
        }

        public void Dispose()
        {
        }
    }
}
