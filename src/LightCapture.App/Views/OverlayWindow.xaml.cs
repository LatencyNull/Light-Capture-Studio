using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using Microsoft.Win32;
using LightCapture.App.Controls;
using LightCapture.Core.Models;
using LightCapture.Core.Services;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Clipboard = System.Windows.Clipboard;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using PrintDialog = System.Windows.Controls.PrintDialog;

namespace LightCapture.App.Views;

public partial class OverlayWindow : Window
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const uint GA_ROOT = 2;

    private record DetectedWindow(IntPtr Handle, Rect DipBounds);
    private readonly List<DetectedWindow> _detectedWindows = new();
    private Rect? _hoveredWindowBounds;

    private readonly BitmapSource _frozenScreen;
    private readonly ICaptureService _captureService;
    private readonly IOcrService _ocrService;
    private readonly ITranslationService _translationService;
    private readonly IQrScannerService _qrScanner = new QrScannerService();
    private readonly ICaptureHistoryService _historyService;
    private readonly ISpeechService _speechService = new SpeechService();
    private readonly ICloudUploadService _cloudUploadService;
    private readonly IVideoRecordingService _videoRecorder = new ScreenRecorderService();
    private RecordingControlWidget? _recordingWidget;
    private RecordingBorderOverlay? _recordingBorder;

    private Point _selectionStart;
    private Rect _selectionBounds;
    private bool _isSelecting;
    private bool _hasSelection;
    private bool _isDraggingSelection;
    private Point _dragStartPoint;
    private string _lastTranslatedPlainText = string.Empty;
    private string _lastExtractedOcrText = string.Empty;
    private string? _detectedQrCode;
    private bool _isSearchingLens;

    public OverlayWindow(
        BitmapSource frozenScreen,
        ScreenBounds screenBounds,
        ICaptureService captureService,
        IOcrService ocrService,
        ITranslationService translationService,
        ICaptureHistoryService? historyService = null,
        ICloudUploadService? cloudUploadService = null)
    {
        InitializeComponent();

        _frozenScreen = frozenScreen;
        _captureService = captureService;
        _ocrService = ocrService;
        _translationService = translationService;
        _historyService = historyService ?? new CaptureHistoryService();
        _cloudUploadService = cloudUploadService ?? new CloudUploadService();

        // Alineación 1:1 exacta en coordenadas DIP (elimina por completo el zoom por escalado DPI de Windows)
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        FrozenScreenImage.Width = Width;
        FrozenScreenImage.Height = Height;
        FrozenScreenImage.Source = _frozenScreen;

        MainDrawingCanvas.Width = Width;
        MainDrawingCanvas.Height = Height;
        MainDrawingCanvas.BackgroundSource = _frozenScreen;
        UpdateDimming(Rect.Empty);

        SetupToolbar();
        Loaded += (s, e) => DetectDesktopWindows();
    }

    private void DetectDesktopWindows()
    {
        try
        {
            _detectedWindows.Clear();
            IntPtr currentHwnd = new WindowInteropHelper(this).Handle;

            EnumWindows((hWnd, lParam) =>
            {
                if (hWnd == currentHwnd) return true;
                if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;

                var sbClass = new System.Text.StringBuilder(128);
                GetClassName(hWnd, sbClass, 128);
                string className = sbClass.ToString();
                if (className == "Progman" || className == "WorkerW" || className == "Shell_TrayWnd") return true;

                RECT rect;
                if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>()) != 0)
                {
                    GetWindowRect(hWnd, out rect);
                }

                double rawX = (rect.Left - Left * ScaleX) / ScaleX;
                double rawY = (rect.Top - Top * ScaleY) / ScaleY;
                double rawW = (rect.Right - rect.Left) / ScaleX;
                double rawH = (rect.Bottom - rect.Top) / ScaleY;

                // Clampeamos a los límites de la pantalla (evita fallos en ventanas maximizadas)
                double clLeft = Math.Max(0, rawX);
                double clTop = Math.Max(0, rawY);
                double clRight = Math.Min(Width, rawX + rawW);
                double clBottom = Math.Min(Height, rawY + rawH);
                double validW = clRight - clLeft;
                double validH = clBottom - clTop;

                if (validW > 80 && validH > 80)
                {
                    _detectedWindows.Add(new DetectedWindow(hWnd, new Rect(clLeft, clTop, validW, validH)));
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
    }

    private double ScaleX => _frozenScreen.PixelWidth / Math.Max(1.0, Width);
    private double ScaleY => _frozenScreen.PixelHeight / Math.Max(1.0, Height);

    private void SetupToolbar()
    {
        MainToolbar.ToolSelected += tool =>
        {
            CommitFloatingText();
            MainDrawingCanvas.CurrentTool = tool;
            if (tool == DrawingToolType.Text)
            {
                ShowToast("🔤 Haz clic en la captura para escribir");
            }
            else
            {
                ShowToast($"Herramienta: {tool.ToString().ToUpper()}");
            }
        };

        MainToolbar.ColorSelected += color =>
        {
            MainDrawingCanvas.CurrentColor = color;
            if (FloatingInputBox.Visibility == Visibility.Visible)
            {
                FloatingInputBox.Foreground = new SolidColorBrush(color);
            }
        };

        MainToolbar.UndoRequested += PerformUndo;

        MainToolbar.TranslateRequested += async () => await OnTranslateButtonClickedAsync();
        MainToolbar.TranslateTextCopyRequested += CopyTranslatedTextToClipboard;
        MainToolbar.CopyOcrTextRequested += async () => await CopyRawOcrTextToClipboardAsync();

        MainToolbar.SpeakRequested += SpeakCurrentText;
        MainToolbar.PinRequested += PinCaptureToScreen;

        MainToolbar.CopyRequested += CopyCaptureToClipboard;
        MainToolbar.SaveRequested += SaveCaptureToFile;
        MainToolbar.PrintRequested += PrintCapture;
        MainToolbar.RecordVideoRequested += (audio, mic) => StartVideoRecording(audio, mic, false);
        MainToolbar.SearchRequested += SearchOnGoogle;
        MainToolbar.GeminiRequested += AskGeminiAboutCapture;
        MainToolbar.GeminiPromptCopyRequested += CopyGeminiPromptToClipboard;
        MainToolbar.GeminiGoogleSearchRequested += SearchGeminiGoogleAsync;
        MainToolbar.GeminiPerplexityRequested += SearchPerplexityAsync;
        MainToolbar.GeminiWebChatRequested += OpenGeminiWebChat;

        MainToolbar.ShareWhatsAppRequested += ShareOnWhatsAppApp;
        MainToolbar.ShareWhatsAppAppRequested += ShareOnWhatsAppApp;
        MainToolbar.ShareWhatsAppWebRequested += ShareOnWhatsAppWeb;
        MainToolbar.ShareDiscordRequested += ShareOnDiscord;
        MainToolbar.ShareFacebookRequested += ShareOnFacebook;
        MainToolbar.ShareTikTokRequested += ShareOnTikTok;
        MainToolbar.ShareInstagramRequested += ShareOnInstagram;
        MainToolbar.ShareTelegramRequested += ShareOnTelegram;
        MainToolbar.ShareTwitterRequested += ShareOnTwitter;
        MainToolbar.ShareEmailRequested += ShareByEmail;
        MainToolbar.ShareCloudRequested += UploadToCloud;

        MainToolbar.UploadRequested += UploadToCloud;
        MainToolbar.CloseRequested += () => Close();
    }

    private void UpdateDimming(Rect selectedArea)
    {
        var fullRect = new RectangleGeometry(new Rect(0, 0, Width, Height));
        if (selectedArea.IsEmpty || selectedArea.Width <= 0 || selectedArea.Height <= 0)
        {
            DimmingPath.Data = fullRect;
            return;
        }

        var selGeometry = new RectangleGeometry(selectedArea);
        var combined = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, selGeometry);
        DimmingPath.Data = combined;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        Point p = e.GetPosition(this);

        // ⭐ Ajuste Magnético Inteligente (Doble clic antes o durante selección)
        if (e.ClickCount == 2)
        {
            SnapToWindowAtPoint(p);
            return;
        }

        if (!_hasSelection)
        {
            _selectionStart = p;
            _isSelecting = true;
            _selectionBounds = new Rect(p, p);
            SelectionLayer.Visibility = Visibility.Visible;
            WindowSnapBorder.Visibility = Visibility.Collapsed;
            HandlesLayer.Visibility = Visibility.Collapsed;
            MainToolbar.Visibility = Visibility.Collapsed;
            MainToolbar.ResetToolSelection();
            MainDrawingCanvas.CurrentTool = DrawingToolType.None;
            DimensionTag.Visibility = Visibility.Visible;
            QrCodeBanner.Visibility = Visibility.Collapsed;
            MainDrawingCanvas.ClearTranslations();
            MainToolbar.SetTranslateActiveState(false, false);
        }
        else
        {
            if (_selectionBounds.Contains(p))
            {
                if (MainDrawingCanvas.CurrentTool != DrawingToolType.None)
                {
                    if (MainDrawingCanvas.CurrentTool == DrawingToolType.Text)
                    {
                        CommitFloatingText();
                        ShowFloatingInputAt(p);
                        return;
                    }

                    CommitFloatingText();
                    MainDrawingCanvas.StartDrawing(p);
                }
                else
                {
                    CommitFloatingText();
                    _isDraggingSelection = true;
                    _dragStartPoint = p;
                }
            }
            else
            {
                CommitFloatingText();
                _hasSelection = false;
                _isSelecting = true;
                _selectionStart = p;
                _selectionBounds = new Rect(p, p);
                WindowSnapBorder.Visibility = Visibility.Collapsed;
                HandlesLayer.Visibility = Visibility.Collapsed;
                MainToolbar.Visibility = Visibility.Collapsed;
                QrCodeBanner.Visibility = Visibility.Collapsed;
                MainDrawingCanvas.ClearAll();
                MainDrawingCanvas.ClearTranslations();
                MainToolbar.SetTranslateActiveState(false, false);
            }
        }
    }

    private void SnapToWindowAtPoint(Point screenPoint)
    {
        try
        {
            var targetWin = _detectedWindows.FirstOrDefault(w => w.DipBounds.Contains(screenPoint));
            if (targetWin != null)
            {
                ApplyWindowSnap(targetWin.DipBounds);
                return;
            }

            var pt = new POINT { X = (int)(screenPoint.X * ScaleX + Left * ScaleX), Y = (int)(screenPoint.Y * ScaleY + Top * ScaleY) };
            IntPtr hWnd = WindowFromPoint(pt);
            if (hWnd != IntPtr.Zero)
            {
                IntPtr rootWnd = GetAncestor(hWnd, GA_ROOT);
                if (rootWnd != IntPtr.Zero) hWnd = rootWnd;

                RECT rect;
                if (DwmGetWindowAttribute(hWnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>()) != 0)
                {
                    GetWindowRect(hWnd, out rect);
                }

                double snapX = Math.Max(0, (rect.Left - Left * ScaleX) / ScaleX);
                double snapY = Math.Max(0, (rect.Top - Top * ScaleY) / ScaleY);
                double snapW = Math.Min(Width - snapX, (rect.Right - rect.Left) / ScaleX);
                double snapH = Math.Min(Height - snapY, (rect.Bottom - rect.Top) / ScaleY);

                if (snapW > 40 && snapH > 40)
                {
                    ApplyWindowSnap(new Rect(snapX, snapY, snapW, snapH));
                }
            }
        }
        catch { }
    }

    private void ApplyWindowSnap(Rect snapBounds)
    {
        _isSelecting = false;
        _selectionBounds = snapBounds;
        _hasSelection = true;
        _hoveredWindowBounds = null;
        WindowSnapBorder.Visibility = Visibility.Collapsed;
        UpdateSelectionVisuals(_selectionBounds);
        UpdateDimming(_selectionBounds);
        HandlesLayer.Visibility = Visibility.Visible;
        MainToolbar.Visibility = Visibility.Visible;
        MainDrawingCanvas.ActiveSelectionBounds = _selectionBounds;
        MainToolbar.UpdatePosition(_selectionBounds, Width, Height);
        CheckForQrCodeAsync();
        ShowToast("🧲 Ventana seleccionada automáticamente");
    }

    private Point _floatingInputPos;

    private void ShowFloatingInputAt(Point p)
    {
        _floatingInputPos = p;
        double inputX = Math.Max(_selectionBounds.Left, Math.Min(_selectionBounds.Right - 130, p.X));
        double inputY = Math.Max(_selectionBounds.Top, Math.Min(_selectionBounds.Bottom - 35, p.Y));

        Canvas.SetLeft(FloatingInputBox, inputX);
        Canvas.SetTop(FloatingInputBox, inputY);
        FloatingInputBox.Foreground = new SolidColorBrush(MainDrawingCanvas.CurrentColor);
        FloatingInputBox.Text = string.Empty;
        FloatingInputBox.Visibility = Visibility.Visible;
        FloatingInputBox.Focus();
    }

    private void CommitFloatingText()
    {
        if (FloatingInputBox.Visibility != Visibility.Visible) return;

        string text = FloatingInputBox.Text.Trim();
        FloatingInputBox.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrEmpty(text))
        {
            MainDrawingCanvas.AddTextElement(_floatingInputPos, text);
            ShowToast("✍️ Texto añadido");
        }
    }

    private void FloatingInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            CommitFloatingText();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            FloatingInputBox.Text = string.Empty;
            FloatingInputBox.Visibility = Visibility.Collapsed;
        }
    }

    private void FloatingInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitFloatingText();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        Point current = e.GetPosition(this);

        if (!_hasSelection && !_isSelecting)
        {
            // Detección automática de ventanas bajo el cursor (Ajuste Magnético Inteligente)
            var targetWin = _detectedWindows.FirstOrDefault(w => w.DipBounds.Contains(current));
            if (targetWin != null)
            {
                _hoveredWindowBounds = targetWin.DipBounds;
                Canvas.SetLeft(WindowSnapBorder, targetWin.DipBounds.X);
                Canvas.SetTop(WindowSnapBorder, targetWin.DipBounds.Y);
                WindowSnapBorder.Width = targetWin.DipBounds.Width;
                WindowSnapBorder.Height = targetWin.DipBounds.Height;
                WindowSnapBorder.Visibility = Visibility.Visible;
            }
            else
            {
                _hoveredWindowBounds = null;
                WindowSnapBorder.Visibility = Visibility.Collapsed;
            }
        }
        else if (_isSelecting)
        {
            double x = Math.Min(_selectionStart.X, current.X);
            double y = Math.Min(_selectionStart.Y, current.Y);
            double w = Math.Abs(current.X - _selectionStart.X);
            double h = Math.Abs(current.Y - _selectionStart.Y);

            _selectionBounds = new Rect(x, y, w, h);
            UpdateSelectionVisuals(_selectionBounds);
            UpdateDimming(_selectionBounds);
        }
        else if (_isDraggingSelection)
        {
            double dx = current.X - _dragStartPoint.X;
            double dy = current.Y - _dragStartPoint.Y;
            _dragStartPoint = current;

            _selectionBounds = new Rect(
                Math.Max(0, Math.Min(Width - _selectionBounds.Width, _selectionBounds.X + dx)),
                Math.Max(0, Math.Min(Height - _selectionBounds.Height, _selectionBounds.Y + dy)),
                _selectionBounds.Width,
                _selectionBounds.Height
            );

            UpdateSelectionVisuals(_selectionBounds);
            UpdateDimming(_selectionBounds);
        }
        else if (_hasSelection && MainDrawingCanvas.CurrentTool != DrawingToolType.None)
        {
            MainDrawingCanvas.UpdateDrawing(current);
        }

        if (_hasSelection && MainDrawingCanvas.CurrentTool == DrawingToolType.Text)
        {
            Cursor = _selectionBounds.Contains(current) ? System.Windows.Input.Cursors.IBeam : System.Windows.Input.Cursors.Arrow;
        }
        else if (!_isSelecting && !_isDraggingSelection && !_hasSelection)
        {
            Cursor = System.Windows.Input.Cursors.Arrow;
        }
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        Point p = e.GetPosition(this);

        if (_isSelecting)
        {
            _isSelecting = false;

            // Si fue un clic simple sin arrastrar sobre una ventana detectada -> Auto-seleccionar ventana
            double dragDist = (p - _selectionStart).Length;
            if (dragDist < 8 && _hoveredWindowBounds.HasValue)
            {
                ApplyWindowSnap(_hoveredWindowBounds.Value);
                return;
            }

            if (_selectionBounds.Width > 15 && _selectionBounds.Height > 15)
            {
                _hasSelection = true;
                HandlesLayer.Visibility = Visibility.Visible;
                MainToolbar.Visibility = Visibility.Visible;
                MainDrawingCanvas.ActiveSelectionBounds = _selectionBounds;
                MainToolbar.UpdatePosition(_selectionBounds, Width, Height);

                CheckForQrCodeAsync();
            }
            else
            {
                _hasSelection = false;
                SelectionRect.Visibility = Visibility.Collapsed;
                SelectionRectBacking.Visibility = Visibility.Collapsed;
                DimensionTag.Visibility = Visibility.Collapsed;
                QrCodeBanner.Visibility = Visibility.Collapsed;
                UpdateDimming(Rect.Empty);
            }
        }
        else if (_isDraggingSelection)
        {
            _isDraggingSelection = false;
            MainToolbar.UpdatePosition(_selectionBounds, Width, Height);
            UpdateQrBannerPosition();
        }
        else if (_hasSelection && MainDrawingCanvas.CurrentTool != DrawingToolType.None)
        {
            MainDrawingCanvas.EndDrawing(p);
        }
    }

    private void UpdateSelectionVisuals(Rect r)
    {
        SelectionRect.Visibility = Visibility.Visible;
        SelectionRectBacking.Visibility = Visibility.Visible;

        Canvas.SetLeft(SelectionRect, r.X);
        Canvas.SetTop(SelectionRect, r.Y);
        SelectionRect.Width = r.Width;
        SelectionRect.Height = r.Height;

        Canvas.SetLeft(SelectionRectBacking, r.X);
        Canvas.SetTop(SelectionRectBacking, r.Y);
        SelectionRectBacking.Width = r.Width;
        SelectionRectBacking.Height = r.Height;

        DimensionTag.Visibility = Visibility.Visible;
        DimensionText.Text = $"{(int)r.Width}x{(int)r.Height}";
        double tagY = r.Y > 26 ? r.Y - 24 : r.Y + 4;
        Canvas.SetLeft(DimensionTag, r.X);
        Canvas.SetTop(DimensionTag, tagY);

        UpdateHandlesPosition(r);
        UpdateQrBannerPosition();
    }

    private void UpdateHandlesPosition(Rect r)
    {
        Canvas.SetLeft(HandleNW, r.X - 3); Canvas.SetTop(HandleNW, r.Y - 3);
        Canvas.SetLeft(HandleN, r.X + (r.Width / 2) - 3); Canvas.SetTop(HandleN, r.Y - 3);
        Canvas.SetLeft(HandleNE, r.Right - 3); Canvas.SetTop(HandleNE, r.Y - 3);

        Canvas.SetLeft(HandleW, r.X - 3); Canvas.SetTop(HandleW, r.Y + (r.Height / 2) - 3);
        Canvas.SetLeft(HandleE, r.Right - 3); Canvas.SetTop(HandleE, r.Y + (r.Height / 2) - 3);

        Canvas.SetLeft(HandleSW, r.X - 3); Canvas.SetTop(HandleSW, r.Bottom - 3);
        Canvas.SetLeft(HandleS, r.X + (r.Width / 2) - 3); Canvas.SetTop(HandleS, r.Bottom - 3);
        Canvas.SetLeft(HandleSE, r.Right - 3); Canvas.SetTop(HandleSE, r.Bottom - 3);
    }

    private void UpdateQrBannerPosition()
    {
        if (QrCodeBanner.Visibility == Visibility.Visible)
        {
            double qrX = _selectionBounds.X + 80;
            double qrY = _selectionBounds.Y > 36 ? _selectionBounds.Y - 32 : _selectionBounds.Y + 4;
            Canvas.SetLeft(QrCodeBanner, qrX);
            Canvas.SetTop(QrCodeBanner, qrY);
        }
    }

    private async void CheckForQrCodeAsync()
    {
        try
        {
            int cropX = (int)Math.Round(_selectionBounds.X * ScaleX);
            int cropY = (int)Math.Round(_selectionBounds.Y * ScaleY);
            int cropW = (int)Math.Round(_selectionBounds.Width * ScaleX);
            int cropH = (int)Math.Round(_selectionBounds.Height * ScaleY);

            var cropped = _captureService.CropBitmap(_frozenScreen, cropX, cropY, cropW, cropH);

            string? qrResult = await Task.Run(() => _qrScanner.ScanQrCode(cropped));
            if (!string.IsNullOrEmpty(qrResult))
            {
                _detectedQrCode = qrResult;
                QrTextPreview.Text = qrResult;
                QrCodeBanner.Visibility = Visibility.Visible;
                UpdateQrBannerPosition();
                ShowToast("¡Código QR detectado en la captura!");
            }
        }
        catch { }
    }

    private void BtnQrCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_detectedQrCode))
        {
            Clipboard.SetText(_detectedQrCode);
            ShowToast("Contenido del QR copiado al portapapeles 📋");
        }
    }

    private void BtnQrOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_detectedQrCode))
        {
            try
            {
                string url = _detectedQrCode.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             _detectedQrCode.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? _detectedQrCode
                    : "https://" + _detectedQrCode;

                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { }
        }
    }

    private void SpeakCurrentText()
    {
        string textToSpeak = !string.IsNullOrWhiteSpace(_lastTranslatedPlainText)
            ? _lastTranslatedPlainText
            : _lastExtractedOcrText;

        if (string.IsNullOrWhiteSpace(textToSpeak))
        {
            ShowToast("Pulsa Traducir o Copiar Texto antes de escuchar.");
            return;
        }

        _speechService.Speak(textToSpeak);
        ShowToast("🔊 Leyendo texto en voz alta...");
    }

    private void PinCaptureToScreen()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        _historyService.SaveCapture(flattened, "Fijado en pantalla " + _lastTranslatedPlainText + " " + _lastExtractedOcrText);
        var pinWindow = new PinnedCaptureWindow(flattened);
        pinWindow.Show();
        Close();
    }

    private async Task CopyRawOcrTextToClipboardAsync()
    {
        if (!_hasSelection) return;

        ShowToast("Extrayendo texto de pantalla (OCR)...");

        try
        {
            int cropX = (int)Math.Round(_selectionBounds.X * ScaleX);
            int cropY = (int)Math.Round(_selectionBounds.Y * ScaleY);
            int cropW = (int)Math.Round(_selectionBounds.Width * ScaleX);
            int cropH = (int)Math.Round(_selectionBounds.Height * ScaleY);

            var cropped = _captureService.CropBitmap(_frozenScreen, cropX, cropY, cropW, cropH);

            var blocks = await _ocrService.RecognizeTextAsync(cropped);
            if (blocks == null || blocks.Count == 0)
            {
                ShowToast("No se detectó texto en el área seleccionada.");
                return;
            }

            _lastExtractedOcrText = string.Join("\n", blocks.Select(b => b.Text));
            Clipboard.SetText(_lastExtractedOcrText);

            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            _historyService.SaveCapture(flattened, _lastExtractedOcrText);

            ShowToast("Texto extraído copiado al portapapeles 📋");
        }
        catch (Exception ex)
        {
            ShowToast($"Error OCR: {ex.Message}");
        }
    }

    private async Task OnTranslateButtonClickedAsync()
    {
        if (!_hasSelection) return;

        // Si ya está traducido, alternar (Toggle) entre Original y Traducción
        if (MainDrawingCanvas.HasTranslatedBlocks)
        {
            MainDrawingCanvas.ToggleTranslationVisibility();
            bool showingTranslation = MainDrawingCanvas.ShowTranslations;
            MainToolbar.SetTranslateActiveState(true, showingTranslation);

            if (showingTranslation)
            {
                ShowToast("👁️ Mostrando traducción (Clic de nuevo para ver original)");
            }
            else
            {
                ShowToast("🔄 Mostrando texto original (Clic de nuevo para traducir)");
            }
            return;
        }

        await ExecuteTranslationAsync();
    }

    private async Task ExecuteTranslationAsync()
    {
        if (!_hasSelection) return;

        ShowToast("Extrayendo texto con OCR...");

        try
        {
            int cropX = (int)Math.Round(_selectionBounds.X * ScaleX);
            int cropY = (int)Math.Round(_selectionBounds.Y * ScaleY);
            int cropW = (int)Math.Round(_selectionBounds.Width * ScaleX);
            int cropH = (int)Math.Round(_selectionBounds.Height * ScaleY);

            var cropped = _captureService.CropBitmap(_frozenScreen, cropX, cropY, cropW, cropH);

            var blocks = await _ocrService.RecognizeTextAsync(cropped);
            if (blocks == null || blocks.Count == 0)
            {
                ShowToast("No se detectó texto en la selección.");
                return;
            }

            ShowToast($"Traduciendo {blocks.Count} bloques en paralelo...");

            var tasks = blocks.Select(async block =>
            {
                var tr = await _translationService.TranslateAsync(block.Text, targetLang: "es", sourceLang: "auto");
                return (Block: block, Result: tr);
            }).ToList();

            var results = await Task.WhenAll(tasks);

            int successCount = 0;
            var lensBlocks = new List<TranslatedLensBlock>();
            var plainTextList = new List<string>();

            foreach (var item in results)
            {
                string translated;
                if (item.Result.IsSuccess && !string.IsNullOrWhiteSpace(item.Result.TranslatedText))
                {
                    translated = item.Result.TranslatedText;
                    successCount++;
                }
                else
                {
                    translated = item.Block.Text; // Fallback al texto original si falla
                }

                plainTextList.Add(translated);

                double dipX = item.Block.X / ScaleX;
                double dipY = item.Block.Y / ScaleY;
                double dipW = item.Block.Width / ScaleX;
                double dipH = item.Block.Height / ScaleY;

                var globalBox = new Rect(
                    _selectionBounds.X + dipX,
                    _selectionBounds.Y + dipY,
                    dipW,
                    Math.Max(18, dipH)
                );

                lensBlocks.Add(new TranslatedLensBlock(
                    Bounds: globalBox,
                    TranslatedText: translated,
                    BackgroundColor: Color.FromArgb(235, 20, 20, 24),
                    TextColor: Color.FromRgb(244, 244, 245),
                    FontSize: Math.Max(11, Math.Min(22, dipH * 0.72))
                ));
            }

            _lastTranslatedPlainText = string.Join("\n\n", plainTextList);
            _lastExtractedOcrText = string.Join(" ", blocks.Select(b => b.Text));

            MainDrawingCanvas.SetTranslatedBlocks(lensBlocks);
            MainToolbar.SetTranslateActiveState(true, true);

            if (successCount > 0)
            {
                ShowToast($"¡Traducción lista! ({successCount}/{blocks.Count}) - Clic para alternar a original");
            }
            else
            {
                ShowToast("⚠️ No se pudo obtener traducción del servidor.");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Error al traducir: {ex.Message}");
        }
    }

    private void CopyCaptureToClipboard()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);

        _historyService.SaveCapture(flattened, _lastTranslatedPlainText + " " + _lastExtractedOcrText);

        ToastNotificationWindow.ShowToast("Captura copiada al portapapeles 📑", "pack://application:,,,/Assets/Icons/copy.png");
        Close();
    }

    private void CopyTranslatedTextToClipboard()
    {
        if (string.IsNullOrWhiteSpace(_lastTranslatedPlainText))
        {
            ShowToast("No hay texto traducido aún. Pulsa Traducir primero.");
            return;
        }

        Clipboard.SetText(_lastTranslatedPlainText);

        if (_hasSelection)
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            _historyService.SaveCapture(flattened, "Traducción: " + _lastTranslatedPlainText);
        }

        ShowToast("Texto traducido copiado al portapapeles 📋");
    }

    private void SaveCaptureToFile()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);

        var sfd = new SaveFileDialog
        {
            InitialDirectory = _historyService.HistoryDirectory,
            Filter = "Imagen PNG (*.png)|*.png|Imagen JPEG (*.jpg)|*.jpg",
            FileName = $"LightCapture_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (sfd.ShowDialog() == true)
        {
            using var fileStream = File.Create(sfd.FileName);
            BitmapEncoder encoder = sfd.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                ? new JpegBitmapEncoder()
                : new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(flattened));
            encoder.Save(fileStream);

            _historyService.SaveCapture(flattened, _lastTranslatedPlainText + " " + _lastExtractedOcrText);

            ShowToast("Imagen guardada e indexada en historial");
            Close();
        }
    }

    private void PrintCapture()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        var printDlg = new PrintDialog();
        if (printDlg.ShowDialog() == true)
        {
            var vis = new DrawingVisual();
            using (var dc = vis.RenderOpen())
            {
                dc.DrawImage(flattened, new Rect(0, 0, flattened.PixelWidth, flattened.PixelHeight));
            }
            printDlg.PrintVisual(vis, "LightCapture Screenshot");
            Close();
        }
    }

    private void StartVideoRecording(bool captureAudio, bool captureMic = true, bool isGif = false)
    {
        if (!_hasSelection) return;

        if (_selectionBounds.Width < 32 || _selectionBounds.Height < 32)
        {
            ShowToast("El área seleccionada es demasiado pequeña para grabar.");
            return;
        }

        int pxX = (int)Math.Round((Left + _selectionBounds.Left) * ScaleX);
        int pxY = (int)Math.Round((Top + _selectionBounds.Top) * ScaleY);
        int pxW = (int)Math.Round(_selectionBounds.Width * ScaleX);
        int pxH = (int)Math.Round(_selectionBounds.Height * ScaleY);

        var dipBounds = new Rect(Left + _selectionBounds.Left, Top + _selectionBounds.Top, _selectionBounds.Width, _selectionBounds.Height);

        // Ocultamos la ventana de captura congelada para dejar el escritorio 100% interactivo
        Visibility = Visibility.Collapsed;

        _recordingBorder = new RecordingBorderOverlay(dipBounds);
        _recordingBorder.Show();

        _recordingWidget = new RecordingControlWidget(captureAudio, captureMic, dipBounds, isGif);
        _recordingWidget.AudioToggled += enabled => _videoRecorder.SetAudioEnabled(enabled);
        _recordingWidget.MicToggled += enabled => _videoRecorder.SetMicEnabled(enabled);

        // El usuario presiona "Iniciar" en el widget de control
        _recordingWidget.StartRequested += async (audio, mic, gif, fps) =>
        {
            try
            {
                App.EnsureNativeDependencies();
                _recordingWidget.OnRecordingStarted();
                await _videoRecorder.StartRecordingAsync(pxX, pxY, pxW, pxH, audio, mic, gif, fps);
            }
            catch (Exception ex)
            {
                CloseRecordingOverlays();
                System.Windows.MessageBox.Show($"No se pudo iniciar la grabación: {ex.Message}", "Grabador de Pantalla", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        };

        _recordingWidget.StopRequested += async () => await StopVideoRecordingAsync();
        _recordingWidget.CancelRequested += async () => await CancelVideoRecordingAsync();
        _recordingWidget.Show();

        _videoRecorder.DurationTick += duration =>
        {
            _recordingWidget?.UpdateDuration(duration);
        };

        _videoRecorder.RecordingError += err =>
        {
            Dispatcher.Invoke(() =>
            {
                CloseRecordingOverlays();
                System.Windows.MessageBox.Show($"Error al grabar: {err}", "Grabador de Pantalla", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            });
        };
    }

    private async Task StopVideoRecordingAsync()
    {
        CloseRecordingOverlays();

        try
        {
            if (_videoRecorder.IsRecording)
            {
                string videoPath = await _videoRecorder.StopRecordingAsync();
                if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                {
                    var previewWindow = new VideoPreviewWindow(videoPath);
                    previewWindow.Show();
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error al procesar el archivo: {ex.Message}");
        }

        Close();
    }

    private async Task CancelVideoRecordingAsync()
    {
        CloseRecordingOverlays();

        try
        {
            if (_videoRecorder.IsRecording)
            {
                await _videoRecorder.CancelRecordingAsync();
            }
        }
        catch { }

        Close();
    }

    private void CloseRecordingOverlays()
    {
        try
        {
            _recordingBorder?.Close();
            _recordingBorder = null;
        }
        catch { }

        try
        {
            _recordingWidget?.Close();
            _recordingWidget = null;
        }
        catch { }
    }

    private async void SearchOnGoogle()
    {
        if (!_hasSelection || _isSearchingLens) return;

        _isSearchingLens = true;
        ShowToast("Buscando en Google Lens... 🔍");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);

            // Copia instantánea preventiva al portapapeles
            try { ClipboardHelper.CopyImageToClipboard(flattened); } catch { }

            // Subir la imagen a FreeImage (enlace CDN directo y ultra rápido)
            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);

            if (!string.IsNullOrEmpty(publicUrl))
            {
                _historyService.SaveCapture(flattened, "Búsqueda Google Lens");
                string lensUrl = GoogleLensService.BuildLensSearchUrl(publicUrl);
                Process.Start(new ProcessStartInfo(lensUrl) { UseShellExecute = true });
                ShowToast("¡Búsqueda abierta en Google Lens! 🔍");
                Close();
            }
            else
            {
                // Fallback de contingencia: portal de Google Lens con la imagen lista en portapapeles
                Process.Start(new ProcessStartInfo("https://lens.google.com/") { UseShellExecute = true });
                _historyService.SaveCapture(flattened, "Búsqueda Google Lens");
                ShowToast("Captura en portapapeles. Pega (Ctrl+V) en Google Lens 📋");
                Close();
            }
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://lens.google.com/") { UseShellExecute = true });
            }
            catch { }
            Close();
        }
        finally
        {
            _isSearchingLens = false;
        }
    }

    private async void SearchOnYandex()
    {
        if (!_hasSelection) return;

        ShowToast("Buscando en Yandex Images... 🟡");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);
            try { ClipboardHelper.CopyImageToClipboard(flattened); } catch { }

            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);
            if (!string.IsNullOrEmpty(publicUrl))
            {
                _historyService.SaveCapture(flattened, "Búsqueda Yandex Images");
                string yandexUrl = $"https://yandex.com/images/search?rpt=imageview&url={Uri.EscapeDataString(publicUrl)}";
                Process.Start(new ProcessStartInfo(yandexUrl) { UseShellExecute = true });
                ShowToast("¡Búsqueda abierta en Yandex Images! 🟡");
                Close();
            }
            else
            {
                Process.Start(new ProcessStartInfo("https://yandex.com/images/") { UseShellExecute = true });
                Close();
            }
        }
        catch
        {
            Process.Start(new ProcessStartInfo("https://yandex.com/images/") { UseShellExecute = true });
            Close();
        }
    }

    private async void SearchOnBing()
    {
        if (!_hasSelection) return;

        ShowToast("Buscando en Bing Visual Search... 🟦");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);
            try { ClipboardHelper.CopyImageToClipboard(flattened); } catch { }

            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);
            if (!string.IsNullOrEmpty(publicUrl))
            {
                _historyService.SaveCapture(flattened, "Búsqueda Bing Visual");
                string bingUrl = $"https://www.bing.com/images/search?view=detailv2&iss=sbi&FORM=SBIHMP&sbisrc=UrlPaste&q=imgurl:{Uri.EscapeDataString(publicUrl)}";
                Process.Start(new ProcessStartInfo(bingUrl) { UseShellExecute = true });
                ShowToast("¡Búsqueda abierta en Bing! 🟦");
                Close();
            }
            else
            {
                Process.Start(new ProcessStartInfo("https://www.bing.com/visualsearch") { UseShellExecute = true });
                Close();
            }
        }
        catch
        {
            Process.Start(new ProcessStartInfo("https://www.bing.com/visualsearch") { UseShellExecute = true });
            Close();
        }
    }

    private async void UploadToCloud()
    {
        if (!_hasSelection) return;

        ShowToast("Subiendo captura a la nube...");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);
            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);

            if (!string.IsNullOrEmpty(publicUrl))
            {
                Clipboard.SetText(publicUrl);
                ShowToast("¡Enlace copiado al portapapeles! ☁️");
                Process.Start(new ProcessStartInfo(publicUrl) { UseShellExecute = true });
            }
            else
            {
                ShowToast("⚠️ No se pudo completar la subida. Verifica tu conexión.");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Error de subida: {ex.Message}");
        }
    }

    private void AskGeminiAboutCapture()
    {
        if (!_hasSelection) return;

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            ClipboardHelper.CopyImageToClipboard(flattened);
            _historyService.SaveCapture(flattened, "Consulta Gemini");

            Process.Start(new ProcessStartInfo("https://gemini.google.com/app") { UseShellExecute = true });
            ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en Gemini ✨", "/Assets/Icons/gemini.png");
            App.ShowNotification("Google Gemini ✨", "¡Captura copiada! Pulsa Ctrl+V en la caja de Gemini para pegarla.");
            Close();
        }
        catch
        {
            Process.Start(new ProcessStartInfo("https://gemini.google.com/app") { UseShellExecute = true });
            Close();
        }
    }

    private void OpenGeminiWebChat()
    {
        if (!_hasSelection) return;

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            ClipboardHelper.CopyImageToClipboard(flattened);
            _historyService.SaveCapture(flattened, "Consulta Gemini Web");

            Process.Start(new ProcessStartInfo("https://gemini.google.com/app") { UseShellExecute = true });
            Close();
        }
        catch
        {
            Process.Start(new ProcessStartInfo("https://gemini.google.com/app") { UseShellExecute = true });
            Close();
        }
    }

    private async void SearchPerplexityAsync()
    {
        if (!_hasSelection) return;

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            ClipboardHelper.CopyImageToClipboard(flattened);

            int cropX = (int)Math.Round(_selectionBounds.X * ScaleX);
            int cropY = (int)Math.Round(_selectionBounds.Y * ScaleY);
            int cropW = (int)Math.Round(_selectionBounds.Width * ScaleX);
            int cropH = (int)Math.Round(_selectionBounds.Height * ScaleY);

            var cropped = _captureService.CropBitmap(_frozenScreen, cropX, cropY, cropW, cropH);
            var blocks = await _ocrService.RecognizeTextAsync(cropped);
            string ocrText = (blocks != null && blocks.Count > 0)
                ? string.Join(" ", blocks.Select(b => b.Text)).Trim()
                : string.Empty;

            string query = !string.IsNullOrWhiteSpace(ocrText) ? ocrText : "Análisis de captura";
            string perpUrl = $"https://www.perplexity.ai/search?q={Uri.EscapeDataString(query)}";
            Process.Start(new ProcessStartInfo(perpUrl) { UseShellExecute = true });
            Close();
        }
        catch
        {
            Process.Start(new ProcessStartInfo("https://www.perplexity.ai/") { UseShellExecute = true });
            Close();
        }
    }

    private async void CopyGeminiPromptToClipboard()
    {
        if (!_hasSelection) return;

        try
        {
            int cropX = (int)Math.Round(_selectionBounds.X * ScaleX);
            int cropY = (int)Math.Round(_selectionBounds.Y * ScaleY);
            int cropW = (int)Math.Round(_selectionBounds.Width * ScaleX);
            int cropH = (int)Math.Round(_selectionBounds.Height * ScaleY);

            var cropped = _captureService.CropBitmap(_frozenScreen, cropX, cropY, cropW, cropH);
            var blocks = await _ocrService.RecognizeTextAsync(cropped);
            string ocrText = (blocks != null && blocks.Count > 0)
                ? string.Join("\n", blocks.Select(b => b.Text)).Trim()
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                string prompt = $"Por favor analiza detalladamente, explica o asísteme con lo siguiente:\n\n\"{ocrText}\"";
                Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, prompt), true);
                ToastNotificationWindow.ShowToast("¡Prompt copiado! Pega con Ctrl+V en Gemini ✨", "/Assets/Icons/gemini.png");
                App.ShowNotification("Google Gemini ✨", "¡Prompt y texto copiados! Pulsa Ctrl+V en Gemini para pegarlos.");
            }
            else
            {
                var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
                ClipboardHelper.CopyImageToClipboard(flattened);
                ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en Gemini ✨", "/Assets/Icons/gemini.png");
                App.ShowNotification("Google Gemini ✨", "¡Captura copiada! Pulsa Ctrl+V en la caja de Gemini para pegarla.");
            }

            Process.Start(new ProcessStartInfo("https://gemini.google.com/app") { UseShellExecute = true });
            Close();
        }
        catch
        {
            Process.Start(new ProcessStartInfo("https://gemini.google.com/app") { UseShellExecute = true });
            Close();
        }
    }

    private async void SearchGeminiGoogleAsync()
    {
        if (!_hasSelection) return;

        try
        {
            int cropX = (int)Math.Round(_selectionBounds.X * ScaleX);
            int cropY = (int)Math.Round(_selectionBounds.Y * ScaleY);
            int cropW = (int)Math.Round(_selectionBounds.Width * ScaleX);
            int cropH = (int)Math.Round(_selectionBounds.Height * ScaleY);

            var cropped = _captureService.CropBitmap(_frozenScreen, cropX, cropY, cropW, cropH);
            var blocks = await _ocrService.RecognizeTextAsync(cropped);
            string ocrText = (blocks != null && blocks.Count > 0)
                ? string.Join(" ", blocks.Select(b => b.Text)).Trim()
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                string searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(ocrText)}";
                Process.Start(new ProcessStartInfo(searchUrl) { UseShellExecute = true });
                Close();
            }
            else
            {
                SearchOnGoogle();
            }
        }
        catch
        {
            SearchOnGoogle();
        }
    }

    private void ShareOnWhatsAppApp()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en WhatsApp (App)");

        bool appOpened = false;
        try
        {
            Process.Start(new ProcessStartInfo("whatsapp://") { UseShellExecute = true });
            appOpened = true;
        }
        catch { }

        if (appOpened)
        {
            ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en WhatsApp App 💬", "/Assets/Icons/whatsapp.png");
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://web.whatsapp.com/") { UseShellExecute = true });
            }
            catch { }
            ToastNotificationWindow.ShowToast("WhatsApp App no encontrado. Abriendo WhatsApp Web... 💬", "/Assets/Icons/whatsapp.png");
        }

        Close();
    }

    private void ShareOnWhatsAppWeb()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en WhatsApp Web");

        try
        {
            Process.Start(new ProcessStartInfo("https://web.whatsapp.com/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en WhatsApp Web 💬", "/Assets/Icons/whatsapp.png");
        Close();
    }

    private void ShareOnDiscord()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en Discord");

        try
        {
            Process.Start(new ProcessStartInfo("https://discord.com/app") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en Discord 💬", "pack://application:,,,/Assets/Icons/discord.png");
        Close();
    }

    private void ShareOnFacebook()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en Facebook");

        try
        {
            Process.Start(new ProcessStartInfo("https://www.facebook.com/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en Facebook 🔵", "pack://application:,,,/Assets/Icons/facebook.png");
        Close();
    }

    private void ShareOnTikTok()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en TikTok");

        try
        {
            Process.Start(new ProcessStartInfo("https://www.tiktok.com/upload") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en TikTok 🎵", "/Assets/Icons/tiktok.png");
        Close();
    }

    private void ShareOnInstagram()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en Instagram");

        try
        {
            Process.Start(new ProcessStartInfo("https://www.instagram.com/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en Instagram 📸", "pack://application:,,,/Assets/Icons/instagram.png");
        Close();
    }

    private void ShareByEmail()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);

        try
        {
            string tempDir = Path.GetTempPath();
            string tempPath = Path.Combine(tempDir, $"LightCapture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            using (var fs = File.Create(tempPath))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(flattened));
                encoder.Save(fs);
            }

            _historyService.SaveCapture(flattened, "Compartir por Correo");
            Process.Start(new ProcessStartInfo("mailto:?subject=Captura%20de%20pantalla%20-%20LightCapture&body=Adjunto%20captura%20de%20pantalla%20(tambi%C3%A9n%20copiada%20al%20portapapeles%20para%20pegar).") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Abriendo correo (Captura copiada al portapapeles) ✉️", "pack://application:,,,/Assets/Icons/email.png");
        Close();
    }

    private void ShareOnTelegram()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en Telegram");

        try
        {
            Process.Start(new ProcessStartInfo("https://web.telegram.org/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en Telegram ✈️", "pack://application:,,,/Assets/Icons/telegram.png");
        Close();
    }

    private void ShareOnTwitter()
    {
        if (!_hasSelection) return;

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Compartir en X");

        try
        {
            Process.Start(new ProcessStartInfo("https://twitter.com/intent/tweet") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("¡Captura copiada! Pega con Ctrl+V en tu publicación ✖️", "pack://application:,,,/Assets/Icons/x.png");
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _speechService.Stop();
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            CopyCaptureToClipboard();
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopyCaptureToClipboard();
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            CopyTranslatedTextToClipboard();
        }
        else if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _ = CopyRawOcrTextToClipboardAsync();
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveCaptureToFile();
        }
        else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _ = ExecuteTranslationAsync();
        }
        else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        {
            AskGeminiAboutCapture();
        }
        else if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PinCaptureToScreen();
        }
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
        {
            PerformUndo();
        }
    }

    private void PerformUndo()
    {
        CommitFloatingText();
        bool undone = MainDrawingCanvas.Undo();
        if (undone)
        {
            ShowToast("↩️ Deshecho (Ctrl+Z)");
        }
        else
        {
            ShowToast("No hay trazos que deshacer");
        }
    }

    private DispatcherTimer? _toastTimer;
    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastNotification.Visibility = Visibility.Visible;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _toastTimer.Tick += (s, e) =>
        {
            ToastNotification.Visibility = Visibility.Collapsed;
            _toastTimer.Stop();
        };
        _toastTimer.Start();
    }
}
