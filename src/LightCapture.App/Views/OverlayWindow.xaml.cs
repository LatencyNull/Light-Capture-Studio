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

    private enum ResizeDirection
    {
        None,
        NW, N, NE,
        W,      E,
        SW, S, SE
    }

    private bool _isResizing;
    private ResizeDirection _activeResizeHandle = ResizeDirection.None;
    private Point _resizeStartMouse;
    private Rect _resizeStartBounds;

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
                ShowToast("🔤 Click on capture to write");
            }
            else
            {
                ShowToast($"Tool: {tool.ToString().ToUpper()}");
            }
        };

        MainToolbar.ColorSelected += color =>
        {
            MainDrawingCanvas.CurrentColor = color;
            if (FloatingTextContainer.Visibility == Visibility.Visible)
            {
                FloatingInputBox.Foreground = new SolidColorBrush(color);
            }
        };

        MainToolbar.UndoRequested += PerformUndo;

        MainToolbar.TranslateRequested += async () => await OnTranslateButtonClickedAsync();
        MainToolbar.TranslateTextCopyRequested += CopyTranslatedTextToClipboard;
        MainToolbar.CopyOcrTextRequested += async () => await CopyRawOcrTextToClipboardAsync();
        MainToolbar.TargetLanguageChanged += async (lang) => await OnTargetLanguageChangedAsync(lang);

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
            // Comprobar si el usuario hizo clic en uno de los 8 tiradores de redimensionado (esquinas y bordes)
            if (MainDrawingCanvas.CurrentTool == DrawingToolType.None)
            {
                var resizeDir = GetResizeHandleAtPoint(p);
                if (resizeDir != ResizeDirection.None)
                {
                    CommitFloatingText();
                    _isResizing = true;
                    _activeResizeHandle = resizeDir;
                    _resizeStartMouse = p;
                    _resizeStartBounds = _selectionBounds;
                    CaptureMouse();
                    return;
                }
            }

            if (_selectionBounds.Contains(p))
            {
                if (MainDrawingCanvas.CurrentTool != DrawingToolType.None)
                {
                    if (MainDrawingCanvas.CurrentTool == DrawingToolType.Text)
                    {
                        // Si ya hay texto activo en el contenedor, comprobar si el clic es dentro del contenedor
                        if (FloatingTextContainer.Visibility == Visibility.Visible)
                        {
                            double boxLeft = Canvas.GetLeft(FloatingTextContainer);
                            double boxTop = Canvas.GetTop(FloatingTextContainer);
                            Rect boxRect = new Rect(boxLeft, boxTop, FloatingTextContainer.ActualWidth > 0 ? FloatingTextContainer.ActualWidth : FloatingTextContainer.Width, FloatingTextContainer.ActualHeight > 0 ? FloatingTextContainer.ActualHeight : FloatingTextContainer.Height);
                            if (boxRect.Contains(p))
                            {
                                return; // Clic dentro del contenedor, dejar interactuar
                            }
                        }

                        // Si hacemos clic sobre un texto ya existente, permitir re-estirarlo y editarlo
                        var existingText = MainDrawingCanvas.Elements.OfType<TextDrawingElement>().LastOrDefault(t => t.GetBounds().Contains(p));
                        if (existingText != null)
                        {
                            CommitFloatingText();
                            MainDrawingCanvas.RemoveElement(existingText);
                            ShowFloatingInputAt(existingText.Position, existingText.Text, existingText.FontSize);
                            return;
                        }

                        CommitFloatingText();
                        ShowFloatingInputAt(p, "", _currentTextFontSize);
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
        ShowToast("🧲 Window auto-selected");
    }

    private Point _floatingInputPos;
    private double _currentTextFontSize = 20;
    private bool _isStretchingText = false;
    private Point _textStretchStartMouse;
    private double _textStretchStartWidth;
    private double _textStretchStartHeight;

    private void ShowFloatingInputAt(Point p, string initialText = "", double initialFontSize = 20)
    {
        _floatingInputPos = p;
        _currentTextFontSize = Math.Max(12, initialFontSize);

        double defaultWidth = Math.Max(120, Math.Min(300, _selectionBounds.Width - 10));
        double defaultHeight = Math.Max(34, _currentTextFontSize * 1.8);

        double inputX = Math.Max(_selectionBounds.Left, Math.Min(_selectionBounds.Right - defaultWidth, p.X));
        double inputY = Math.Max(_selectionBounds.Top, Math.Min(_selectionBounds.Bottom - defaultHeight, p.Y));

        Canvas.SetLeft(FloatingTextContainer, inputX);
        Canvas.SetTop(FloatingTextContainer, inputY);
        FloatingTextContainer.Width = defaultWidth;
        FloatingTextContainer.Height = defaultHeight;

        FloatingInputBox.Foreground = new SolidColorBrush(MainDrawingCanvas.CurrentColor);
        FloatingInputBox.FontSize = _currentTextFontSize;
        FloatingInputBox.Text = initialText;

        FloatingTextContainer.Visibility = Visibility.Visible;
        FloatingInputBox.Focus();
        if (!string.IsNullOrEmpty(initialText))
        {
            FloatingInputBox.SelectAll();
        }
    }

    private void CommitFloatingText()
    {
        if (FloatingTextContainer.Visibility != Visibility.Visible) return;

        string text = FloatingInputBox.Text.Trim();
        FloatingTextContainer.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrEmpty(text))
        {
            double posX = Canvas.GetLeft(FloatingTextContainer) + 3;
            double posY = Canvas.GetTop(FloatingTextContainer) + 1;
            MainDrawingCanvas.AddTextElement(new Point(posX, posY), text, _currentTextFontSize);
            ShowToast($"✍️ Text added ({_currentTextFontSize:0} pt)");
        }
    }

    private bool _isMovingText = false;
    private Point _textMoveStartMouse;
    private Point _textMoveStartPos;

    private void BtnDragText_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isMovingText = true;
            _textMoveStartMouse = e.GetPosition(this);
            _textMoveStartPos = new Point(Canvas.GetLeft(FloatingTextContainer), Canvas.GetTop(FloatingTextContainer));
            BtnDragText.CaptureMouse();
            e.Handled = true;
        }
    }

    private void BtnDragText_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isMovingText && e.LeftButton == MouseButtonState.Pressed)
        {
            Point current = e.GetPosition(this);
            double newX = _textMoveStartPos.X + (current.X - _textMoveStartMouse.X);
            double newY = _textMoveStartPos.Y + (current.Y - _textMoveStartMouse.Y);
            Canvas.SetLeft(FloatingTextContainer, newX);
            Canvas.SetTop(FloatingTextContainer, newY);
            e.Handled = true;
        }
    }

    private void BtnDragText_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isMovingText)
        {
            _isMovingText = false;
            BtnDragText.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void BtnConfirmText_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CommitFloatingText();
    }

    private void BtnCancelText_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        FloatingInputBox.Text = string.Empty;
        FloatingTextContainer.Visibility = Visibility.Collapsed;
    }

    private void FloatingInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                // Shift + Enter -> permitir salto de línea
                return;
            }
            // Enter normal -> CONFIRMAR Y AÑADIR AL LIENZO
            e.Handled = true;
            CommitFloatingText();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            FloatingInputBox.Text = string.Empty;
            FloatingTextContainer.Visibility = Visibility.Collapsed;
        }
    }

    private void FloatingInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_isStretchingText)
        {
            CommitFloatingText();
        }
    }

    private void FloatingInputBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        double delta = e.Delta > 0 ? 2.0 : -2.0;
        double newSize = Math.Max(10, Math.Min(120, _currentTextFontSize + delta));
        _currentTextFontSize = newSize;
        FloatingInputBox.FontSize = newSize;

        double minH = Math.Max(32, newSize * 1.8);
        if (FloatingTextContainer.Height < minH)
        {
            FloatingTextContainer.Height = minH;
        }
    }

    private void FloatingTextResizeGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isStretchingText = true;
            _textStretchStartMouse = e.GetPosition(this);
            _textStretchStartWidth = FloatingTextContainer.ActualWidth > 0 ? FloatingTextContainer.ActualWidth : FloatingTextContainer.Width;
            _textStretchStartHeight = FloatingTextContainer.ActualHeight > 0 ? FloatingTextContainer.ActualHeight : FloatingTextContainer.Height;
            FloatingTextResizeGrip.CaptureMouse();
            e.Handled = true;
        }
    }

    private void FloatingTextResizeGrip_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isStretchingText && e.LeftButton == MouseButtonState.Pressed)
        {
            Point current = e.GetPosition(this);
            double deltaX = current.X - _textStretchStartMouse.X;
            double deltaY = current.Y - _textStretchStartMouse.Y;

            double newW = Math.Max(80, _textStretchStartWidth + deltaX);
            double newH = Math.Max(28, _textStretchStartHeight + deltaY);

            FloatingTextContainer.Width = newW;
            FloatingTextContainer.Height = newH;

            // Escalar el tamaño del texto proporcionalmente al alto del cuadro (Free Transform estilo Photoshop)
            double newSize = Math.Max(10, Math.Min(120, Math.Round(newH * 0.55)));
            _currentTextFontSize = newSize;
            FloatingInputBox.FontSize = newSize;

            e.Handled = true;
        }
    }

    private void FloatingTextResizeGrip_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isStretchingText)
        {
            _isStretchingText = false;
            FloatingTextResizeGrip.ReleaseMouseCapture();
            FloatingInputBox.Focus();
            e.Handled = true;
        }
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
        else if (_isResizing)
        {
            double dx = current.X - _resizeStartMouse.X;
            double dy = current.Y - _resizeStartMouse.Y;

            double left = _resizeStartBounds.Left;
            double top = _resizeStartBounds.Top;
            double right = _resizeStartBounds.Right;
            double bottom = _resizeStartBounds.Bottom;

            switch (_activeResizeHandle)
            {
                case ResizeDirection.NW: left += dx; top += dy; break;
                case ResizeDirection.N:  top += dy; break;
                case ResizeDirection.NE: right += dx; top += dy; break;
                case ResizeDirection.E:  right += dx; break;
                case ResizeDirection.SE: right += dx; bottom += dy; break;
                case ResizeDirection.S:  bottom += dy; break;
                case ResizeDirection.SW: left += dx; bottom += dy; break;
                case ResizeDirection.W:  left += dx; break;
            }

            double screenW = Width;
            double screenH = Height;

            double finalX = Math.Max(0, Math.Min(left, right));
            double finalY = Math.Max(0, Math.Min(top, bottom));
            double finalW = Math.Max(15, Math.Abs(right - left));
            double finalH = Math.Max(15, Math.Abs(bottom - top));

            if (finalX + finalW > screenW) finalW = Math.Max(15, screenW - finalX);
            if (finalY + finalH > screenH) finalH = Math.Max(15, screenH - finalY);

            _selectionBounds = new Rect(finalX, finalY, finalW, finalH);

            UpdateSelectionVisuals(_selectionBounds);
            UpdateDimming(_selectionBounds);
            MainToolbar.UpdatePosition(_selectionBounds, Width, Height);
            return;
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

        if (_hasSelection && !_isSelecting && !_isDraggingSelection && !_isResizing && MainDrawingCanvas.CurrentTool == DrawingToolType.None)
        {
            var resizeDir = GetResizeHandleAtPoint(current);
            Cursor = resizeDir switch
            {
                ResizeDirection.NW or ResizeDirection.SE => System.Windows.Input.Cursors.SizeNWSE,
                ResizeDirection.NE or ResizeDirection.SW => System.Windows.Input.Cursors.SizeNESW,
                ResizeDirection.N or ResizeDirection.S => System.Windows.Input.Cursors.SizeNS,
                ResizeDirection.W or ResizeDirection.E => System.Windows.Input.Cursors.SizeWE,
                _ => _selectionBounds.Contains(current) ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Cross
            };
        }
        else if (_hasSelection && MainDrawingCanvas.CurrentTool == DrawingToolType.Text)
        {
            Cursor = _selectionBounds.Contains(current) ? System.Windows.Input.Cursors.IBeam : System.Windows.Input.Cursors.Arrow;
        }
        else if (!_isSelecting && !_isDraggingSelection && !_isResizing && !_hasSelection)
        {
            Cursor = System.Windows.Input.Cursors.Arrow;
        }
    }

    private void Window_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        Point p = e.GetPosition(this);

        if (_isResizing)
        {
            _isResizing = false;
            _activeResizeHandle = ResizeDirection.None;
            ReleaseMouseCapture();

            UpdateSelectionVisuals(_selectionBounds);
            UpdateDimming(_selectionBounds);
            UpdateHandlesPosition(_selectionBounds);
            HandlesLayer.Visibility = Visibility.Visible;
            DimensionTag.Visibility = Visibility.Visible;
            MainToolbar.UpdatePosition(_selectionBounds, Width, Height);
            MainDrawingCanvas.ActiveSelectionBounds = _selectionBounds;
            CheckForQrCodeAsync();
            return;
        }

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
        double tagX = Math.Max(6, Math.Min(r.X, Width - 80));
        double tagY = r.Y > 26 ? r.Y - 24 : r.Y + 6;
        Canvas.SetLeft(DimensionTag, tagX);
        Canvas.SetTop(DimensionTag, tagY);

        UpdateHandlesPosition(r);
        UpdateQrBannerPosition();
    }

    private void UpdateHandlesPosition(Rect r)
    {
        const double half = 4.0;
        Canvas.SetLeft(HandleNW, r.X - half); Canvas.SetTop(HandleNW, r.Y - half);
        Canvas.SetLeft(HandleN, r.X + (r.Width / 2) - half); Canvas.SetTop(HandleN, r.Y - half);
        Canvas.SetLeft(HandleNE, r.Right - half); Canvas.SetTop(HandleNE, r.Y - half);

        Canvas.SetLeft(HandleW, r.X - half); Canvas.SetTop(HandleW, r.Y + (r.Height / 2) - half);
        Canvas.SetLeft(HandleE, r.Right - half); Canvas.SetTop(HandleE, r.Y + (r.Height / 2) - half);

        Canvas.SetLeft(HandleSW, r.X - half); Canvas.SetTop(HandleSW, r.Bottom - half);
        Canvas.SetLeft(HandleS, r.X + (r.Width / 2) - half); Canvas.SetTop(HandleS, r.Bottom - half);
        Canvas.SetLeft(HandleSE, r.Right - half); Canvas.SetTop(HandleSE, r.Bottom - half);
    }

    private ResizeDirection GetResizeHandleAtPoint(Point p)
    {
        if (!_hasSelection || HandlesLayer.Visibility != Visibility.Visible) return ResizeDirection.None;

        Rect r = _selectionBounds;
        const double tol = 12.0;

        Point nw = new Point(r.Left, r.Top);
        Point ne = new Point(r.Right, r.Top);
        Point se = new Point(r.Right, r.Bottom);
        Point sw = new Point(r.Left, r.Bottom);

        Point n = new Point(r.Left + r.Width / 2.0, r.Top);
        Point s = new Point(r.Left + r.Width / 2.0, r.Bottom);
        Point w = new Point(r.Left, r.Top + r.Height / 2.0);
        Point e = new Point(r.Right, r.Top + r.Height / 2.0);

        // Esquinas prioritarias
        if (Math.Abs(p.X - nw.X) <= tol && Math.Abs(p.Y - nw.Y) <= tol) return ResizeDirection.NW;
        if (Math.Abs(p.X - ne.X) <= tol && Math.Abs(p.Y - ne.Y) <= tol) return ResizeDirection.NE;
        if (Math.Abs(p.X - se.X) <= tol && Math.Abs(p.Y - se.Y) <= tol) return ResizeDirection.SE;
        if (Math.Abs(p.X - sw.X) <= tol && Math.Abs(p.Y - sw.Y) <= tol) return ResizeDirection.SW;

        // Bordes cardinales
        if (Math.Abs(p.X - n.X) <= tol && Math.Abs(p.Y - n.Y) <= tol) return ResizeDirection.N;
        if (Math.Abs(p.X - s.X) <= tol && Math.Abs(p.Y - s.Y) <= tol) return ResizeDirection.S;
        if (Math.Abs(p.X - w.X) <= tol && Math.Abs(p.Y - w.Y) <= tol) return ResizeDirection.W;
        if (Math.Abs(p.X - e.X) <= tol && Math.Abs(p.Y - e.Y) <= tol) return ResizeDirection.E;

        return ResizeDirection.None;
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
                ShowToast("QR Code detected in capture!");
            }
        }
        catch { }
    }

    private void BtnQrCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_detectedQrCode))
        {
            Clipboard.SetText(_detectedQrCode);
            ShowToast("QR content copied to clipboard 📋");
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
            ShowToast("Click Translate or Copy Text before listening.");
            return;
        }

        _speechService.Speak(textToSpeak);
        ShowToast("🔊 Reading text aloud...");
    }

    private void PinCaptureToScreen()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        _historyService.SaveCapture(flattened, "Pinned on screen " + _lastTranslatedPlainText + " " + _lastExtractedOcrText);
        var pinWindow = new PinnedCaptureWindow(flattened);
        pinWindow.Show();
        Close();
    }

    private async Task CopyRawOcrTextToClipboardAsync()
    {
        if (!_hasSelection) return;

        ShowToast("Extracting screen text (OCR)...");

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
                ShowToast("No text detected in selected area.");
                return;
            }

            _lastExtractedOcrText = string.Join("\n", blocks.Select(b => b.Text));
            Clipboard.SetText(_lastExtractedOcrText);

            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            _historyService.SaveCapture(flattened, _lastExtractedOcrText);

            ShowToast("Extracted text copied to clipboard 📋");
        }
        catch (Exception ex)
        {
            ShowToast($"OCR Error: {ex.Message}");
        }
    }

    private async Task OnTranslateButtonClickedAsync()
    {
        if (!_hasSelection) return;

        // If already translated, toggle between Original and Translation
        if (MainDrawingCanvas.HasTranslatedBlocks)
        {
            MainDrawingCanvas.ToggleTranslationVisibility();
            bool showingTranslation = MainDrawingCanvas.ShowTranslations;
            MainToolbar.SetTranslateActiveState(true, showingTranslation);

            if (showingTranslation)
            {
                ShowToast("👁️ Showing translation (Click again to view original)");
            }
            else
            {
                ShowToast("🔄 Showing original text (Click again to translate)");
            }
            return;
        }

        await ExecuteTranslationAsync();
    }

    private async Task OnTargetLanguageChangedAsync(string newLang)
    {
        string langName = TranslationLanguageManager.CurrentLanguageDisplayName;

        if (MainDrawingCanvas.HasTranslatedBlocks)
        {
            ShowToast($"🌐 Changing translation to {langName}...");
            MainDrawingCanvas.ClearTranslations();
            MainToolbar.SetTranslateActiveState(false, false);
            await ExecuteTranslationAsync();
        }
        else
        {
            ShowToast($"🌐 Target language: {langName}");
        }
    }

    private async Task ExecuteTranslationAsync()
    {
        if (!_hasSelection) return;

        ShowToast("Extracting text with OCR...");

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
                ShowToast("No text detected in selection.");
                return;
            }

            string targetLang = TranslationLanguageManager.CurrentLanguage;
            string targetName = TranslationLanguageManager.CurrentLanguageDisplayName;

            ShowToast($"Translating to {targetName} ({blocks.Count} blocks in parallel)...");

            var tasks = blocks.Select(async block =>
            {
                var tr = await _translationService.TranslateAsync(block.Text, targetLang: targetLang, sourceLang: "auto");
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
                ShowToast($"Translation to {targetName} ready! ({successCount}/{blocks.Count}) - Click to toggle original");
            }
            else
            {
                ShowToast("⚠️ Could not retrieve translation from server.");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Translation error: {ex.Message}");
        }
    }

    private void CopyCaptureToClipboard()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);

        _historyService.SaveCapture(flattened, _lastTranslatedPlainText + " " + _lastExtractedOcrText);

        ToastNotificationWindow.ShowToast("Capture copied to clipboard 📑", "pack://application:,,,/Assets/Icons/copy.png");
        Close();
    }

    private void CopyTranslatedTextToClipboard()
    {
        if (string.IsNullOrWhiteSpace(_lastTranslatedPlainText))
        {
            ShowToast("No translated text yet. Click Translate first.");
            return;
        }

        Clipboard.SetText(_lastTranslatedPlainText);

        if (_hasSelection)
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            _historyService.SaveCapture(flattened, "Translation: " + _lastTranslatedPlainText);
        }

        ShowToast("Translated text copied to clipboard 📋");
    }

    private void SaveCaptureToFile()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);

        var sfd = new SaveFileDialog
        {
            InitialDirectory = _historyService.HistoryDirectory,
            Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
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

            ShowToast("Image saved and indexed in history");
            Close();
        }
    }

    private void PrintCapture()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

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
            ShowToast("Selected area is too small to record.");
            return;
        }

        int pxX = (int)Math.Round((Left + _selectionBounds.Left) * ScaleX);
        int pxY = (int)Math.Round((Top + _selectionBounds.Top) * ScaleY);
        int pxW = (int)Math.Round(_selectionBounds.Width * ScaleX);
        int pxH = (int)Math.Round(_selectionBounds.Height * ScaleY);

        var dipBounds = new Rect(Left + _selectionBounds.Left, Top + _selectionBounds.Top, _selectionBounds.Width, _selectionBounds.Height);

        // Hide frozen screenshot window to keep desktop fully interactive
        Visibility = Visibility.Collapsed;

        _recordingBorder = new RecordingBorderOverlay(dipBounds);
        _recordingBorder.Show();

        _recordingWidget = new RecordingControlWidget(captureAudio, captureMic, dipBounds, isGif);
        _recordingWidget.AudioToggled += enabled => _videoRecorder.SetAudioEnabled(enabled);
        _recordingWidget.MicToggled += enabled => _videoRecorder.SetMicEnabled(enabled);

        // User clicks "Start" in recording control widget
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
                System.Windows.MessageBox.Show($"Could not start recording: {ex.Message}", "Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
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
                System.Windows.MessageBox.Show($"Recording error: {err}", "Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
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
            System.Windows.MessageBox.Show($"Error processing file: {ex.Message}");
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
        CommitFloatingText();

        _isSearchingLens = true;
        ShowToast("Searching Google Lens... 🔍");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);

            try { ClipboardHelper.CopyImageToClipboard(flattened); } catch { }

            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);

            if (!string.IsNullOrEmpty(publicUrl))
            {
                _historyService.SaveCapture(flattened, "Google Lens Search");
                string lensUrl = GoogleLensService.BuildLensSearchUrl(publicUrl);
                Process.Start(new ProcessStartInfo(lensUrl) { UseShellExecute = true });
                ShowToast("Search opened in Google Lens! 🔍");
                Close();
            }
            else
            {
                Process.Start(new ProcessStartInfo("https://lens.google.com/") { UseShellExecute = true });
                _historyService.SaveCapture(flattened, "Google Lens Search");
                ShowToast("Capture on clipboard. Paste (Ctrl+V) into Google Lens 📋");
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

        ShowToast("Searching Yandex Images... 🟡");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);
            try { ClipboardHelper.CopyImageToClipboard(flattened); } catch { }

            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);
            if (!string.IsNullOrEmpty(publicUrl))
            {
                _historyService.SaveCapture(flattened, "Yandex Images Search");
                string yandexUrl = $"https://yandex.com/images/search?rpt=imageview&url={Uri.EscapeDataString(publicUrl)}";
                Process.Start(new ProcessStartInfo(yandexUrl) { UseShellExecute = true });
                ShowToast("Search opened in Yandex Images! 🟡");
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

        ShowToast("Searching Bing Visual Search... 🟦");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);
            try { ClipboardHelper.CopyImageToClipboard(flattened); } catch { }

            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);
            if (!string.IsNullOrEmpty(publicUrl))
            {
                _historyService.SaveCapture(flattened, "Bing Visual Search");
                string bingUrl = $"https://www.bing.com/images/search?view=detailv2&iss=sbi&FORM=SBIHMP&sbisrc=UrlPaste&q=imgurl:{Uri.EscapeDataString(publicUrl)}";
                Process.Start(new ProcessStartInfo(bingUrl) { UseShellExecute = true });
                ShowToast("Search opened in Bing! 🟦");
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

        ShowToast("Uploading capture to cloud...");

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            var pngBytes = ScreenCaptureService.BitmapSourceToPngBytes(flattened);
            string? publicUrl = await _cloudUploadService.UploadImageAsync(pngBytes);

            if (!string.IsNullOrEmpty(publicUrl))
            {
                Clipboard.SetText(publicUrl);
                ShowToast("Link copied to clipboard! ☁️");
                Process.Start(new ProcessStartInfo(publicUrl) { UseShellExecute = true });
            }
            else
            {
                ShowToast("⚠️ Could not complete upload. Check your connection.");
            }
        }
        catch (Exception ex)
        {
            ShowToast($"Upload error: {ex.Message}");
        }
    }

    private void AskGeminiAboutCapture()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        try
        {
            var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
            ClipboardHelper.CopyImageToClipboard(flattened);
            _historyService.SaveCapture(flattened, "Gemini Query");

            Process.Start(new ProcessStartInfo("https://gemini.google.com/app") { UseShellExecute = true });
            ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in Gemini ✨", "/Assets/Icons/gemini.png");
            App.ShowNotification("Google Gemini ✨", "Capture copied! Press Ctrl+V in Gemini to paste.");
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
            _historyService.SaveCapture(flattened, "Gemini Web Query");

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

            string query = !string.IsNullOrWhiteSpace(ocrText) ? ocrText : "Capture Analysis";
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
                string prompt = $"Please analyze in detail, explain or assist me with the following:\n\n\"{ocrText}\"";
                Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, prompt), true);
                ToastNotificationWindow.ShowToast("Prompt copied! Paste with Ctrl+V in Gemini ✨", "/Assets/Icons/gemini.png");
                App.ShowNotification("Google Gemini ✨", "Prompt and text copied! Press Ctrl+V in Gemini to paste.");
            }
            else
            {
                var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
                ClipboardHelper.CopyImageToClipboard(flattened);
                ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in Gemini ✨", "/Assets/Icons/gemini.png");
                App.ShowNotification("Google Gemini ✨", "Capture copied! Press Ctrl+V in Gemini to paste.");
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
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on WhatsApp (App)");

        bool appOpened = false;
        try
        {
            Process.Start(new ProcessStartInfo("whatsapp://") { UseShellExecute = true });
            appOpened = true;
        }
        catch { }

        if (appOpened)
        {
            ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in WhatsApp App 💬", "/Assets/Icons/whatsapp.png");
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://web.whatsapp.com/") { UseShellExecute = true });
            }
            catch { }
            ToastNotificationWindow.ShowToast("WhatsApp App not found. Opening WhatsApp Web... 💬", "/Assets/Icons/whatsapp.png");
        }

        Close();
    }

    private void ShareOnWhatsAppWeb()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on WhatsApp Web");

        try
        {
            Process.Start(new ProcessStartInfo("https://web.whatsapp.com/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in WhatsApp Web 💬", "/Assets/Icons/whatsapp.png");
        Close();
    }

    private void ShareOnDiscord()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on Discord");

        try
        {
            Process.Start(new ProcessStartInfo("https://discord.com/app") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in Discord 💬", "pack://application:,,,/Assets/Icons/discord.png");
        Close();
    }

    private void ShareOnFacebook()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on Facebook");

        try
        {
            Process.Start(new ProcessStartInfo("https://www.facebook.com/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in Facebook 🔵", "pack://application:,,,/Assets/Icons/facebook.png");
        Close();
    }

    private void ShareOnTikTok()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on TikTok");

        try
        {
            Process.Start(new ProcessStartInfo("https://www.tiktok.com/upload") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in TikTok 🎵", "/Assets/Icons/tiktok.png");
        Close();
    }

    private void ShareOnInstagram()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on Instagram");

        try
        {
            Process.Start(new ProcessStartInfo("https://www.instagram.com/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in Instagram 📸", "pack://application:,,,/Assets/Icons/instagram.png");
        Close();
    }

    private void ShareByEmail()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

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

            _historyService.SaveCapture(flattened, "Share via Email");
            Process.Start(new ProcessStartInfo("mailto:?subject=Screenshot%20-%20LightCapture&body=Attached%20screenshot%20(also%20copied%20to%20clipboard%20to%20paste).") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Opening email (Capture copied to clipboard) ✉️", "pack://application:,,,/Assets/Icons/email.png");
        Close();
    }

    private void ShareOnTelegram()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on Telegram");

        try
        {
            Process.Start(new ProcessStartInfo("https://web.telegram.org/") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V in Telegram ✈️", "pack://application:,,,/Assets/Icons/telegram.png");
        Close();
    }

    private void ShareOnTwitter()
    {
        if (!_hasSelection) return;
        CommitFloatingText();

        var flattened = MainDrawingCanvas.RenderFlattenedBitmap(_frozenScreen, _selectionBounds);
        ClipboardHelper.CopyImageToClipboard(flattened);
        _historyService.SaveCapture(flattened, "Share on X");

        try
        {
            Process.Start(new ProcessStartInfo("https://twitter.com/intent/tweet") { UseShellExecute = true });
        }
        catch { }

        ToastNotificationWindow.ShowToast("Capture copied! Paste with Ctrl+V into your post ✖️", "pack://application:,,,/Assets/Icons/x.png");
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
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ApplyWindowSnap(new Rect(0, 0, Width, Height));
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
            _ = OnTranslateButtonClickedAsync();
        }
        else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            string next = TranslationLanguageManager.ToggleLanguage();
            MainToolbar.UpdateLanguageUi();
            _ = OnTargetLanguageChangedAsync(next);
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
            ShowToast("↩️ Undo (Ctrl+Z)");
        }
        else
        {
            ShowToast("Nothing to undo");
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
