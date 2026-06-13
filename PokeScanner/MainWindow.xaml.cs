using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Tesseract;

namespace PokeScanner;

public partial class MainWindow : System.Windows.Window
{
    private static readonly string LogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "pokescanner_debug.log");

    private static void LogInfo(string msg)
    {
        System.Console.Out.WriteLine(msg);
        try { File.AppendAllText(LogPath, msg + Environment.NewLine); } catch { }
    }

    private VideoCapture? _capture;
    private bool _isRunning;
    private Mat? _latestFrame;
    private readonly object _frameLock = new();
    private readonly object _captureLock = new();
    private TesseractEngine? _ocrEngine;
    private int _currentCameraIndex = -1;
    private System.Windows.Threading.DispatcherTimer? _scanAnimTimer;
    private int _scanAnimDots;
    private static string? _llmKey;
    private CancellationTokenSource? _scanCts;
    private System.Windows.Threading.DispatcherTimer? _autoTimer;
    private bool _autoCooldown;
    private Mat? _prevRoiGray;
    private int _stableTicks;
    private string _selectedModel = "gemma3:12b";
    private static readonly string[] AvailableModels =
    {
        "qwen3-vl:30b-a3b-instruct-q4_K_M",
        "gemma3:12b",
        "gemma4:12b",
    };

    public MainWindow()
    {
        InitializeComponent();
        try { File.WriteAllText(LogPath, $"--- PokeScanner Debug Log {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---\n"); } catch { }
        LogInfo("[PokeScanner] Log file: " + LogPath);
        LoadEnv();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void LoadEnv()
    {
        var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        if (!File.Exists(envPath))
        {
            envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            if (!File.Exists(envPath))
            {
                LogInfo("[PokeScanner] .env not found, LLM key won't be loaded");
                return;
            }
        }
        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
            var eq = trimmed.IndexOf('=');
            if (eq < 0) continue;
            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim();
            if (key == "LITELLM_MASTER_KEY" || key == "LITELLM_SKELETON_KEY")
            {
                value = value.Trim().Trim('"').Trim('\'');
                if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    value = value["Bearer ".Length..];
                _llmKey = value;
                LogInfo($"[PokeScanner] Loaded {key} ({_llmKey[..Math.Min(8, _llmKey.Length)]}...)");
                break;
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LogInfo("[PokeScanner] Window loaded");

        // Test: show a solid green image to verify Image control works
        using var testMat = new Mat(480, 640, MatType.CV_8UC3, new Scalar(0, 255, 0));
        var testBmp = testMat.ToBitmapSource();
        CameraPreview.Source = testBmp;
        LogInfo($"[PokeScanner] Test image set: {testBmp.Width}x{testBmp.Height}");

        PopulateCameraList();
        PopulateModelSelector();
    }

    private void PopulateModelSelector()
    {
        ModelSelector.Items.Clear();
        foreach (var m in AvailableModels)
            ModelSelector.Items.Add(m);
        ModelSelector.SelectedItem = _selectedModel;
    }

    private void ModelSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModelSelector.SelectedItem is string model)
            _selectedModel = model;
    }

    private void AutoButton_Checked(object sender, RoutedEventArgs e)
    {
        _autoCooldown = false;
        _autoTimer = new System.Windows.Threading.DispatcherTimer();
        _autoTimer.Interval = TimeSpan.FromMilliseconds(500);
        _autoTimer.Tick += AutoTimer_Tick;
        _autoTimer.Start();
        LogInfo("[PokeScanner] Auto-scan enabled");
    }

    private void AutoButton_Unchecked(object sender, RoutedEventArgs e)
    {
        _autoTimer?.Stop();
        _autoTimer = null;
        _autoCooldown = false;
        LogInfo("[PokeScanner] Auto-scan disabled");
    }

    private void AutoTimer_Tick(object? sender, EventArgs e)
    {
        Mat? frame;
        lock (_frameLock) { frame = _latestFrame?.Clone(); }
        if (frame == null) return;

        using (frame)
        {
            using var roi = GetRoiCrop(frame);
            if (roi == null) return;

            using var gray = new Mat();
            Cv2.CvtColor(roi, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.MeanStdDev(gray, out _, out var stddev);
            var std = stddev.Val0;

            if (std > 30)
            {
                if (_autoCooldown || !CaptureButton.IsEnabled) return;

                if (_prevRoiGray != null && _prevRoiGray.Size() == gray.Size())
                {
                    using var diff = new Mat();
                    Cv2.Absdiff(gray, _prevRoiGray, diff);
                    double meanDiff = Cv2.Mean(diff).Val0;

                    if (meanDiff < 5)
                    {
                        _stableTicks++;
                        if (_stableTicks >= 4)
                        {
                            LogInfo($"[PokeScanner] Auto: card stable 2s, triggering (stddev={std:F1})");
                            _autoCooldown = true;
                            _stableTicks = 0;
                            _prevRoiGray?.Dispose();
                            _prevRoiGray = null;
                            Dispatcher.InvokeAsync(() => CaptureButton_Click(null, null));
                        }
                    }
                    else
                    {
                        _stableTicks = 0;
                    }
                }

                _prevRoiGray?.Dispose();
                _prevRoiGray = gray.Clone();
            }
            else
            {
                // Card removed — reset everything
                _autoCooldown = false;
                _stableTicks = 0;
                _prevRoiGray?.Dispose();
                _prevRoiGray = null;
            }
        }
    }

    private static readonly Guid CameraClass = new("{ca3e7ab9-b4c3-4ae6-8251-579ef933890f}");
    private static readonly Guid ImageClass = new("{6bdd1fc6-810f-11d0-bec7-08002be2092f}");

    private record CameraRegEntry(string Name, Guid ClassGuid, int Order);

    private async void PopulateCameraList()
    {
        CameraSelector.Items.Clear();

        try
        {
            var devices = await Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(
                Windows.Devices.Enumeration.DeviceClass.VideoCapture);

            LogInfo($"[PokeScanner] WinRT found {devices.Count} cameras:");
            for (int i = 0; i < devices.Count; i++)
            {
                var name = string.IsNullOrWhiteSpace(devices[i].Name) ? $"Camera {i}" : devices[i].Name;
                LogInfo($"[PokeScanner]   [{i}] name={name} id={devices[i].Id}");
                CameraSelector.Items.Add(new CameraItem(i, name));
            }
        }
        catch (Exception ex)
        {
            LogInfo($"[PokeScanner] WinRT enumeration failed: {ex.Message}, falling back to registry");
            PopulateCameraListFallback();
        }

        CameraSelector.SelectedIndex = -1;
    }

    private void PopulateCameraListFallback()
    {
        CameraSelector.Items.Clear();
        var found = false;

        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<CameraRegEntry>();
            int order = 0;
            var devClass = "{65E8773D-8F56-11D0-A3B9-00A0C9223196}";
            using var root = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\DeviceClasses\" + devClass);
            if (root != null)
            {
                foreach (var sub in root.GetSubKeyNames())
                {
                    using var sk = root.OpenSubKey(sub);
                    var di = sk?.GetValue("DeviceInstance")?.ToString();
                    if (string.IsNullOrEmpty(di)) continue;

                    using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\" + di);
                    if (enumKey == null) continue;

                    var classGuid = enumKey.GetValue("ClassGUID")?.ToString();
                    var guid = Guid.TryParse(classGuid, out var g) ? g : Guid.Empty;
                    if (guid != CameraClass && guid != ImageClass)
                    {
                        LogInfo($"[PokeScanner] SKIP: {di} ClassGUID={classGuid}");
                        continue;
                    }

                    var rawName = enumKey.GetValue("FriendlyName")?.ToString()
                        ?? enumKey.GetValue("DeviceDesc")?.ToString()
                        ?? "";
                    var name = ParseIndirectString(rawName) ?? rawName;
                    LogInfo($"[PokeScanner] REG: {di} ClassGUID={classGuid} raw={rawName} parsed={name}");

                    if (string.IsNullOrEmpty(name)) continue;
                    if (seen.Add(name))
                    {
                        entries.Add(new CameraRegEntry(name, guid, order++));
                        LogInfo($"[PokeScanner] ADD: {name} as {guid} order={order - 1}");
                    }
                    else
                    {
                        LogInfo($"[PokeScanner] DUP: {name} skipped");
                    }
                }
            }

            // MSMF orders Image-class cameras before Camera-class cameras.
            // Within Camera-class, MSMF enumerates in reverse registry order
            // (higher instance path first, e.g. ROOT\CAMERA\0001 before ROOT\CAMERA\0000).
            entries.Sort((a, b) =>
            {
                var pa = a.ClassGuid == ImageClass ? 0 : 1;
                var pb = b.ClassGuid == ImageClass ? 0 : 1;
                int cmp = pa.CompareTo(pb);
                if (cmp != 0) return cmp;
                // Same class: reverse order for Camera (MSMF), original order for Image
                return a.ClassGuid == CameraClass
                    ? b.Order.CompareTo(a.Order)
                    : a.Order.CompareTo(b.Order);
            });

            LogInfo($"[PokeScanner] Camera entries after sort: [{string.Join(", ", entries.Select(e => $"{e.Name}(cls={e.ClassGuid},ord={e.Order})"))}]");

            for (int i = 0; i < entries.Count; i++)
            {
                CameraSelector.Items.Add(new CameraItem(i, entries[i].Name));
                found = true;
            }
        }
        catch (Exception ex)
        {
            LogInfo($"[PokeScanner] Camera enumeration failed: {ex.Message}");
        }

        if (!found)
        {
            for (int i = 0; i < 10; i++)
                CameraSelector.Items.Add(new CameraItem(i, $"Camera {i}"));
        }

        CameraSelector.SelectedIndex = -1;
    }

    private static string? ParseIndirectString(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        // Format: @oemNN.inf,%key%;DisplayName
        var idx = raw.LastIndexOf(';');
        if (idx >= 0 && idx < raw.Length - 1)
            return raw[(idx + 1)..];
        return raw;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        StopCapture();
        _currentCameraIndex = -1;
        PopulateCameraList();
    }

    private void CameraSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CameraSelector.SelectedItem is not CameraItem cam) return;
        if (cam.Index == _currentCameraIndex) return;

        LogInfo($"[PokeScanner] Switching to camera {cam.Index}");
        StopCapture();
        var prevIndex = _currentCameraIndex;
        _currentCameraIndex = cam.Index;
        if (!StartCapture(_currentCameraIndex))
        {
            _currentCameraIndex = prevIndex;
            StatusText.Text = $"Camera {cam.Index} failed — try another";
            StatusText.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private bool StartCapture(int index)
    {
        Environment.SetEnvironmentVariable("OPENCV_VIDEOIO_MSMF_ENABLE_HW_TRANSFORMS", "0");

        // Show loading indicator while probing camera
        StatusText.Text = $"Loading camera {index}...";
        StatusText.Foreground = System.Windows.Media.Brushes.Gold;

        var backends = new[] { VideoCaptureAPIs.MSMF, VideoCaptureAPIs.DSHOW };
        VideoCapture? cap = null;
        string usedApi = "none";

        foreach (var api in backends)
        {
            LogInfo($"[PokeScanner] Opening camera {index} with {api}");
            try
            {
                var test = new VideoCapture(index, api);
                if (!test.IsOpened())
                {
                    LogInfo($"[PokeScanner] {api}: not opened");
                    test.Dispose();
                    continue;
                }

                test.Set(VideoCaptureProperties.FrameWidth, 1280);
                test.Set(VideoCaptureProperties.FrameHeight, 720);

                using var frame = new Mat();
                if (test.Read(frame) && !frame.Empty())
                {
                    LogInfo($"[PokeScanner] {api}: frame grabbed OK");
                    cap = test;
                    usedApi = api.ToString();
                    break;
                }

                LogInfo($"[PokeScanner] {api}: opened but no frame");
                test.Release();
                test.Dispose();
            }
            catch (Exception ex)
            {
                LogInfo($"[PokeScanner] {api}: {ex.Message}");
            }
        }

        if (cap == null)
        {
            LogInfo($"[PokeScanner] Trying default (auto-detect) for camera {index}");
            try
            {
                var test = new VideoCapture(index);
                if (test.IsOpened())
                {
                    test.Set(VideoCaptureProperties.FrameWidth, 1280);
                    test.Set(VideoCaptureProperties.FrameHeight, 720);

                    using var frame = new Mat();
                    if (test.Read(frame) && !frame.Empty())
                    {
                        cap = test;
                        usedApi = "auto";
                    }
                    else
                    {
                        test.Release();
                        test.Dispose();
                    }
                }
                else
                {
                    test.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogInfo($"[PokeScanner] auto: {ex.Message}");
            }
        }

        if (cap == null)
        {
            _currentCameraIndex = -1;
            LogInfo($"[PokeScanner] Camera {index} failed with all backends");
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Camera {index} failed — try another";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            });
            return false;
        }

        LogInfo($"[PokeScanner] Using backend {usedApi} for camera {index}");
        _capture = cap;
        _isRunning = true;
        Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text = "Ready";
            StatusText.Foreground = System.Windows.Media.Brushes.Gray;
        });
        _ = Task.Run(CaptureLoop);
        return true;
    }

    private void StopCapture()
    {
        _isRunning = false;
        lock (_captureLock)
        {
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }
        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }
    }

    private void CaptureLoop()
    {
        int frameCount = 0;
        int failCount = 0;
        LogInfo("[PokeScanner] Capture loop running");

        while (_isRunning)
        {
            using var temp = new Mat();
            try
            {
                bool readOk;
                lock (_captureLock) { readOk = _capture?.Read(temp) ?? false; }
                frameCount++;

                if (!readOk)
                {
                    failCount++;
                    if (failCount > 60)
                    {
                        LogInfo($"[PokeScanner] Too many read failures ({failCount}), aborting capture loop");
                        break;
                    }
                    if (frameCount % 60 == 0)
                        LogInfo($"[PokeScanner] Read returned false (frame {frameCount})");
                    Task.Delay(10).Wait();
                    continue;
                }

                if (temp.Empty())
                {
                    failCount++;
                    if (failCount > 60)
                    {
                        LogInfo($"[PokeScanner] Too many empty frames ({failCount}), aborting capture loop");
                        break;
                    }
                    Task.Delay(10).Wait();
                    continue;
                }

                failCount = 0;

                var display = temp.Clone();

                lock (_frameLock)
                {
                    _latestFrame?.Dispose();
                    _latestFrame = temp.Clone();
                }

                try
                {
                    int stride = (int)display.Step();
                    int height = display.Height;
                    var buffer = new byte[stride * height];
                    Marshal.Copy(display.Data, buffer, 0, buffer.Length);

                    var bitmap = BitmapSource.Create(
                        display.Width, height, 96, 96,
                        System.Windows.Media.PixelFormats.Bgr24, null,
                        buffer, stride);
                    bitmap.Freeze();

                    Dispatcher.Invoke(() =>
                    {
                        CameraPreview.Source = bitmap;
                    });
                }
                catch (Exception ex)
                {
                    LogInfo($"[PokeScanner] Display error: {ex.Message}");
                }
                finally
                {
                    display.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogInfo($"[PokeScanner] Capture error: {ex.Message}");
                Task.Delay(100).Wait();
            }
        }

        LogInfo($"[PokeScanner] Capture loop exited after {frameCount} frames");

        if (_isRunning)
        {
            // Loop exited unexpectedly (not from StopCapture)
            _isRunning = false;
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "Camera disconnected — switch to another camera";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            });
        }
    }

    private Mat? GetRoiCrop(Mat frame)
    {
        var topLeft = RoiOverlay.TranslatePoint(new System.Windows.Point(0, 0), CameraPreview);
        var bottomRight = RoiOverlay.TranslatePoint(
            new System.Windows.Point(RoiOverlay.Width, RoiOverlay.Height), CameraPreview);
        var bounds = GetImageBounds();
        if (bounds == null) return null;

        double scaleX = frame.Width / (double)bounds.Value.Width;
        double scaleY = frame.Height / (double)bounds.Value.Height;

        int cropX = (int)((topLeft.X - bounds.Value.X) * scaleX);
        int cropY = (int)((topLeft.Y - bounds.Value.Y) * scaleY);
        int cropW = (int)((bottomRight.X - topLeft.X) * scaleX);
        int cropH = (int)((bottomRight.Y - topLeft.Y) * scaleY);

        cropX = Math.Max(0, cropX); cropY = Math.Max(0, cropY);
        cropW = Math.Min(cropW, frame.Width - cropX);
        cropH = Math.Min(cropH, frame.Height - cropY);
        if (cropW <= 0 || cropH <= 0) return null;

        using var cropped = new Mat(frame, new OpenCvSharp.Rect(cropX, cropY, cropW, cropH));
        return cropped.Clone();
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        Mat? frame;
        lock (_frameLock)
        {
            frame = _latestFrame?.Clone();
        }
        if (frame == null) return;

        var card = GetRoiCrop(frame);
        frame.Dispose();

        if (card == null) return;
        CroppedPreview.Source = card.ToBitmapSource();

        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        _ = RunOcrAsync(card, _scanCts.Token);
    }

    private async Task RunOcrAsync(Mat cardImage, CancellationToken ct = default)
    {
        string result;
        await Dispatcher.InvokeAsync(() =>
        {
            _scanAnimDots = 0;
            _scanAnimTimer = new System.Windows.Threading.DispatcherTimer();
            _scanAnimTimer.Interval = TimeSpan.FromMilliseconds(500);
            _scanAnimTimer.Tick += (s, e) =>
            {
                _scanAnimDots = (_scanAnimDots + 1) % 4;
                StatusText.Text = "Scanning" + new string('.', _scanAnimDots == 0 ? 3 : _scanAnimDots);
            };
            _scanAnimTimer.Start();
            StatusText.Text = "Scanning...";
            StatusText.Foreground = System.Windows.Media.Brushes.Gold;
            CaptureButton.IsEnabled = false;
            CaptureButton.Content = "Scanning";
        });
        try
        {
            ct.ThrowIfCancellationRequested();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (cardImage)
            {
            int w = cardImage.Width, h = cardImage.Height;

            string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug");
            Directory.CreateDirectory(debugDir);
            var timestamp = DateTime.Now.ToString("HHmmss");

            // Save full card and bottom crop for debug
            Cv2.ImEncode(".png", cardImage, out var fullCardPng);
            File.WriteAllBytes(Path.Combine(debugDir, $"{timestamp}_fullcard.png"), fullCardPng);

            int bottomTop = h * 75 / 100;
            int bottomH = h * 25 / 100;
            using var bottomCrop = new Mat(cardImage, new OpenCvSharp.Rect(0, bottomTop, w, bottomH));
            Cv2.ImEncode(".png", bottomCrop, out var bottomPng);
            File.WriteAllBytes(Path.Combine(debugDir, $"{timestamp}_bottom.png"), bottomPng);

            var (llmName, llmNumber) = await IdentifyWithLlmAsync(cardImage, bottomCrop, ct);
            var llmMs = sw.ElapsedMilliseconds;
            LogInfo($"[PokeScanner] LLM took {llmMs}ms");
            string topText = llmName;
            string botText = llmNumber;

            LogInfo($"[PokeScanner] LLM: name='{llmName}' number='{llmNumber}'");

            // Fallback to Tesseract if LLM returns nothing useful
            if (string.IsNullOrEmpty(topText) || string.IsNullOrEmpty(botText) || !Regex.IsMatch(botText, @"\d+/\d+"))
            {
                LogInfo("[PokeScanner] LLM failed or incomplete, falling back to Tesseract...");

                // --- Card name bar: top 5-15% of card, left 75% to skip HP ---
                int nameTop = h * 5 / 100;
                int nameH = h * 12 / 100;
                int nameW = w * 75 / 100;
                var nameRect = new OpenCvSharp.Rect(0, nameTop, nameW, nameH);
                using var nameRegion = new Mat(cardImage, nameRect);
                Cv2.ImEncode(".png", nameRegion, out var nameDebugPng);
                File.WriteAllBytes(Path.Combine(debugDir, $"{timestamp}_name_crop.png"), nameDebugPng);

                if (string.IsNullOrEmpty(topText))
                {
                    topText = OcrWithOtsu(nameRegion, timestamp, "name",
                        psmList: new[] { "7", "6" },
                        whitelist: "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz ",
                        minTextLen: 3);
                }
                else
                {
                    // Still save the debug image for comparison
                    LogInfo("[PokeScanner] Name from LLM OK, skipping Tesseract for name");
                }

                if (string.IsNullOrEmpty(botText) || !Regex.IsMatch(botText, @"\d+/\d+"))
                {
                    // --- Set number: row at ~80-90% of card, left 70% to skip flavor text ---
                    int setTop = h * 78 / 100;
                    int setH = h * 14 / 100;
                    int setW = w * 70 / 100;
                    var setRect = new OpenCvSharp.Rect(0, setTop, setW, setH);
                    using var setRegion = new Mat(cardImage, setRect);
                    Cv2.ImEncode(".png", setRegion, out var setDebugPng);
                    File.WriteAllBytes(Path.Combine(debugDir, $"{timestamp}_set_crop.png"), setDebugPng);

                    botText = OcrWithOtsu(setRegion, timestamp, "setnum",
                        psmList: new[] { "7", "6" },
                        whitelist: "0123456789/ ",
                        minTextLen: 1,
                        requirePattern: @"\d+\s*/\s*\d+");
                } // end inner if (set number fallback)

                if (string.IsNullOrEmpty(botText) || !Regex.IsMatch(botText, @"\d+\s*/\s*\d+"))
                {
                    // Try a wider set number crop
                    int setTop2 = h * 75 / 100;
                    int setH2 = h * 18 / 100;
                    var setRect2 = new OpenCvSharp.Rect(0, setTop2, w * 75 / 100, setH2);
                    using var setRegion2 = new Mat(cardImage, setRect2);
                    botText = OcrWithOtsu(setRegion2, timestamp, "setnum2",
                        psmList: new[] { "6", "7" },
                        whitelist: "0123456789/ ",
                        minTextLen: 1,
                        requirePattern: @"\d+\s*/\s*\d+");
                }

                if (string.IsNullOrEmpty(botText) || !Regex.IsMatch(botText, @"\d+\s*/\s*\d+"))
                {
                    botText = ExtractSetNumber(topText);
                }
            } // end outer if (LLM fallback)

            // Normalize the cropped set number for OCR digit misreads
            string NormalizeSetNumber(string raw)
            {
                var ocrPattern = Regex.Match(raw, @"([0-9A-Za-z]+)\s*/\s*(\d+)");
                if (ocrPattern.Success)
                {
                    string first = ocrPattern.Groups[1].Value;
                    string second = ocrPattern.Groups[2].Value;
                    var digitMap = GetDigitMap();
                    string corrected = string.Concat(first.Select(c => digitMap.TryGetValue(c, out var d) ? d : c));
                    if (corrected.All(char.IsDigit) && corrected.Length <= 3 && second.Length <= 3)
                    {
                        int a = int.Parse(corrected);
                        int b = int.Parse(second);
                        if (a <= 250 && b <= 250)
                            return $"{corrected}/{second}";
                    }
                }
                return raw;
            }

            var setMatch = Regex.Match(botText, @"(\d+)\s*/\s*(\d+)");
            if (setMatch.Success)
            {
                botText = setMatch.Value;
            }
            else
            {
                botText = NormalizeSetNumber(botText);
            }

            LogInfo($"[PokeScanner] Final bottom (setNum): '{botText}'");

            // Extract card name and optionally the localId from LLM number
            var (cardName, hp) = ParseOcrFields(topText);
            var localIdMatch = Regex.Match(botText, @"^(\d+)\s*/");
            var llmLocalId = localIdMatch.Success ? localIdMatch.Groups[1].Value : "";
            LogInfo($"[PokeScanner] Parsed: name='{cardName}' hp='{hp}' llmLocalId='{llmLocalId}'");

            result = "";
            List<CardResult> apiResults = new();
            var statusMsg = "";

            // Search TCGdex by name only (LLM is reliable for names, not numbers)
            if (!string.IsNullOrEmpty(cardName))
                apiResults = await LookupCardsAsync(cardName, hp, llmLocalId, ct);
            var apiMs = sw.ElapsedMilliseconds;
            LogInfo($"[PokeScanner] TCGdex took {apiMs - llmMs}ms (total {apiMs}ms)");

            var totalMs = sw.ElapsedMilliseconds;
            if (apiResults.Count > 0)
            {
                var best = apiResults[0];
                result = $"{best.Name} — #{best.Number} ({best.SetName})";
                statusMsg = $"Found {apiResults.Count} card(s) — {best.Name} selected ({totalMs}ms)";
            }
            else
            {
                var nameForMsg = string.IsNullOrEmpty(cardName) ? "(unknown)" : cardName;
                statusMsg = $"No cards found for '{nameForMsg}' — check card position ({totalMs}ms)";
            }

            await Dispatcher.InvokeAsync(() =>
            {
                _scanAnimTimer?.Stop();
                _scanAnimTimer = null;
                CardInfoText.Text = result;
                CardResultList.ItemsSource = null;
                CardResultList.ItemsSource = apiResults;
                _selectedCard = apiResults.FirstOrDefault();
                CardResultList.SelectedItem = _selectedCard;
                CollectrButton.IsEnabled = apiResults.Count > 0;
                TcgplayerButton.IsEnabled = apiResults.Count > 0;
                StatusText.Text = statusMsg;
                StatusText.Foreground = apiResults.Count > 0
                    ? System.Windows.Media.Brushes.LightGreen
                    : System.Windows.Media.Brushes.Orange;
                CaptureButton.IsEnabled = true;
                CaptureButton.Content = "Capture Card";
            });
            }
        }
        catch (OperationCanceledException)
        {
            LogInfo("[PokeScanner] Scan cancelled");
            await Dispatcher.InvokeAsync(() =>
            {
                _scanAnimTimer?.Stop();
                _scanAnimTimer = null;
                CaptureButton.IsEnabled = true;
                CaptureButton.Content = "Capture Card";
                StatusText.Text = "Scan cancelled";
                StatusText.Foreground = System.Windows.Media.Brushes.Gray;
            });
        }
        catch (Exception ex)
        {
            LogInfo($"[PokeScanner] OCR error: {ex}");
            result = $"OCR error: {ex.Message}";
            await Dispatcher.InvokeAsync(() =>
            {
                _scanAnimTimer?.Stop();
                _scanAnimTimer = null;
                CardInfoText.Text = result;
                CollectrButton.IsEnabled = false;
                TcgplayerButton.IsEnabled = false;
                StatusText.Text = "Error during scan — see debug log";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                CaptureButton.IsEnabled = true;
                CaptureButton.Content = "Capture Card";
            });
        }
    }

    private static Dictionary<char, char> GetDigitMap() => new()
    {
        { 'O', '0' }, { 'o', '0' }, { 'P', '0' },
        { 'S', '5' }, { 's', '5' },
        { 'I', '1' }, { 'l', '1' },
        { 'r', '7' }, { 'R', '7' },
        { 'b', '6' },
        { 'g', '9' },
        { '6', '0' }, { '9', '1' },
    };

    private static string ExtractSetNumber(string text)
    {
        var digitMap = GetDigitMap();

        // Try "XX/XXX" pattern first
        var m = Regex.Match(text, @"\b(\d{1,3})\s*/\s*(\d{1,3})\b");
        if (m.Success)
        {
            int a = int.Parse(m.Groups[1].Value);
            int b = int.Parse(m.Groups[2].Value);
            if (a <= 250 && b <= 250) return m.Value;
        }

        // Try patterns where "/" was misread as 7, I, l
        m = Regex.Match(text, @"\b(\d{3})[7Il](\d{3})\b");
        if (m.Success && int.Parse(m.Groups[1].Value) <= 250 && int.Parse(m.Groups[2].Value) <= 250)
            return $"{m.Groups[1].Value}/{m.Groups[2].Value}";

        // Try patterns with OCR-letter-digits like "5r1/094" (0→P/5, 7→r, etc.)
        m = Regex.Match(text, @"\b([0-9A-Za-z]{1,3})\s*/\s*(\d{1,3})\b");
        if (m.Success)
        {
            string first = m.Groups[1].Value;
            string second = m.Groups[2].Value;
            string corrected = string.Concat(first.Select(c => digitMap.TryGetValue(c, out var d) ? d : c));
            if (corrected.All(char.IsDigit) && corrected.Length <= 3 && second.Length <= 3)
            {
                int a = int.Parse(corrected);
                int b = int.Parse(second);
                if (a <= 250 && b <= 250) return $"{corrected}/{second}";
            }
        }

        return "";
    }

    private CardResult? _selectedCard = null;

    private class CardResult
    {
        public string CardId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Number { get; set; } = "";
        public string SetName { get; set; } = "";
        public string Hp { get; set; } = "";
        public int Score { get; set; }
        public string DisplayText => $"{Name} #{Number} ({SetName}) HP={Hp} match={Score}%";
    }

    private static readonly HttpClient _http = new();

    private async Task<List<CardResult>> LookupCardsAsync(string cardName, string? hpStr, string? llmLocalId = null, CancellationToken ct = default)
    {
        var results = new List<CardResult>();
        try
        {
            ct.ThrowIfCancellationRequested();
            var searchUrl = $"https://api.tcgdex.net/v2/en/cards?name={Uri.EscapeDataString(cardName)}";
            LogInfo($"[PokeScanner] TCGdex search: {searchUrl}");
            var response = await _http.GetAsync(searchUrl, ct);
            LogInfo($"[PokeScanner] TCGdex response: {response.StatusCode}");
            if (!response.IsSuccessStatusCode) return results;

            var briefJson = await response.Content.ReadAsStringAsync(ct);
            using var briefDoc = JsonDocument.Parse(briefJson);
            var briefCards = briefDoc.RootElement.EnumerateArray().ToList();
            LogInfo($"[PokeScanner] TCGdex found {briefCards.Count} cards");

            if (briefCards.Count == 0) return results;

            var lockObj = new object();
            var sem = new SemaphoreSlim(3);
            var tasks = briefCards.Select(async brief =>
            {
                var cardId = brief.GetProperty("id").GetString() ?? "";
                if (string.IsNullOrEmpty(cardId)) return;

                await sem.WaitAsync(ct);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var fullUrl = $"https://api.tcgdex.net/v2/en/cards/{cardId}";
                    var fullResponse = await _http.GetAsync(fullUrl, ct);
                    if (!fullResponse.IsSuccessStatusCode) return;

                    var fullJson = await fullResponse.Content.ReadAsStringAsync(ct);
                    using var fullDoc = JsonDocument.Parse(fullJson);
                    var card = fullDoc.RootElement;

                    var apiName = card.GetProperty("name").GetString() ?? "";
                    var apiNum = card.GetProperty("localId").GetString() ?? "";
                    var apiHp = card.TryGetProperty("hp", out var hp) && hp.ValueKind == JsonValueKind.String
                        ? hp.GetString() ?? "" : "-";

                    string apiSetName = "";
                    if (card.TryGetProperty("set", out var set) && set.ValueKind == JsonValueKind.Object)
                        apiSetName = set.GetProperty("name").GetString() ?? "";

                    int score = 0;
                    if (apiName == cardName) score += 15;
                    if (apiName.StartsWith(cardName, StringComparison.OrdinalIgnoreCase)) score += 5;
                    if (!string.IsNullOrEmpty(hpStr) && apiHp == hpStr) score += 3;
                    if (!string.IsNullOrEmpty(llmLocalId) && apiNum == llmLocalId) score += 10;

                    lock (lockObj)
                    {
                        results.Add(new CardResult
                        {
                            CardId = cardId,
                            Name = apiName,
                            Number = apiNum,
                            SetName = apiSetName,
                            Hp = apiHp,
                            Score = score,
                        });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    LogInfo($"[PokeScanner] Error fetching {cardId}: {ex.Message}");
                }
                finally
                {
                    sem.Release();
                }
            });

            await Task.WhenAll(tasks).WaitAsync(ct);

            results = results.OrderByDescending(r => r.Score).ThenBy(r => r.Name).ToList();
            LogInfo($"[PokeScanner] Processed {results.Count} card details");
            return results;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogInfo($"[PokeScanner] TCGdex API error: {ex.Message}");
            return results;
        }
    }

    private static (string name, string hp) ParseOcrFields(string mainText)
    {
        var lines = mainText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        lines = Array.FindAll(lines, l => !string.IsNullOrWhiteSpace(l));

        var cardName = "";
        foreach (var line in lines)
        {
            var clean = Regex.Replace(line.Trim(), @"^[^A-Za-z0-9]+", "");
            if (clean.Count(char.IsLetter) >= 3)
            {
                var m = Regex.Match(clean, @"^[A-Za-z0-9\s]+");
                var candidate = m.Value.Trim();
                if (candidate.Length >= 3 && !candidate.StartsWith("Flip", StringComparison.OrdinalIgnoreCase))
                {
                    cardName = candidate;
                    break;
                }
            }
        }
        if (string.IsNullOrEmpty(cardName))
            cardName = lines.FirstOrDefault(l => l.Length > 2) ?? "";

        var hp = "";
        var nameLine = lines.FirstOrDefault(l => l.Contains(cardName)) ?? "";
        if (!string.IsNullOrEmpty(nameLine))
        {
            var afterName = nameLine.Substring(nameLine.IndexOf(cardName) + cardName.Length);
            var hpMatch = Regex.Match(afterName, @"(\d{2,3})");
            if (hpMatch.Success) hp = hpMatch.Groups[1].Value;
        }

        return (cardName, hp);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        CardInfoText.Text = "⚡ Select a camera and position a card...";
        CardResultList.ItemsSource = null;
        CollectrButton.IsEnabled = false;
        TcgplayerButton.IsEnabled = false;
        _selectedCard = null;
        CroppedPreview.Source = null;
        StatusText.Text = "Ready";
        StatusText.Foreground = System.Windows.Media.Brushes.Gray;
        CaptureButton.IsEnabled = true;
        CaptureButton.Content = "Capture Card";
    }

    private void CardResultList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedCard = CardResultList.SelectedItem as CardResult;
    }

    private void CollectrButton_Click(object sender, RoutedEventArgs e) => OpenLucky(_selectedCard, "collectr");

    private void TcgplayerButton_Click(object sender, RoutedEventArgs e) => OpenLucky(_selectedCard, "tcgplayer");

    private void OpenLucky(CardResult? card, string site)
    {
        if (card == null) return;
        var domain = site switch
        {
            "collectr" => "https://app.getcollectr.com",
            "tcgplayer" => "https://www.tcgplayer.com",
            _ => null
        };
        if (domain != null)
        {
            var number = card.Number?.Replace("/", "-") ?? "";
            var q = Uri.EscapeDataString($"{domain} : {card.Name} {number}");
            var url = $"https://www.google.com/search?q={q}";
            LogInfo($"[PokeScanner] Opening {site}: {url}");
            try
            {
                using var proc = new System.Diagnostics.Process();
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.FileName = url;
                proc.Start();
            }
            catch { }
        }
        else
        {
            var q = Uri.EscapeDataString($"{card.Name} {card.Number} {card.SetName} pokemon {site}");
            var url = $"https://www.google.com/search?q={q}&btnI=1";
            LogInfo($"[PokeScanner] Opening {site}: {url}");
            try
            {
                using var proc = new System.Diagnostics.Process();
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.FileName = url;
                proc.Start();
            }
            catch { }
        }
    }

    private static readonly HttpClient _llmHttp = new() { Timeout = TimeSpan.FromSeconds(60) };

    private async Task<(string name, string setNumber)> IdentifyWithLlmAsync(Mat cardImage, Mat? bottomCrop, CancellationToken ct = default)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            Cv2.ImEncode(".jpg", cardImage, out var jpg);
            var b64 = Convert.ToBase64String(jpg);

            var contentParts = new List<object>
            {
                new { type = "text", text = "Identify this Pokemon TCG card. The first image is the full card; the second image is a close-up of the bottom of the card with the set number. Return only JSON: {\"name\":\"card name\",\"number\":\"NNN/NNN\"}. No other text." },
                new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } }
            };

            if (bottomCrop != null)
            {
                using var enlarged = new Mat();
                Cv2.Resize(bottomCrop, enlarged, new OpenCvSharp.Size(bottomCrop.Width * 2, bottomCrop.Height * 2));
                Cv2.ImEncode(".jpg", enlarged, out var bottomJpg);
                var bottomB64 = Convert.ToBase64String(bottomJpg);
                contentParts.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{bottomB64}" } });
            }

            var body = new
            {
                model = _selectedModel,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentParts.ToArray()
                    }
                },
                max_tokens = 200,
                temperature = 0.1
            };

            var json = JsonSerializer.Serialize(body);
            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:4000/chat/completions");
            request.Headers.Add("Authorization", $"Bearer {_llmKey ?? ""}");
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _llmHttp.SendAsync(request, ct);
            var respBody = await response.Content.ReadAsStringAsync(ct);
            LogInfo($"[PokeScanner] LLM response: {respBody}");

            using var doc = JsonDocument.Parse(respBody);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

            // Extract JSON from response (LLM may wrap in markdown)
            var jsonMatch = Regex.Match(content, @"\{[^}]+\}");
            if (jsonMatch.Success)
            {
                using var cardDoc = JsonDocument.Parse(jsonMatch.Value);
                var name = cardDoc.RootElement.GetProperty("name").GetString() ?? "";
                var number = cardDoc.RootElement.GetProperty("number").GetString() ?? "";
                return (name.Trim(), number.Trim());
            }

            // Fallback: try to extract from raw text
            var nameMatch = Regex.Match(content, @"name["":]+\s*([A-Za-z0-9\s\-]+)", RegexOptions.IgnoreCase);
            var numMatch = Regex.Match(content, @"number["":]+\s*(\d+/\d+)", RegexOptions.IgnoreCase);
            return (nameMatch.Success ? nameMatch.Groups[1].Value.Trim() : "",
                    numMatch.Success ? numMatch.Groups[1].Value.Trim() : "");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogInfo($"[PokeScanner] LLM error: {ex.Message}");
            return ("", "");
        }
    }

    private TesseractEngine GetOcrEngine()
    {
        if (_ocrEngine == null)
        {
            var dataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            LogInfo($"[PokeScanner] Loading Tesseract from {dataPath}");
            _ocrEngine = new TesseractEngine(dataPath, "eng", EngineMode.Default);
        }

        return _ocrEngine;
    }

    private Mat PreprocessForOcr(Mat src)
    {
        // 1. Denoise (fastNlMeans is slow; use bilateral for edges-preserving smoothing)
        using var denoised = new Mat();
        Cv2.BilateralFilter(src, denoised, 5, 50, 50);

        // 2. Convert to grayscale
        using var gray = new Mat();
        Cv2.CvtColor(denoised, gray, ColorConversionCodes.BGR2GRAY);

        // 3. CLAHE - contrast enhancement (clip limit 2.0, 8x8 grid)
        using var clahe = Cv2.CreateCLAHE(clipLimit: 2.0, tileGridSize: new OpenCvSharp.Size(8, 8));
        using var enhanced = new Mat();
        clahe.Apply(gray, enhanced);

        // 4. Sharpen (unsharp mask)
        using var blurred = new Mat();
        Cv2.GaussianBlur(enhanced, blurred, new OpenCvSharp.Size(0, 0), 3);
        using var sharpened = new Mat();
        Cv2.AddWeighted(enhanced, 1.5, blurred, -0.5, 0, sharpened);

        return sharpened.Clone();
    }

    private string OcrWithOtsu(Mat region, string timestamp, string label,
        string[] psmList, string? whitelist = null, int minTextLen = 1, string? requirePattern = null)
    {
        string debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug");
        var engine = GetOcrEngine();
        string firstNonEmpty = "";

        // Convert to grayscale
        using var gray = new Mat();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);

        // Otsu binarization - black text on white background
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // Check if the text is light-on-dark (need to invert)
        // If mean brightness is low, most pixels are dark = inverted text
        double meanBrightness = Cv2.Mean(binary).Val0;
        if (meanBrightness < 127)
        {
            Cv2.BitwiseNot(binary, binary);
        }

        // Save the binarized image for debug
        Cv2.ImEncode(".png", binary, out var binPng);
        File.WriteAllBytes(Path.Combine(debugDir, $"{timestamp}_{label}_bin.png"), binPng);

        foreach (var psm in psmList)
        {
            // Upscale 4x
            int upW = binary.Width * 4;
            int upH = binary.Height * 4;
            using var big = new Mat();
            Cv2.Resize(binary, big, new OpenCvSharp.Size(upW, upH), 0, 0, InterpolationFlags.Cubic);

            // Add white border padding (Tesseract works better with margin)
            using var padded = new Mat();
            Cv2.CopyMakeBorder(big, padded, 30, 30, 30, 30, BorderTypes.Constant, new Scalar(255));

            Cv2.ImEncode(".png", padded, out var png);
            File.WriteAllBytes(Path.Combine(debugDir, $"{timestamp}_{label}_psm{psm}.png"), png);

            engine.SetVariable("tessedit_pageseg_mode", psm);
            engine.SetVariable("tessedit_char_whitelist", whitelist ?? "");

            using var pix = Pix.LoadFromMemory(png);
            using var page = engine.Process(pix);
            var text = page.GetText().Trim();

            LogInfo($"[PokeScanner] {label} psm{psm}: '{text.Replace("\n", "\\n")}'");

            if (string.IsNullOrEmpty(text)) continue;
            if (firstNonEmpty == "") firstNonEmpty = text;

            // If a required pattern is set, only return if it matches
            if (requirePattern != null && Regex.IsMatch(text, requirePattern))
                return text;

            // If no pattern required and we got something, return first non-empty
            if (requirePattern == null && text.Length >= minTextLen)
                return text;
        }

        return firstNonEmpty;
    }

    private (int X, int Y, int Width, int Height)? GetImageBounds()
    {
        if (CameraPreview.Source == null) return null;

        double renderW = CameraPreview.ActualWidth;
        double renderH = CameraPreview.ActualHeight;
        double sourceW = CameraPreview.Source.Width;
        double sourceH = CameraPreview.Source.Height;

        if (sourceW <= 0 || sourceH <= 0 || renderW <= 0 || renderH <= 0) return null;

        double scale = Math.Min(renderW / sourceW, renderH / sourceH);
        int dispW = (int)(sourceW * scale);
        int dispH = (int)(sourceH * scale);
        int offX = (int)((renderW - dispW) / 2);
        int offY = (int)((renderH - dispH) / 2);

        return (offX, offY, dispW, dispH);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        LogInfo("[PokeScanner] Window closing");
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        StopCapture();
        _ocrEngine?.Dispose();
    }

    private record CameraItem(int Index, string Label)
    {
        public override string ToString() => Label;
    }
}
