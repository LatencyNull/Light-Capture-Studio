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

    private WpfContextMenu? _trayContextMenu;
    private WpfMenuItem? _captureMenuItem;
    private WpfMenuItem? _hotkeyMenuItem;
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
            MessageBox.Show($"An unexpected error occurred:\n{args.Exception}", AppPaths.AppDisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            MessageBox.Show($"An unexpected error occurred:\n{args.ExceptionObject}", AppPaths.AppDisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
        };

        _singleInstanceMutex = new Mutex(true, "MasterCapture_SingleInstance_Mutex", out bool createdNew);

        if (!createdNew)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting("MasterCapture_WakeUp_Event", out var existingEvent))
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
            _wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "MasterCapture_WakeUp_Event");
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
            Text = FormatTrayText(HotkeySettingsManager.CurrentDisplayName),
            Visible = true,
            ContextMenuStrip = null // Reemplazado por el menú flotante moderno WPF One UI
        };

        _notifyIcon.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right)
            {
                ShowTrayContextMenu();
            }
        };

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                StartCapture();
            }
        };

        _notifyIcon.BalloonTipClicked += (s, e) => StartCapture();
    }

    private void ShowTrayContextMenu()
    {
        if (_trayContextMenu == null)
        {
            BuildTrayContextMenu();
        }

        if (_trayContextMenu != null && _trayContextMenu.IsOpen)
        {
            _trayContextMenu.IsOpen = false;
            return;
        }

        UpdateTrayMenuItems();

        var cursorPos = System.Windows.Forms.Cursor.Position;
        double scaleX = 1.0;
        double scaleY = 1.0;
        try
        {
            using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
            {
                scaleX = g.DpiX / 96.0;
                scaleY = g.DpiY / 96.0;
            }
        }
        catch { }

        if (_trayContextMenu != null)
        {
            _trayContextMenu.PlacementTarget = null;
            _trayContextMenu.Placement = PlacementMode.AbsolutePoint;
            _trayContextMenu.HorizontalOffset = cursorPos.X / scaleX;
            _trayContextMenu.VerticalOffset = cursorPos.Y / scaleY;

            _trayContextMenu.IsOpen = true;

            if (PresentationSource.FromVisual(_trayContextMenu) is HwndSource source)
            {
                SetForegroundWindow(source.Handle);
            }
        }
    }

    private void BuildTrayContextMenu()
    {
        _trayContextMenu = new WpfContextMenu
        {
            Style = (Style)FindResource("ModernContextMenuStyle"),
            StaysOpen = false
        };

        _trayContextMenu.Closed += (s, e) =>
        {
            if (PresentationSource.FromVisual(_trayContextMenu) is HwndSource source)
            {
                PostMessage(source.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            }
        };

        // 1. Header elegante con logo, nombre y versión (sin bug de recorte)
        var headerItem = new WpfMenuItem
        {
            Style = (Style)FindResource("ModernMenuTitleStyle"),
            Header = new WpfTextBlock
            {
                Text = AppPaths.AppDisplayName,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText"),
                VerticalAlignment = VerticalAlignment.Center
            },
            InputGestureText = typeof(App).Assembly.GetName().Version is { } v ? $"v{v.Major}.{v.Minor}.{v.Build}" : "v1.1.0"
        };
        var appLogo = new WpfImage
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/Icons/gemini.png")),
            Width = 15,
            Height = 15
        };
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(appLogo, System.Windows.Media.BitmapScalingMode.HighQuality);
        headerItem.Icon = appLogo;
        _trayContextMenu.Items.Add(headerItem);

        // 2. 📸 Screen Capture (Parent menu with subitems for capture, history and settings)
        var captureMenu = new WpfMenuItem
        {
            Header = "Screen Capture",
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
        captureMenu.Icon = camPath;

        // Subitem: Capture Screen
        _captureMenuItem = new WpfMenuItem
        {
            Header = "Capture Screen",
            InputGestureText = HotkeySettingsManager.CurrentDisplayName,
            FontWeight = FontWeights.SemiBold
        };
        var camSubPath = new WpfPath
        {
            Data = System.Windows.Media.Geometry.Parse("M4 4h3l2-2h6l2 2h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2zm8 3a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0 2a3 3 0 1 1 0 6 3 3 0 0 1 0-6z"),
            Fill = (System.Windows.Media.Brush)FindResource("PrimaryText"),
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 16,
            Height = 16
        };
        _captureMenuItem.Icon = camSubPath;
        _captureMenuItem.Click += (s, e) => StartCapture();
        captureMenu.Items.Add(_captureMenuItem);

        // Subitem: Capture History...
        var historyItem = new WpfMenuItem
        {
            Header = "Capture History..."
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
        captureMenu.Items.Add(historyItem);

        // Subitem: Open Captures Folder
        var openFolderItem = new WpfMenuItem
        {
            Header = "Open Captures Folder"
        };
        var folderPath = new WpfPath
        {
            Data = System.Windows.Media.Geometry.Parse("M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z"),
            Fill = (System.Windows.Media.Brush)FindResource("PrimaryText"),
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 16,
            Height = 16
        };
        openFolderItem.Icon = folderPath;
        openFolderItem.Click += (s, e) => OpenCapturesFolder();
        captureMenu.Items.Add(openFolderItem);

        // Subitem: Configure Capture Hotkey...
        _hotkeyMenuItem = new WpfMenuItem
        {
            Header = $"Configure Capture Hotkey ({HotkeySettingsManager.CurrentDisplayName})..."
        };
        var keyboardPath = new WpfPath
        {
            Data = System.Windows.Media.Geometry.Parse("M20 5H4c-1.1 0-1.99.9-1.99 2L2 17c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm-9 3h2v2h-2V8zm0 3h2v2h-2v-2zM8 8h2v2H8V8zm0 3h2v2H8v-2zm-1 2H5v-2h2v2zm0-3H5V8h2v2zm9 7H8v-2h8v2zm0-4h-2v-2h2v2zm0-3h-2V8h2v2zm3 3h-2v-2h2v2zm0-3h-2V8h2v2z"),
            Fill = (System.Windows.Media.Brush)FindResource("PrimaryText"),
            Stretch = System.Windows.Media.Stretch.Uniform,
            Width = 16,
            Height = 16
        };
        _hotkeyMenuItem.Icon = keyboardPath;
        _hotkeyMenuItem.Click += (s, e) => OpenHotkeyConfig();
        captureMenu.Items.Add(_hotkeyMenuItem);

        _trayContextMenu.Items.Add(captureMenu);

        _trayContextMenu.Items.Add(new WpfSeparator());

        // 4. ☀️ / 🌙 Theme Dark / Light
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

        // 5. 🌐 Translation Language (Modern flyout submenu)
        var langMenu = new WpfMenuItem
        {
            Header = "Translation Language"
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
            Header = "🇪🇸  Spanish",
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
            Header = "🇺🇸  English (Default)",
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

        // 6. ❌ Exit
        var exitItem = new WpfMenuItem
        {
            Header = "Exit"
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
            _menuItemTheme.Header = isDark ? "Theme: Dark (Switch to Light)" : "Theme: Light (Switch to Dark)";
            _themeIconPath.Data = isDark
                ? System.Windows.Media.Geometry.Parse("M12 3a9 9 0 1 0 9 9c0-.46-.04-.92-.1-1.36a5.389 5.389 0 0 1-4.4 2.26 5.403 5.403 0 0 1-3.14-9.8c-.44-.06-.9-.1-1.36-.1z") // Luna cuando está en modo oscuro
                : System.Windows.Media.Geometry.Parse("M12 7a5 5 0 1 0 0 10 5 5 0 0 0 0-10zm0-5a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0V3a1 1 0 0 1 1-1zm0 18a1 1 0 0 1 1 1v2a1 1 0 0 1-2 0v-2a1 1 0 0 1 1-1zm10-9a1 1 0 0 1-1 1h-2a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1zM5 12a1 1 0 0 1-1 1H2a1 1 0 0 1 0-2h2a1 1 0 0 1 1 1z"); // Sol cuando está en modo claro
            _themeIconPath.Fill = isDark
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
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
        var parameters = new HwndSourceParameters("MasterCaptureHotkeyListener")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };

        _messageHwndSource = new HwndSource(parameters);
        _hotkeyManager = new HotkeyManager();
        _hotkeyManager.Initialize(_messageHwndSource);

        // Tecla configurada por el usuario (F9 por defecto, o personalizada)
        uint userVk = HotkeySettingsManager.CurrentVirtualKey;
        uint userMod = HotkeySettingsManager.CurrentModifiers;
        string userDisplay = HotkeySettingsManager.CurrentDisplayName;

        bool mainOk = _hotkeyManager.RegisterCaptureHotkey(userVk, userMod, StartCapture);

        // Si la tecla configurada no es Impr Pant, registrarla como acceso secundario universal
        if (userVk != HotkeyManager.VK_SNAPSHOT)
        {
            _hotkeyManager.Register(HotkeyManager.VK_SNAPSHOT, 0, StartCapture);
        }

        // Atajo secundario alternativo fijo (Ctrl+Shift+X)
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint VK_X = 0x58;
        _hotkeyManager.Register(VK_X, MOD_CONTROL | MOD_SHIFT, StartCapture);

        if (!mainOk)
        {
            _notifyIcon?.ShowBalloonTip(
                4000,
                AppPaths.AppDisplayName,
                $"The key '{userDisplay}' is assigned to another program. Use 'Ctrl+Shift+X' or right-click the tray icon to change it.",
                ToolTipIcon.Warning
            );
        }
    }

    public void ApplyCaptureHotkey(uint vk, uint modifiers, string displayName)
    {
        HotkeySettingsManager.SaveConfig(vk, modifiers, displayName);

        bool ok = _hotkeyManager?.RegisterCaptureHotkey(vk, modifiers, StartCapture) ?? false;

        UpdateTrayHotkeyDisplay(displayName);

        if (!ok)
        {
            _notifyIcon?.ShowBalloonTip(
                4000,
                AppPaths.AppDisplayName,
                $"The key '{displayName}' could not be registered (may be in use by Windows or another app).",
                ToolTipIcon.Warning
            );
        }
        else
        {
            _notifyIcon?.ShowBalloonTip(
                2500,
                AppPaths.AppDisplayName,
                $"✅ Capture hotkey configured: {displayName}",
                ToolTipIcon.Info
            );
        }
    }

    private static string FormatTrayText(string displayName)
    {
        string text = $"{AppPaths.AppDisplayName} - {displayName} to capture screen";
        return text.Length > 63 ? text[..63] : text;
    }

    private void UpdateTrayHotkeyDisplay(string displayName)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Text = FormatTrayText(displayName);
        }

        if (_captureMenuItem != null)
        {
            _captureMenuItem.InputGestureText = displayName;
        }

        if (_hotkeyMenuItem != null)
        {
            _hotkeyMenuItem.Header = $"Configure Capture Hotkey ({displayName})...";
        }
    }

    public void OpenCapturesFolder()
    {
        try
        {
            string folder = AppPaths.GetCapturesDirectory();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening folder: {ex.Message}", "Captures Folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private HotkeyConfigWindow? _currentHotkeyWindow;

    public void OpenHotkeyConfig()
    {
        Dispatcher.Invoke(() =>
        {
            if (_currentHotkeyWindow != null && _currentHotkeyWindow.IsLoaded)
            {
                _currentHotkeyWindow.Activate();
                return;
            }

            _currentHotkeyWindow = new HotkeyConfigWindow();
            _currentHotkeyWindow.Closed += (s, e) => _currentHotkeyWindow = null;
            _currentHotkeyWindow.Show();
            _currentHotkeyWindow.Activate();
        });
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
                MessageBox.Show($"Error capturing screen: {ex.Message}", "Capture Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            string appDataBin = AppPaths.GetRecorderBinDirectory();

            ExtractResourceIfMissing("ScreenRecorderLib.dll", Path.Combine(appDataBin, "ScreenRecorderLib.dll"));
            ExtractResourceIfMissing("LightCapture.Recorder.exe", Path.Combine(appDataBin, "LightCapture.Recorder.exe"));
            ExtractResourceIfMissing("LightCapture.Recorder.dll", Path.Combine(appDataBin, "LightCapture.Recorder.dll"));
            ExtractResourceIfMissing("LightCapture.Recorder.runtimeconfig.json", Path.Combine(appDataBin, "LightCapture.Recorder.runtimeconfig.json"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Error ensuring native dependencies: {ex.Message}");
        }
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Error extracting resource {resourceSuffix}: {ex.Message}");
        }
    }
}
