using Microsoft.Extensions.Configuration;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tesseract;
using WPF_Translator_Screen.Services.OcrModals;
using WPF_Translator_Screen.Services.OcrModels;

namespace WPF_Translator_Screen.Services
{
    public partial class OcrService : IDisposable
    {
        private const float FallbackConfidenceThreshold = 0.72f;
        private readonly object _engineLock = new();
        private readonly Dictionary<string, TesseractEngine> _engines = new(StringComparer.OrdinalIgnoreCase);

        // service
        private readonly PaddleOcr _paddleOcr;
        private readonly TesseractOcr _tesseractOcr;

        public record WordBox(string Text, Rectangle Box);

        public OcrService(PaddleOcr paddleOcr, TesseractOcr tesseractOcr)
        {
            _paddleOcr = paddleOcr;
            _tesseractOcr = tesseractOcr;
        }

        public Task<List<WordBox>> RecognizeWithOpenCvAsync(byte[] imagePng, string tessLang = "jpn", string ocrProvider = "Paddle", CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ocrProvider.Equals("Paddle", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _paddleOcr.RecognizeWithOpenCvAsync(imagePng, tessLang);
                    cancellationToken.ThrowIfCancellationRequested();

                    return result
                        .Select(w => new WordBox(w.Text, w.Box))
                        .ToList();
                }
                else
                {
                    throw new ArgumentException($"Unsupported OCR provider: {ocrProvider}");
                }
            }, cancellationToken);
        }

        public void Dispose()
        {
            lock (_engineLock)
            {
                foreach (var engine in _engines.Values)
                {
                    engine.Dispose();
                }

                _engines.Clear();
            }
        }
    }
}
