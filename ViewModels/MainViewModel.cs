using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WPF_Translator_Screen.Models;
using WPF_Translator_Screen.Services;
using WPF_Translator_Screen.Services.API;
using WPF_Translator_Screen.Services.OcrModals;
using WPF_Translator_Screen.Views;

namespace WPF_Translator_Screen.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => !_isExecuting && (_canExecute == null || _canExecute(parameter));

        public async void Execute(object parameter)
        {
            _isExecuting = true;
            try { await _execute(parameter); }
            finally { _isExecuting = false; }
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }

    public class MainViewModel : BaseViewModel
    {
        private readonly OcrService _ocrService;
        private readonly TranslationService _translator;
        private readonly PaddleOcr _paddleOcr;
        private readonly OllamaService _ollamaService;

        public ICommand BeginSelectionCommand { get; }
        public ICommand TranslateCommand { get; }

        private Rect? _selectedArea;
        public Rect? SelectedArea
        {
            get => _selectedArea;
            set => SetProperty(ref _selectedArea, value);
        }

        private string _translateProvider = "Ollama";
        public string TranslateProvider
        {
            get => _translateProvider;
            set => SetProperty(ref _translateProvider, value);
        }

        private string _ocrProvider = "Paddle";
        public string OcrProvider
        {
            get => _ocrProvider;
            set => SetProperty(ref _ocrProvider, value);
        }

        private bool _isSettingsOpen;
        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set => SetProperty(ref _isSettingsOpen, value);
        }

        private readonly SemaphoreSlim _selectLock = new(1, 1);
        private readonly SemaphoreSlim _translateLock = new(1, 1);

        public MainViewModel(OcrService ocrService, TranslationService translator, PaddleOcr paddleOcr, OllamaService ollamaService)
        {
            _ocrService = ocrService;
            _translator = translator;
            _paddleOcr = paddleOcr;
            _ollamaService = ollamaService;

            BeginSelectionCommand = new RelayCommand(async (obj) => await BeginSelectionAsync());
            TranslateCommand = new AsyncRelayCommand(async (obj) => await TranslateSelectedAreaAsync());
        }

        public async Task BeginSelectionAsync()
        {
            if (!_selectLock.Wait(0)) return;
            try
            {
                // The actual window showing is handled by the View calling this ViewModel
                // In a full MVVM, we'd use a NavigationService or DialogService
            }
            finally
            {
                _selectLock.Release();
            }
        }

        public async Task TranslateSelectedAreaAsync()
        {
            if (SelectedArea == null)
            {
                MessageBox.Show("ยังไม่ได้กำหนดพื้นที่ (Select Area ก่อน)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_translateLock.Wait(0)) return;

            try
            {
                var totalSw = Stopwatch.StartNew();
                var bytes = CaptureAreaToPngBytes(SelectedArea.Value);

                var srlLanguage = MapOCRLang(CurrentSourceLanguage);
                List<OcrService.WordBox> words = await _ocrService.RecognizeWithOpenCvAsync(bytes, srlLanguage, OcrProvider);

                if (words == null || words.Count == 0)
                {
                    MessageBox.Show("ไม่พบคำในพื้นที่ที่เลือก", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                words = words
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .Where(w => !Regex.IsMatch(w.Text, @"^[A-Za-z0-9_\-'.:]+$"))
                    .ToList();

                string fullSentence = string.Empty;

                if (MapOCRLang(CurrentSourceLanguage) == "jpn")
                {
                    fullSentence = BuildRpgJapaneseText(words);
                }
                else
                {
                    fullSentence = string.Join(" ", words.Select(w => w.Text)).Trim();
                }

                string? translatedSentence = null;
                if (!string.IsNullOrWhiteSpace(fullSentence))
                {
                    try
                    {
                        var srcLang = MapTranslateLang(CurrentSourceLanguage);
                        var tgtLang = MapTranslateLang(CurrentTargetLanguage) ?? "th";

                        translatedSentence = await _translator.TranslateTextAsync(
                            fullSentence,
                            srcLang: srcLang,
                            tgtLang: tgtLang,
                            TranslateProvider);
                    }
                    catch
                    {
                        translatedSentence = "Oop! Something went wrong during translation";
                    }
                }

                if (string.IsNullOrWhiteSpace(translatedSentence))
                {
                    translatedSentence = fullSentence;
                }
                else
                {
                    var screenRect = BuildTextOverlayRect(SelectedArea.Value, words);
                    TranslationResult = translatedSentence;
                    TranslationRect = screenRect;
                }

                totalSw.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _translateLock.Release();
            }
        }

        public string? TranslationResult { get; private set; }
        public Rect? TranslationRect { get; private set; }
        public string CurrentSourceLanguage { get; set; } = "English";
        public string CurrentTargetLanguage { get; set; } = "Thai";

        private byte[] CaptureAreaToPngBytes(Rect area)
        {
            int w = Math.Max(1, (int)area.Width);
            int h = Math.Max(1, (int)area.Height);
            using (var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen((int)area.Left, (int)area.Top, 0, 0, new System.Drawing.Size(w, h), System.Drawing.CopyPixelOperation.SourceCopy);
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        private string MapOCRLang(string? uiLanguage)
        {
            return uiLanguage?.ToLower() switch
            {
                "english" => "en",
                "japanese" => "japan",
                "chinese" => "ch",
                "korean" => "korean",
                _ => "eng"
            };
        }

        private string MapTranslateLang(string? uiLanguage)
        {
            return uiLanguage?.ToLower() switch
            {
                "english" => "en",
                "thai" => "th",
                "japanese" => "ja",
                _ => "en"
            };
        }

        private static string BuildRpgJapaneseText(List<OcrService.WordBox> words)
        {
            if (words == null || words.Count == 0) return string.Empty;
            var ordered = words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderBy(w => w.Box.Top).ThenBy(w => w.Box.Left).ToList();
            var lines = new List<List<OcrService.WordBox>>();
            double avgHeight = ordered.Count > 0 ? ordered.Average(w => w.Box.Height) : 12;
            int lineTolerance = Math.Max(12, (int)Math.Round(avgHeight / 2.0));

            foreach (var word in ordered)
            {
                var targetLine = lines.FirstOrDefault(line => Math.Abs(line.Average(x => x.Box.Top) - word.Box.Top) <= lineTolerance);
                if (targetLine == null) lines.Add(new List<OcrService.WordBox> { word });
                else targetLine.Add(word);
            }

            var lineTexts = lines
                .Select(line => string.Concat(line.OrderBy(w => w.Box.Left).Select(w => w.Text)))
                .Select(CleanRpgJapaneseLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return string.Join(Environment.NewLine, lineTexts);
        }

        private static Rect BuildTextOverlayRect(Rect selectedArea, IReadOnlyCollection<OcrService.WordBox> words)
        {
            var boxes = words
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .Select(w => w.Box)
                .Where(box => box.Width > 0 && box.Height > 0)
                .ToList();

            if (boxes.Count == 0)
                return selectedArea;

            double left = Math.Max(0, boxes.Min(box => box.Left));
            double top = Math.Max(0, boxes.Min(box => box.Top));
            double right = Math.Min(selectedArea.Width, boxes.Max(box => box.Right));
            double bottom = Math.Min(selectedArea.Height, boxes.Max(box => box.Bottom));

            if (right <= left || bottom <= top)
                return selectedArea;

            const double padding = 2;
            left = Math.Max(0, left - padding);
            top = Math.Max(0, top - padding);
            right = Math.Min(selectedArea.Width, right + padding);
            bottom = Math.Min(selectedArea.Height, bottom + padding);

            return new Rect(
                selectedArea.Left + left,
                selectedArea.Top + top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
        }

        private static string CleanRpgJapaneseLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            text = Regex.Replace(text, @"\s+", "");
            text = text.Replace("『", "「").Replace("』", "」").Replace("【", "").Replace("】", "").Replace("⋯", "…").Replace("---", "―");
            text = Regex.Replace(text, @"^[◆◇★]+", m => m.Value);
            text = Regex.Replace(text, @"(?<![A-Za-z])[A-Za-z_][A-Za-z0-9_\-'.:]{2,}", "");
            text = Regex.Replace(text, @"[|_/~`]+", "");
            text = Regex.Replace(text, @"[^\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}A-Za-z0-9。、！？「」『』（）()…―ー・、，．\-\s◆◇★]", "");
            int jpCount = text.Count(c => (c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF) || (c >= 0x4E00 && c <= 0x9FFF));
            if (jpCount < 4) return string.Empty;
            return text.Trim();
        }
    }
}
