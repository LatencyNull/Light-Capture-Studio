using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using LightCapture.App.Services;
using LightCapture.App.Views;
using LightCapture.Core.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;
using WpfPath = System.Windows.Shapes.Path;
using WpfImage = System.Windows.Controls.Image;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using PlacementMode = System.Windows.Controls.Primitives.PlacementMode;

namespace LightCapture.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _wakeUpEvent;
    private NotifyIcon? _notifyIcon;
    private HotkeyManager? _hotkeyManager;
    private HwndSource? _messageHwndSource;

    private Window? _trayHelperWindow;
    private WpfContextMenu? _trayContextMenu;
    private WpfMenuItem? _menuItemTheme;
    private WpfMenuItem? _menuLangEs;
    private WpfMenuItem? _menuLangEn;
    private WpfTextBlock? _checkLangEs;
    private WpfTextBlock? _checkLangEn;
    private WpfPath? _themeIconPath;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_NULL = 0x0000;

    private readonly ICaptureService _captureService = new ScreenCaptureService();
    private readonly IOcrService _ocrService = new WindowsOcrService();
    private readonly ITranslationService _translationService = new GoogleTranslationService();
    private readonly ICaptureHistoryService _historyService = new CaptureHistoryService();

    private OverlayWindow? _currentOverlay;
    private HistoryWindow? _currentHistoryWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeManager.Initialize();
        EnsureNativeDependencies();

        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Ocurrió un error inesperado:\n{args.Exception}", "Light Capture Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"Ocurrió un error inesperado:\n{args.ExceptionObject}", "Light Capture Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        _singleInstanceMutex = new Mutex(true, "LightCaptureStudio_SingleInstance_Mutex", out bool createdNew);

        if (!createdNew)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting("LightCaptureStudio_WakeUp_Event", out var existingEvent))
                {
                    existingEvent.Set();
                }
            }
            catch { }

            Shutdown();
            return;
        }

        try
        {
            _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "LightCaptureStudio_WakeUp_Event");
            var wakeUpThread = new Thread(() =>
            {
                while (_wakeUpEvent != null)
                {
                    try
                    {
                        if (_wakeUpEvent.WaitOne())
                        {
                            Dispatcher.BeginInvoke(new Action(() => StartCapture()));
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            })
            {
                IsBackground = true
            };
            wakeUpThread.Start();
        }
        catch { }

        SetupTrayIcon();
        SetupGlobalHotkeys();
    }

    private void SetupTrayIcon()
    {
        Icon appIcon;
        try
        {
            using var resStream = typeof(App).Assembly.GetManifestResourceStream("LightCapture.App.app.ico");
            if (resStream != null)
            {
                appIcon = new Icon(resStream);
            }
            else
            {
                string icoFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                if (File.Exists(icoFile))
                {
                    appIcon = new Icon(icoFile);
                }
                else
                {
                    string? exePath = Environment.ProcessPath;
                    appIcon = (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        ? (Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application)
                        : SystemIcons.Application;
                }
            }
        }
        catch
        {
            appIcon = SystemIcons.Application;
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Light Capture Studio - F9 para capturar pantalla",
            Visible = true,
            ContextMenuStrip = null // Reemplazado por el menú flotante moderno WPF One UI
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                StartCapture();
            }
            else if (e.Button == MouseButtons.Right)
            {
                ShowTrayContextMenu();
            }
        };

        _notifyIcon.BalloonTipClicked += (s, e) => StartCapture();
    }

    private void ShowTrayContextMenu()
    {
        if (_trayHelperWindow == null)
        {
            _trayHelperWindow = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Left = -10000,
                Top = -10000,
                Visibility = Visibility.Hidden
            };
            _trayHelperWindow.Show();
            _trayHelperWindow.Hide();

            _trayHelperWindow.Deactivated += (s, e) =>
            {
                if (_trayContextMenu != null && _trayContextMenu.IsOpen)
                {
                    _trayContextMenu.IsOpen = false;
                }
            };
        }

        if (_trayContextMenu == null)
        {
            BuildTrayContextMenu();
        }

        UpdateTrayMenuItems();

        _trayHelperWindow.Show();
        _trayHelperWindow.Activate();
        var helper = new WindowInteropHelper(_trayHelperWindow);
        SetForegroundWindow(helper.Handle);

        if (_trayContextMenu != null)
        {
            _trayContextMenu.PlacementTarget = _trayHelperWindow;
            _trayContextMenu.Placement = PlacementMode.MousePoint;
            _trayContextMenu.IsOpen = true;
        }
    }

    private void BuildTrayContextMenu()
    {
        _trayContextMenu = new WpfContextMenu
        {
            Style = (Style)FindResource("ModernContextMenuStyle")
        };

        _trayContextMenu.Closed += (s, e) =>
        {
            if (_trayHelperWindow != null)
            {
                _trayHelperWindow.Hide();
                var helper = new WindowInteropHelper(_trayHelperWindow);
                PostMessage(helper.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            }
        };

        // 1. Header elegante con nombre y versión
        var headerBorder = new System.Windows.Controls.Border
        {
            Padding = new Thickness(8, 4, 8, 6),
            BorderBrush = (System.Windows.Media.Brush)FindResource("MenuSeparator"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var headerGrid = new System.Windows.Controls.Grid();
        headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        var headerTitle = new WpfTextBlock
        {
            Text = "Light Capture Studio",
            FontWeight = FontWeights.Bold,
            FontSize = 12,
            Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText")
        };
        var headerBadge = new WpfTextBlock
        {
            Text = "v2.0",
            FontSize = 10,
            Foreground = (System.Windows.Media.Brush)FindResource("SecondaryText"),
            VerticalAlignment = VerticalAlignment.Center
        };
        System.Windows.Controls.Grid.SetColumn(headerTitle, 0);
        System.Windows.Controls.Grid.SetColumn(headerBadge, 1);
        headerGrid.Children.Add(headerTitle);
        headerGrid.Children.Add(headerBadge);
        headerBorder.Child = headerGrid;
        _trayContextMenu.Items.Add(headerBorder);

        // 2. 📸 Capturar pantalla (F9)
        var captureItem = new WpfMenuItem
        {
            Header = "Capturar pantalla",
            InputGestureText = "F9",
            FontWeight = FontWeights.SemiBold
        };
        var camPath = new WpfPath
        {
            Data = System.Windows.Media.Geometry.Parse("M4 4h3l2-2h6l2 2h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zm8 3a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0 2a3 3 0 1 1 0 6 3 3 0 0 1 0-6z"),
            Fill = (System.Windows.Media.Brush)FindResource("PrimaryText"),
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 16,
            Height = 16
        };
        captureItem.Icon = camPath;
        captureItem.Click += (s, e) => StartCapture();
        _trayContextMenu.Items.Add(captureItem);

        // 3. 🕒 Historial de capturas...
        var historyItem = new WpfMenuItem
        {
            Header = "Historial de capturas..."
        };
        var historyPath = new WpfPath
        {
            Data = System.Windows.Media.Geometry.Parse("M12 2a10 10 0 1 0 10 10A10 10 0 0 0 12 2zm0 18a8 8 0 1 1 8-8 8 8 0 0 1-8 8zm.5-13H11v6l5.2 3.1.8-1.3-4.5-2.7z"),
            Fill = (System.Windows.Media.Brush)FindResource("PrimaryText"),
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 16,
            Height = 16
        };
        historyItem.Icon = historyPath;
        historyItem.Click += (s, e) => OpenHistory();
        _trayContextMenu.Items.Add(historyItem);

        _trayContextMenu.Items.Add(new WpfSeparator());

        // 4. ☀️ / 🌙 Tema Oscuro / Claro
        _themeIconPath = new WpfPath
        {
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 16,
            Height = 16
        };
        _menuItemTheme = new WpfMenuItem
        {
            Icon = _themeIconPath
        };
        _menuItemTheme.Click += (s, e) =>
        {
            ThemeManager.ToggleTheme();
            UpdateTrayMenuItems();
        };
        _trayContextMenu.Items.Add(_menuItemTheme);

        // 5. 🌐 Idioma de traducción (Submenú desplegable moderno)
        var langMenu = new WpfMenuItem
        {
            Header = "Idioma de traducción"
        };
        var globeImg = new WpfImage
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/Icons/globe.png")),
            Width = 16,
            Height = 16
        };
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(globeImg, System.Windows.Media.BitmapScalingMode.HighQuality);
        langMenu.Icon = globeImg;

        _checkLangEs = new WpfTextBlock
        {
            Text = "✔",
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
            FontSize = 13,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        _menuLangEs = new WpfMenuItem
        {
            Header = "🇪🇸  Español (Predeterminado)",
            Icon = _checkLangEs
        };
        _menuLangEs.Click += (s, e) =>
        {
            TranslationLanguageManager.SetLanguage(TranslationLanguageManager.Spanish);
            UpdateTrayMenuItems();
        };

        _checkLangEn = new WpfTextBlock
        {
            Text = "✔",
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),
            FontSize = 13,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        _menuLangEn = new WpfMenuItem
        {
            Header = "🇺🇸  Inglés (English)",
            Icon = _checkLangEn
        };
        _menuLangEn.Click += (s, e) =>
        {
            TranslationLanguageManager.SetLanguage(TranslationLanguageManager.English);
            UpdateTrayMenuItems();
        };

        langMenu.Items.Add(_menuLangEs);
        langMenu.Items.Add(_menuLangEn);
        _trayContextMenu.Items.Add(langMenu);

        _trayContextMenu.Items.Add(new WpfSeparator());

        // 6. ❌ Salir
        var exitItem = new WpfMenuItem
        {
            Header = "Salir"
        };
        var exitPath = new WpfPath
        {
            Data = System.Windows.Media.Geometry.Parse("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"),
            Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 14,
            Height = 14
        };
        exitItem.Icon = exitPath;
        exitItem.Click += (s, e) => ExitApplication();
        _trayContextMenu.Items.Add(exitItem);

        ThemeManager.ThemeChanged += _ => Dispatcher.Invoke(UpdateTrayMenuItems);
        TranslationLanguageManager.LanguageChanged += _ => Dispatcher.Invoke(UpdateTrayMenuItems);
    }

    private void UpdateTrayMenuItems()
    {
        if (_menuItemTheme != null && _themeIconPath != null)
        {
            bool isDark = ThemeManager.CurrentTheme == AppTheme.Dark;
            _menuItemTheme.Header = isDark ? "Cambiar a Tema Claro" : "Cambiar a Tema Oscuro";
            _themeIconPath.Data = isDark
                ? System.Windows.Media.Geometry.Parse("M12 7a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0-5a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0V3a1 1 0 0 1 1-1zm0 18a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0v-2a1 1 0 0 1 1-1zm10-9a1 1 0 0 1-1 1h-2a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1zM5 12a1 1 0 0 1-1 1H2a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1z")
                : System.Windows.Media.Geometry.Parse("M12 3a9 9 0 1 0 9 9c0-.46-.04-.92-.1-1.36a5.389 5.389 0 0 1-4.4 2.26 5.403 5.403 0 0 1-3.14-9.8c-.44-.06-.9-.1-1.36-.1z");
            _themeIconPath.Fill = isDark
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
        }

        if (_checkLangEs != null && _checkLangEn != null)
        {
            bool isEs = TranslationLanguageManager.CurrentLanguage == TranslationLanguageManager.Spanish;
            _checkLangEs.Visibility = isEs ? Visibility.Visible : Visibility.Collapsed;
            _checkLangEn.Visibility = !isEs ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public static void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (Current is App app && app._notifyIcon != null)
        {
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    app._notifyIcon.ShowBalloonTip(4500, title, message, icon);
                }
                catch { }
            }));
        }
    }

    private void SetupGlobalHotkeys()
    {
        var parameters = new HwndSourceParameters("LightCaptureHotkeyListener")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };

        _messageHwndSource = new HwndSource(parameters);
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.Initialize(_messageHwndSource);

        // Tecla F9 exclusiva para Light Capture Studio
        bool f9Ok = _hotkeyManager.Register(HotkeyManager.VK_F9, 0, StartCapture);

        // Tecla Impr Pant (PrintScreen) directa si está disponible
        _hotkeyManager.Register(HotkeyManager.VK_SNAPSHOT, 0, StartCapture);

        // Atajo secundario alternativo (Ctrl+Shift+X)
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint VK_X = 0x58;
        _hotkeyManager.Register(VK_X, MOD_CONTROL | MOD_SHIFT, StartCapture);

        if (!f9Ok)
        {
            _notifyIcon?.ShowBalloonTip(
                4000,
                "Light Capture Studio",
                "La tecla F9 está asignada en otro programa. Usa 'Ctrl+Shift+X' o haz clic en la pluma para capturar.",
                ToolTipIcon.Warning
            );
        }
    }

    public void StartCapture()
    {
        Dispatcher.Invoke(() =>
        {
            if (_currentOverlay != null && _currentOverlay.IsLoaded)
            {
                _currentOverlay.Activate();
                return;
            }

            try
            {
                var frozenScreen = _captureService.CaptureVirtualScreen();
                var bounds = _captureService.GetVirtualScreenBounds();

                _currentOverlay = new OverlayWindow(
                    frozenScreen,
                    bounds,
                    _captureService,
                    _ocrService,
                    _translationService,
                    _historyService
                );

                _currentOverlay.Closed += (s, e) => _currentOverlay = null;
                _currentOverlay.Show();
                _currentOverlay.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al capturar la pantalla: {ex.Message}", "Error de captura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    public void OpenHistory()
    {
        Dispatcher.Invoke(() =>
        {
            if (_currentHistoryWindow != null && _currentHistoryWindow.IsLoaded)
            {
                _currentHistoryWindow.Activate();
                return;
            }

            _currentHistoryWindow = new HistoryWindow(_historyService);
            _currentHistoryWindow.Closed += (s, e) => _currentHistoryWindow = null;
            _currentHistoryWindow.Show();
            _currentHistoryWindow.Activate();
        });
    }

    private void ExitApplication()
    {
        _wakeUpEvent?.Dispose();
        _wakeUpEvent = null;
        _notifyIcon?.Dispose();
        _hotkeyManager?.Dispose();
        _messageHwndSource?.Dispose();
        _singleInstanceMutex?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wakeUpEvent?.Dispose();
        _wakeUpEvent = null;
        _notifyIcon?.Dispose();
        _hotkeyManager?.Dispose();
        _messageHwndSource?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    public static void EnsureNativeDependencies()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            ExtractResourceIfMissing("ScreenRecorderLib.dll", Path.Combine(baseDir, "ScreenRecorderLib.dll"));
            ExtractResourceIfMissing("LightCapture.Recorder.exe", Path.Combine(baseDir, "LightCapture.Recorder.exe"));
        }
        catch { }
    }

    private static void ExtractResourceIfMissing(string resourceSuffix, string targetPath)
    {
        try
        {
            var asm = typeof(App).Assembly;
            string? resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    bool needExtract = !File.Exists(targetPath) || new FileInfo(targetPath).Length != stream.Length;
                    if (needExtract)
                    {
                        using var fileStream = File.Create(targetPath);
                        stream.CopyTo(fileStream);
                    }
                }
            }
        }
        catch { }
    }
}
