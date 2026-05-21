using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Input;
using WPF_Translator_Screen.Models;

namespace WPF_Translator_Screen.Services.API
{
    public class AzureOutBoundService : IDisposable
    {
        private readonly HttpClient _http;
        private readonly string _key;
        private readonly string _region;
        private readonly string _endpoint;
        public AzureOutBoundService(string key = null, string region = null, string endpoint = null)
        {
            var config = new ConfigurationBuilder()
             .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
             .AddJsonFile("appsettings.json")
             .Build();

            _key = key = config["AzureTranslator:Key"]!;
            _region = region = config["AzureTranslator:Region"]!;
            _endpoint = endpoint = config["AzureTranslator:Endpoint"]!;
            _http = new HttpClient();
        }

        public async Task<string> TranslateTextAsync(string text, string srcLang, string tgtLang, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string route = $"/translate?api-version=3.0&from={srcLang}&to={tgtLang}";
            string textToTranslate = text;
            object[] body = new object[] { new { Text = textToTranslate } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(_endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
                request.Headers.Add("Ocp-Apim-Subscription-Region", _region);

                HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string result = await response.Content.ReadAsStringAsync(cancellationToken);
                result = JsonConvert.DeserializeObject<List<AzureResponse>>(result)?[0].translations?[0].text ?? string.Empty;
                Console.WriteLine(result);
                return result;
            }
        }
        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
