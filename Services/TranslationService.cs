using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WPF_Translator_Screen.Models;
using WPF_Translator_Screen.Services.API;
using WPF_Translator_Screen.Services.APIs;

namespace WPF_Translator_Screen.Services
{
    public class TranslationService : IDisposable
    {
        private readonly HttpClient _http;
        private readonly AzureOutBoundService _azureOutBoundService;
        private readonly GoogleOutBoundService _googleOutBoundService;
        private readonly OllamaService _ollamaService; // not used yet, for refining translation
        private string _result;

        public TranslationService(AzureOutBoundService azureOutBoundService,
            GoogleOutBoundService googleOutBoundService,
            OllamaService ollamaService)
        {
            
            _azureOutBoundService = azureOutBoundService;
            _googleOutBoundService = googleOutBoundService;
            _ollamaService = ollamaService;
            _result = string.Empty;
        }

        public async Task<string> TranslateTextAsync(string text, string srcLang, string tgtLang, string model, CancellationToken cancellationToken = default)
        {
            string respText = "";
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(tgtLang) || string.IsNullOrEmpty(model))
                return string.Empty;

            cancellationToken.ThrowIfCancellationRequested();

            if (model.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                _result = await _azureOutBoundService.TranslateTextAsync(text, srcLang, tgtLang, cancellationToken);
            }
            else if (model.Equals("Google", StringComparison.OrdinalIgnoreCase))
            {
                _result = await _googleOutBoundService.TranslateAsync(text, srcLang, tgtLang, cancellationToken);
            }
            else if (model.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            {
                srcLang = MapLang(srcLang);
                tgtLang = MapLang(tgtLang);
                _result = await _ollamaService.RefineTranslationAsync(text, srcLang, tgtLang, cancellationToken: cancellationToken);
            }
            return _result;
        }

        private string MapLang(string? uiLanguage)
        {
            return uiLanguage?.ToLower() switch
            {
                "en" => "english",
                "th" => "thai",
                "ja" => "japanese",
                _ => "english"
            };
        }


        public void Dispose()
        {
            // We only dispose internal HttpClient if we created it here.
            _http?.Dispose();
        }
    }
}
