using SharpDX.XInput;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Velopack;
using WPF_Translator_Screen.Models;
using WPF_Translator_Screen.Services;
using WPF_Translator_Screen.Services.API;
using WPF_Translator_Screen.Services.Database;
using WPF_Translator_Screen.Services.OcrModals;
using WPF_Translator_Screen.Views;

namespace WPF_Translator_Screen
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public static readonly RoutedCommand StartSelectionCommand = new RoutedCommand();
        public static readonly RoutedCommand TranslateSelectionCommand = new RoutedCommand();
        private Rect? _selectedArea;
        private XInputNativeModel _gamepad;

        // services
        private readonly OcrService _ocrService;
        private readonly TranslationService _translator;
        private readonly PaddleOcr _paddleOcr;
        private readonly OllamaService _ollamaService;

        // action
        private string _translateProvider = "Ollama";
        private string _ocrProvider = "Paddle";

        // Use the words overlay type (ensure this class exists in Views)
        private TranslationOverlayBox? _singleOverlay;
        private TranslatingIndicatorOverlay? _translatingIndicator;
        private CancellationTokenSource? _translateCts;
        private readonly SemaphoreSlim _selectLock = new(1, 1);
        private readonly SemaphoreSlim _translateLock = new(1, 1);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int HOTKEY_START_SELECTION = 9001;
        private const int HOTKEY_TRANSLATE = 9002;
        private const int HOTKEY_CANCEL = 9003;

        // Modifier keys
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        private IntPtr _hwnd;
        public MainWindow(OcrService ocrService, TranslationService translator, PaddleOcr paddleOcr,
            OllamaService ollamaService)
        {
            _ocrService = ocrService;
            _translator = translator;
            _paddleOcr = paddleOcr;
            _ollamaService = ollamaService;

            InitializeComponent();

            CommandBindings.Add(new CommandBinding(StartSelectionCommand, (s, e) => BeginSelection()));
            CommandBindings.Add(new CommandBinding(TranslateSelectionCommand, async (s, e) => await TranslateSelectedAreaAsync()));

            _gamepad = new XInputNativeModel(
                onStartSelection: () => Dispatcher.Invoke(BeginSelection),
                onTranslate: () => Dispatcher.InvokeAsync(async () => await TranslateSelectedAreaAsync())
            );
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwnd = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(_hwnd);
            source.AddHook(HwndHook);

            // ลงทะเบียน hotkey (ปรับ key combo ตามต้องการ)
            RegisterHotKey(_hwnd, HOTKEY_START_SELECTION, MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.S));
            RegisterHotKey(_hwnd, HOTKEY_TRANSLATE, MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.D));
            RegisterHotKey(_hwnd, HOTKEY_CANCEL, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.Escape));
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();

                if (id == HOTKEY_START_SELECTION)
                {

                    BeginSelection();
                    handled = true;
                }
                else if (id == HOTKEY_TRANSLATE)
                {

                    _ = TranslateSelectedAreaAsync();
                    handled = true;
                }
                else if (id == HOTKEY_CANCEL)
                {
                    CancelCurrentWork();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unregister เมื่อปิด Window
            CancelCurrentWork();
            UnregisterHotKey(_hwnd, HOTKEY_START_SELECTION);
            UnregisterHotKey(_hwnd, HOTKEY_TRANSLATE);
            UnregisterHotKey(_hwnd, HOTKEY_CANCEL);
            base.OnClosed(e);
        }

        private void CancelCurrentWork()
        {
            try { _translateCts?.Cancel(); } catch { }
            try { _translatingIndicator?.Close(); } catch { }
            try { _singleOverlay?.Close(); } catch { }

            _translatingIndicator = null;
            _singleOverlay = null;
            Mouse.OverrideCursor = null;
        }

        private void SettingsBadge_Click(object sender, MouseButtonEventArgs e)
        {
            // sync radio buttons กับค่าที่บันทึกไว้ก่อนเปิด
            RadioAzure.IsChecked = _translateProvider == "Azure";
            RadioGoogle.IsChecked = _translateProvider == "Google";
            RadioOllama.IsChecked = _translateProvider == "Ollama";

            SettingsPopup.IsOpen = true;
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            // Translate provider
            if (RadioAzure.IsChecked == true)
            {
                _translateProvider = "Azure";
            }
            else if (RadioGoogle.IsChecked == true)
            {
                _translateProvider = "Google";
            }
            else if (RadioOllama.IsChecked == true)
            {
                _translateProvider = "Ollama";
            }


            SettingsPopup.IsOpen = false;
        }

        private void CancelSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
        }



        // logic for selection and translation

        private void SelectAreaButton_Click(object sender, RoutedEventArgs e) => BeginSelection();
        private async void TranslateButton_Click(object sender, RoutedEventArgs e) => await TranslateSelectedAreaAsync();

        private void BeginSelection()
        {
            if (!_selectLock.Wait(0)) return;
            try
            {
                this.WindowState = WindowState.Minimized;
                var sel = new SelectionWindow();
                sel.Owner = this;
                var dlg = sel.ShowDialog();
                if (dlg == true && sel.SelectedArea.HasValue)
                {
                    _selectedArea = sel.SelectedArea.Value;
                }
            }
            catch
            {

            }
            finally
            {
                _selectLock.Release();
            }
        }

        private async Task TranslateSelectedAreaAsync()
        {
            if (_selectedArea == null)
            {
                MessageBox.Show("ยังไม่ได้กำหนดพื้นที่ (Select Area ก่อน)", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_translateLock.Wait(0)) return;
            var cts = new CancellationTokenSource();
            _translateCts = cts;
            var cancellationToken = cts.Token;

            ////Mouse.OverrideCursor = Cursors.Wait;
            //this.WindowState = WindowState.Minimized;

            //// ✨ แสดง flash indicator
            TranslatingIndicatorOverlay? indicator = null;
            await Dispatcher.InvokeAsync(() =>
            {
                indicator = new TranslatingIndicatorOverlay();
                _translatingIndicator = indicator;
                indicator.Show();
            });

            try
            {
                var totalSw = Stopwatch.StartNew();
                cancellationToken.ThrowIfCancellationRequested();

                // 1) capture image bytes
                var bytes = CaptureAreaToPngBytes(_selectedArea.Value);
                Debug.WriteLine($"Image size: {_selectedArea.Value.Width}x{_selectedArea.Value.Height}, bytes: {bytes.Length}");
                cancellationToken.ThrowIfCancellationRequested();

                // 2) OCR -> words + boxes (in image pixel coordinates)
                var srlLanguage = MapOCRLang(SelectedSourceLanguage());
                List<OcrService.WordBox> words = await _ocrService.RecognizeWithOpenCvAsync(bytes, srlLanguage, _ocrProvider, cancellationToken)
                    .WaitAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"OCR extaction: {words}");
                cancellationToken.ThrowIfCancellationRequested();

                if (words == null || words.Count == 0)
                {
                    MessageBox.Show("ไม่พบคำในพื้นที่ที่เลือก", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                words = words
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"OCR mapping word: {words}");

                string fullSentence = string.Empty;
                if (MapOCRLang(SelectedSourceLanguage()) == "japan")
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
                        var srcLang = MapTranslateLang(SelectedSourceLanguage());
                        var tgtLang = MapTranslateLang(SelectedTargetLanguage()) ?? "th";

                        var translateSw = Stopwatch.StartNew();
                        translatedSentence = await _translator.TranslateTextAsync(
                            fullSentence,
                            srcLang: srcLang,
                            tgtLang: tgtLang,
                            _translateProvider,
                            cancellationToken).WaitAsync(cancellationToken);
                        translateSw.Stop();
                        Debug.WriteLine($"Translation completed in {translateSw.ElapsedMilliseconds} ms using {_translateProvider}.");
                        Debug.WriteLine($"translatedSentence after TranslateTextAsync: {translatedSentence}");
                        cancellationToken.ThrowIfCancellationRequested();

                        var sqlite = new SQLiteService();

                        // ตอน OCR + แปลเสร็จ → เก็บลง SQLite ก่อน
                        sqlite.Insert(new PendingRecord
                        {
                            RawInput = fullSentence,
                            TranslateOutput = translatedSentence,
                            SourceLanguage = SelectedSourceLanguage(),
                            TargetLanguage = SelectedTargetLanguage(),
                            AppSource = "translate_with_screen",
                            ContextName = ""
                        });
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        translatedSentence = "Oop! Something went wrong during translation";
                    }
                }
                //translatedSentence = fullSentence;
                if (string.IsNullOrWhiteSpace(translatedSentence))
                {
                    translatedSentence = fullSentence;
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var screenRect = BuildTextOverlayRect(_selectedArea.Value, words);

                    var overlaySw = Stopwatch.StartNew();
                    try { _singleOverlay?.Close(); } catch { }
                    _singleOverlay = new TranslationOverlayBox(translatedSentence, screenRect, clickThrough: false, duration: TimeSpan.FromSeconds(60));
                    _singleOverlay.Show();
                    overlaySw.Stop();
                    Debug.WriteLine($"Overlay completed in {overlaySw.ElapsedMilliseconds} ms.");
                }

                totalSw.Stop();
                Debug.WriteLine($"TranslateSelectedAreaAsync total time: {totalSw.ElapsedMilliseconds} ms.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Debug.WriteLine("TranslateSelectedAreaAsync canceled.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // ✨ ปิด indicator เมื่อเสร็จ
                await Dispatcher.InvokeAsync(() =>
                {
                    try { indicator?.Close(); } catch { }
                    if (ReferenceEquals(_translatingIndicator, indicator))
                    {
                        _translatingIndicator = null;
                    }
                });

                if (ReferenceEquals(_translateCts, cts))
                {
                    _translateCts = null;
                }

                _translateLock.Release();
                Mouse.OverrideCursor = null;
                cts.Dispose();
            }
        }

        private static string BuildRpgJapaneseText(List<OcrService.WordBox> words)
        {
            if (words == null || words.Count == 0)
                return string.Empty;

            var ordered = words
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .OrderBy(w => w.Box.Top)
                .ThenBy(w => w.Box.Left)
                .ToList();

            var lines = new List<List<OcrService.WordBox>>();
            double avgHeight = ordered.Count > 0 ? ordered.Average(w => w.Box.Height) : 12;
            int lineTolerance = Math.Max(12, (int)Math.Round(avgHeight / 2.0));

            foreach (var word in ordered)
            {
                var targetLine = lines.FirstOrDefault(line =>
                    Math.Abs(line.Average(x => x.Box.Top) - word.Box.Top) <= lineTolerance);

                if (targetLine == null)
                {
                    lines.Add(new List<OcrService.WordBox> { word });
                }
                else
                {
                    targetLine.Add(word);
                }
            }

            var lineTexts = lines
                .Select(line => string.Concat(line.OrderBy(w => w.Box.Left).Select(w => w.Text)))
                .Select(CleanRpgJapaneseLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            return string.Join(Environment.NewLine, lineTexts);
        }

        private static string CleanRpgJapaneseLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = Regex.Replace(text, @"\s+", "");

            text = text.Replace("『", "「")
                       .Replace("』", "」")
                       .Replace("【", "")
                       .Replace("】", "")
                       .Replace("⋯", "…")
                       .Replace("---", "―");

            // เก็บ marker แบบเกม RPG ไว้
            text = Regex.Replace(text, @"^[◆◇★]+", m => m.Value);

            // ตัดขยะ OCR ที่เป็นอังกฤษ/สัญลักษณ์ลอยๆ
            text = Regex.Replace(text, @"(?<![A-Za-z])[A-Za-z_][A-Za-z0-9_\-'.:]{2,}", "");
            text = Regex.Replace(text, @"[|_/~`]+", "");
            text = Regex.Replace(text, @"[^\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}A-Za-z0-9。、！？「」『』（）()…―ー・、，．\-\s◆◇★]", "");

            // ถ้าทั้งบรรทัดแทบไม่ใช่ญี่ปุ่น ให้ทิ้ง
            int jpCount = text.Count(c =>
                (c >= 0x3040 && c <= 0x309F) ||   // Hiragana
                (c >= 0x30A0 && c <= 0x30FF) ||   // Katakana
                (c >= 0x4E00 && c <= 0x9FFF));    // Kanji

            if (jpCount < 4)
                return string.Empty;

            return text.Trim();
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
#if DEBUG
                    var debugDir = @"C:\debug_captures";
                    Directory.CreateDirectory(debugDir); // สร้าง folder ถ้าไม่มี
                    var debugPath = System.IO.Path.Combine(debugDir, $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    bmp.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                    Debug.WriteLine($"[DEBUG] Saved capture to: {debugPath}");
#endif
                    return ms.ToArray();
                }
            }
        }

        private string? SelectedSourceLanguage()
        {
            try
            {
                var cb = this.FindName("SourceLanguageCombo") as System.Windows.Controls.ComboBox;
                return (cb?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
            }
            catch { return null; }
        }

        private string? SelectedTargetLanguage()
        {
            try
            {
                var cb = this.FindName("TargetLanguageCombo") as System.Windows.Controls.ComboBox;
                return (cb?.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString();
            }
            catch { return null; }
        }

        private string MapOCRLang(string? uiLanguage)
        {
            return uiLanguage?.ToLower() switch
            {
                "english" => "en",
                "japanese" => "japan",
                "chinese" => "ch",
                "chinese_cht" => "chinese_cht",
                "korean" => "korean",
                _ => "en"
            };
        }

        private string MapTranslateLang(string? uiLanguage)
        {
            return uiLanguage?.ToLower() switch
            {
                "english" => "en",
                "japanese" => "ja",
                "chinese" => "ch",
                "korean" => "ko",
                "thai" => "th",
                _ => "en"
            };
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await UpdateMyApp();
        }

        private static async Task UpdateMyApp()
        {
            var mgr = new UpdateManager("https://your-server.com/releases");
            // หรือใช้ GitHub: new UpdateManager("https://github.com/youruser/yourrepo")

            // ตรวจสอบ version ใหม่
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null)
                return; // ไม่มี update

            // ดาวน์โหลด
            await mgr.DownloadUpdatesAsync(newVersion);

            // ติดตั้งและ restart app
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
    }
}
