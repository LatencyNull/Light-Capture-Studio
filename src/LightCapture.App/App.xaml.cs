using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using LightCapture.App.Services;
using LightCapture.App.Views;
using LightCapture.Core.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace LightCapture.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static EventWaitHandle? _wakeUpEvent;
    private NotifyIcon? _notifyIcon;
    private HotkeyManager? _hotkeyManager;
    private HwndSource? _messageHwndSource;

    private readonly ICaptureService _captureService = new ScreenCaptureService();
    private readonly IOcrService _ocrService = new WindowsOcrService();
    private readonly ITranslationService _translationService = new GoogleTranslationService();
    private readonly ICaptureHistoryService _historyService = new CaptureHistoryService();

    private OverlayWindow? _currentOverlay;
    private HistoryWindow? _currentHistoryWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        var contextMenu = new ContextMenuStrip();
        
        var captureItem = new ToolStripMenuItem("📸 Capturar pantalla (F9 o Ctrl+Shift+X)", null, (s, e) => StartCapture());
        captureItem.Font = new Font(captureItem.Font, System.Drawing.FontStyle.Bold);

        var historyItem = new ToolStripMenuItem("🕒 Historial de capturas con búsqueda...", null, (s, e) => OpenHistory());

        var infoItem = new ToolStripMenuItem("🌐 Modo: Lightshot + Google Lens + Censura + QR", null, (s, e) => { });
        infoItem.Enabled = false;

        var exitItem = new ToolStripMenuItem("❌ Salir", null, (s, e) => ExitApplication());

        contextMenu.Items.Add(captureItem);
        contextMenu.Items.Add(historyItem);
        contextMenu.Items.Add(infoItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

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
            ContextMenuStrip = contextMenu
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
            if (!File.Exists(targetPath) || new FileInfo(targetPath).Length == 0)
            {
                var asm = typeof(App).Assembly;
                string? resourceName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));

                if (resourceName != null)
                {
                    using var stream = asm.GetManifestResourceStream(resourceName);
                    if (stream != null)
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
