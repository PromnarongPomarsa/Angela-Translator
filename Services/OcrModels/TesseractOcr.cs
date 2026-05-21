using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Tesseract;
using System.Drawing;
using System.IO;

namespace WPF_Translator_Screen.Services.OcrModels
{
    public class TesseractOcr : IDisposable
    {

        private const float FallbackConfidenceThreshold = 0.72f;
        private readonly object _engineLock = new();
        private readonly Dictionary<string, TesseractEngine> _engines = new(StringComparer.OrdinalIgnoreCase);

        public record WordBox(string Text, Rectangle Box);

        public Task<List<WordBox>> RecognizeWithOpenCvAsync(byte[] imagePng, string tessLang = "jpn")
        {
            return Task.Run(() =>
            {
                var totalSw = Stopwatch.StartNew();
                using var ms = new MemoryStream(imagePng);
                using var src = Mat.FromStream(ms, ImreadModes.Color);

                if (src.Empty())
                    throw new InvalidOperationException("Input image is empty");

                int origWidth = src.Width;
                int origHeight = src.Height;

                using var gray = new Mat();
                Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

                using var normalized = new Mat();
                Cv2.Normalize(gray, normalized, 0, 255, NormTypes.MinMax);

                using var blurred = new Mat();
                Cv2.GaussianBlur(normalized, blurred, new OpenCvSharp.Size(3, 3), 0);

                double scale = 1.0;
                using var work = new Mat();
                if (blurred.Width < 1400)
                {
                    scale = 2.0;
                    Cv2.Resize(
                        blurred,
                        work,
                        new OpenCvSharp.Size(),
                        scale,
                        scale,
                        InterpolationFlags.Cubic);
                }
                else
                {
                    blurred.CopyTo(work);
                }

                using var otsu = new Mat();
                Cv2.Threshold(work, otsu, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                double meanVal = Cv2.Mean(otsu).Val0;
                if (meanVal < 127)
                    Cv2.BitwiseNot(otsu, otsu);

                SaveDebugImagesIfEnabled(work, otsu);

                List<WordBox> bestResult = new();
                float bestConf = -1f;

                var primaryAttempts = new (Mat Mat, PageSegMode Mode, string Label)[]
                {
                    (work, PageSegMode.SingleBlock, "gray/single-block"),
                    (otsu, PageSegMode.SingleBlock, "otsu/single-block"),
                };

                var fallbackAttempts = new (Mat Mat, PageSegMode Mode, string Label)[]
                {
                    (work, PageSegMode.SingleLine, "gray/single-line"),
                };

                var engine = GetOrCreateEngine(tessLang);
                RunAttempts(primaryAttempts, engine, scale, origWidth, origHeight, ref bestConf, ref bestResult);

                if (bestResult.Count == 0 || bestConf < FallbackConfidenceThreshold)
                {
                    RunAttempts(fallbackAttempts, engine, scale, origWidth, origHeight, ref bestConf, ref bestResult);
                }

                totalSw.Stop();
                Debug.WriteLine($"OCR total time: {totalSw.ElapsedMilliseconds} ms. BestConf={bestConf:0.000}, Words={bestResult.Count}, Lang={tessLang}");
                return bestResult;
            });
        }

        private void RunAttempts(
            IEnumerable<(Mat Mat, PageSegMode Mode, string Label)> attempts,
            TesseractEngine engine,
            double scale,
            int origWidth,
            int origHeight,
            ref float bestConf,
            ref List<WordBox> bestResult)
        {
            foreach (var attempt in attempts)
            {
                try
                {
                    var attemptSw = Stopwatch.StartNew();
                    var pngBytes = attempt.Mat.ImEncode(".png");
                    using var pix = Pix.LoadFromMemory(pngBytes);

                    List<WordBox> candidate = new();
                    float conf;
                    string text;

                    lock (_engineLock)
                    {
                        using var page = engine.Process(pix, attempt.Mode);
                        conf = page.GetMeanConfidence();
                        text = page.GetText() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(text) || conf <= bestConf)
                        {
                            attemptSw.Stop();
                            Debug.WriteLine($"OCR attempt {attempt.Label} finished in {attemptSw.ElapsedMilliseconds} ms. Conf={conf:0.000}, improved=False");
                            continue;
                        }

                        using var iter = page.GetIterator();
                        iter.Begin();

                        do
                        {
                            if (!iter.IsAtBeginningOf(PageIteratorLevel.Word))
                                continue;

                            if (!iter.TryGetBoundingBox(PageIteratorLevel.Word, out var rect))
                                continue;

                            string txt = iter.GetText(PageIteratorLevel.Word) ?? string.Empty;
                            txt = txt.Trim();

                            if (string.IsNullOrWhiteSpace(txt))
                                continue;

                            int x = (int)Math.Round(rect.X1 / scale);
                            int y = (int)Math.Round(rect.Y1 / scale);
                            int w = (int)Math.Round((rect.X2 - rect.X1) / scale);
                            int h = (int)Math.Round((rect.Y2 - rect.Y1) / scale);

                            x = Math.Clamp(x, 0, origWidth - 1);
                            y = Math.Clamp(y, 0, origHeight - 1);

                            if (x + w > origWidth) w = origWidth - x;
                            if (y + h > origHeight) h = origHeight - y;

                            if (w <= 0 || h <= 0)
                                continue;

                            candidate.Add(new WordBox(txt, new Rectangle(x, y, w, h)));
                        }
                        while (iter.Next(PageIteratorLevel.Word));
                    }

                    bestConf = conf;
                    bestResult = candidate;
                    attemptSw.Stop();
                    Debug.WriteLine($"OCR attempt {attempt.Label} finished in {attemptSw.ElapsedMilliseconds} ms. Conf={conf:0.000}, Words={candidate.Count}, improved=True");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OCR attempt {attempt.Label} failed: {ex}");
                }
            }
        }

        private TesseractEngine GetOrCreateEngine(string tessLang)
        {
            lock (_engineLock)
            {
                if (_engines.TryGetValue(tessLang, out var cachedEngine))
                {
                    return cachedEngine;
                }

                var engine = new TesseractEngine(
                    Path.Combine(AppContext.BaseDirectory, "Ocr", "tessdata"),
                    tessLang,
                    EngineMode.Default);

                engine.SetVariable("preserve_interword_spaces", "1");
                engine.SetVariable("user_defined_dpi", "300");
                _engines[tessLang] = engine;
                return engine;
            }
        }

        private static void SaveDebugImagesIfEnabled(Mat work, Mat otsu)
        {
            if (!Debugger.IsAttached || !string.Equals(Environment.GetEnvironmentVariable("OCR_SAVE_DEBUG_IMAGES"), "1", StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var debugGray = Path.Combine(Path.GetTempPath(), $"ocr_gray_{stamp}.png");
                var debugOtsu = Path.Combine(Path.GetTempPath(), $"ocr_otsu_{stamp}.png");
                Cv2.ImWrite(debugGray, work);
                Cv2.ImWrite(debugOtsu, otsu);

                Debug.WriteLine($"debugGray: {debugGray}");
                Debug.WriteLine($"debugOtsu: {debugOtsu}");
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            // Dispose of any resources if necessary
        }
    }
}
