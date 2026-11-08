using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace LightCapture.App.Views;

public partial class ToastNotificationWindow : Window
{
    private static ToastNotificationWindow? _activeInstance;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    public ToastNotificationWindow(string message, string? iconRelativePath = null)
    {
        InitializeComponent();

        if (message.Contains("Ctrl+V") || message.Contains("Ctrl + V"))
        {
            ShortcutPanel.Visibility = Visibility.Visible;
            if (message.Contains("Gemini"))
            {
                ToastTitle.Text = "¡Captura copiada al portapapeles!";
                ToastActionSuffix.Text = " en Gemini para pegarla ✨";
            }
            else if (message.Contains("WhatsApp") || message.Contains("chat"))
            {
                ToastTitle.Text = "¡Captura copiada al portapapeles!";
                ToastActionSuffix.Text = " en tu chat para enviarla 💬";
            }
            else if (message.Contains("Facebook"))
            {
                ToastTitle.Text = "¡Captura copiada al portapapeles!";
                ToastActionSuffix.Text = " en Facebook para publicarla 🔵";
            }
            else if (message.Contains("Instagram"))
            {
                ToastTitle.Text = "¡Captura copiada al portapapeles!";
                ToastActionSuffix.Text = " en Instagram para enviarla 📸";
            }
            else if (message.Contains("Telegram"))
            {
                ToastTitle.Text = "¡Captura copiada al portapapeles!";
                ToastActionSuffix.Text = " en Telegram para pegarla ✈️";
            }
            else if (message.Contains("X") || message.Contains("Twitter"))
            {
                ToastTitle.Text = "¡Captura copiada al portapapeles!";
                ToastActionSuffix.Text = " en X para publicar ✖️";
            }
            else
            {
                ToastTitle.Text = "¡Captura copiada al portapapeles!";
                ToastActionSuffix.Text = " donde desees pegarla";
            }
        }
        else
        {
            ToastTitle.Text = message;
            ShortcutPanel.Visibility = Visibility.Collapsed;
        }

        // Exact center of the primary screen
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        double screenHeight = SystemParameters.PrimaryScreenHeight;
        Left = Math.Max(20, (screenWidth - 520) / 2);
        Top = Math.Max(20, (screenHeight - 82) / 2);

        if (!string.IsNullOrEmpty(iconRelativePath))
        {
            try
            {
                string path = iconRelativePath.StartsWith("/") ? iconRelativePath : "/" + iconRelativePath;
                ToastIcon.Source = new BitmapImage(new Uri(path, UriKind.Relative));
            }
            catch
            {
                ToastIcon.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            ToastIcon.Visibility = Visibility.Collapsed;
        }

        SourceInitialized += (s, e) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        };

        Loaded += ToastNotificationWindow_Loaded;
    }

    private void ToastNotificationWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Start subtle glowing / breathing pulse
        if (Resources["SubtlePulseStoryboard"] is Storyboard pulse)
        {
            pulse.Begin();
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        timer.Tick += (s, ev) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(450));
            fadeOut.Completed += (s2, ev2) =>
            {
                try { Close(); } catch { }
            };
            BeginAnimation(OpacityProperty, fadeOut);
        };
        timer.Start();
    }

    public static void ShowToast(string message, string? iconRelativePath = null)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                _activeInstance?.Close();
            }
            catch { }

            _activeInstance = new ToastNotificationWindow(message, iconRelativePath);
            _activeInstance.Show();
        });
    }
}
