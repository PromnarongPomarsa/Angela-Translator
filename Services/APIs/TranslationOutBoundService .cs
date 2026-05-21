using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WPF_Translator_Screen.Models;
using WPF_Translator_Screen.Services.Database;

namespace WPF_Translator_Screen.Services.APIs
{
    public class TranslationOutBoundService : IDisposable
    {
        private readonly SQLiteService _sqlite;
        private readonly HttpClient _httpClient = new();
        private readonly string _endpoint;
        private readonly int _userId;
        private readonly int _appSource;
        private readonly TimeSpan _syncInterval;
        private CancellationTokenSource? _cts;
        private Task? _syncTask;
        public TranslationOutBoundService(SQLiteService sqlite)
        {
            _sqlite = sqlite;

            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

            _endpoint = "https://ai-translation-api-production.up.railway.app/api/Translation/batch";
            _userId = int.TryParse(config["TranslationOutBound:UserId"], out var userId) ? userId : 0;
            _appSource = int.TryParse(config["TranslationOutBound:AppSource"], out var appSource) ? appSource : 0;
            _syncInterval = int.TryParse(config["TranslationOutBound:IntervalMinutes"], out var intervalMinutes) && intervalMinutes > 0
                ? TimeSpan.FromMinutes(intervalMinutes)
                : TimeSpan.FromMinutes(5);
        }

        public void Start()
        {
            if (_syncTask is { IsCompleted: false })
                return;

            _cts = new CancellationTokenSource();
            _syncTask = Task.Run(() => RunSyncLoopAsync(_cts.Token));
        }

        private async Task RunSyncLoopAsync(CancellationToken cancellationToken)
        {
            AppendSyncLog($"Translation outbound sync started. Interval={_syncInterval} Endpoint={_endpoint}");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await SendPendingAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    AppendSyncLog($"Sync loop exception: {ex}");
                }

                try
                {
                    await Task.Delay(_syncInterval, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            AppendSyncLog("Translation outbound sync stopped.");
        }

        public async Task SendPendingAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_endpoint))
                    return;

                var records = _sqlite.GetPending();
                if (records.Count == 0)
                {
                    Debug.WriteLine("Sync skipped: no pending records.");
                    return;
                }

                var payload = new TranslationUploadRequest
                {
                    UserId = _userId,
                    AppSource = _appSource,
                    Sessions = records
                        .GroupBy(x => new
                        {
                            x.SourceLanguage,
                            x.TargetLanguage,
                            x.ContextName
                        })
                        .Select(group => new TranslationSessionDto
                        {
                            SourceLanguage = group.Key.SourceLanguage,
                            TargetLanguage = group.Key.TargetLanguage,
                            ContextName = group.Key.ContextName ?? "",
                            VideoFilename = "",
                            VideoDuration = 0,
                            Records = group
                            .DistinctBy(x => x.RawInput) // กรองเอาเฉพาะตัวที่ RawInput ไม่ซ้ำกัน (เอาตัวแรกที่เจอ)
                            .Select(x => new TranslationRecordDto
                            {
                                RawInput = x.RawInput,
                                TranslateOutput = x.TranslateOutput
                            }).ToList()
                        })
                        .ToList()
                };

                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                AppendSyncLog($"Sending {records.Count} pending record(s).");
                var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _sqlite.MarkAsSent(records.Select(x => x.Id).ToList());
                    _sqlite.DeleteSent();
                    AppendSyncLog($"Sync succeeded: {records.Count} record(s) sent.");
                }
                else
                {
                    AppendSyncLog($"Sync failed: {(int)response.StatusCode} {response.ReasonPhrase}");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppendSyncLog($"Sync exception: {ex}");
            }
        }

        private static void AppendSyncLog(string message)
        {
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TranslateApp");

                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "sync.log");
                File.AppendAllText(path, $"{DateTime.Now:O} {message}{Environment.NewLine}");
                Debug.WriteLine(message);
            }
            catch
            {
                Debug.WriteLine(message);
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _httpClient.Dispose();
        }
    }

}
