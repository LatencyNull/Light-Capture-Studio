using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LightCapture.App.Views;

public partial class RecordingControlWidget : Window
{
    private bool _isAudioOn;
    private bool _isMicOn;
    private readonly bool _isGif;

    public event Action? StopRequested;
    public event Action? CancelRequested;
    public event Action<bool>? AudioToggled;
    public event Action<bool>? MicToggled;

    public RecordingControlWidget(bool initialAudioOn, bool initialMicOn, Rect recordBounds, bool isGif = false)
    {
        InitializeComponent();

        _isAudioOn = initialAudioOn;
        _isMicOn = initialMicOn;
        _isGif = isGif;

        if (_isGif)
        {
            BadgeGif.Visibility = Visibility.Visible;
            BtnAudioToggle.Visibility = Visibility.Collapsed;
            BtnMicToggle.Visibility = Visibility.Collapsed;
            TxtTimer.Text = "00s / 15s";
        }
        else
        {
            UpdateAudioUi();
            UpdateMicUi();
        }

        // Posicionamiento inteligente fuera de la zona de grabación
        double leftPos = recordBounds.Left + (recordBounds.Width - 320) / 2;
        double topPos = recordBounds.Bottom + 12;

        if (topPos + 70 > SystemParameters.VirtualScreenHeight)
        {
            topPos = Math.Max(10, recordBounds.Top - 70);
        }

        Left = Math.Max(10, Math.Min(leftPos, SystemParameters.VirtualScreenWidth - 340));
        Top = Math.Max(10, topPos);

        Loaded += (s, e) =>
        {
            if (TryFindResource("RecDotPulse") is Storyboard sb)
            {
                sb.Begin();
            }
        };
    }

    public void UpdateDuration(TimeSpan duration)
    {
        Dispatcher.Invoke(() =>
        {
            if (_isGif)
            {
                int sec = (int)Math.Min(15, duration.TotalSeconds);
                TxtTimer.Text = $"{sec:D2}s / 15s";
            }
            else
            {
                TxtTimer.Text = $"{duration.Minutes:D2}:{duration.Seconds:D2}";
            }
        });
    }

    private void UpdateAudioUi()
    {
        TxtAudioIcon.Text = _isAudioOn ? "🔊" : "🔇";
        BtnAudioToggle.ToolTip = _isAudioOn
            ? "Audio del sistema ACTIVADO. Haz clic para silenciar."
            : "Audio del sistema SILENCIADO. Haz clic para activar.";
        BtnAudioToggle.Opacity = _isAudioOn ? 1.0 : 0.55;
    }

    private void UpdateMicUi()
    {
        TxtMicIcon.Text = _isMicOn ? "🎙️" : "🔇";
        BtnMicToggle.ToolTip = _isMicOn
            ? "Micrófono ACTIVADO (Grabando tu voz). Haz clic para silenciar."
            : "Micrófono SILENCIADO. Haz clic para activar.";
        BtnMicToggle.Opacity = _isMicOn ? 1.0 : 0.55;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnAudioToggle_Click(object sender, RoutedEventArgs e)
    {
        _isAudioOn = !_isAudioOn;
        UpdateAudioUi();
        AudioToggled?.Invoke(_isAudioOn);
    }

    private void BtnMicToggle_Click(object sender, RoutedEventArgs e)
    {
        _isMicOn = !_isMicOn;
        UpdateMicUi();
        MicToggled?.Invoke(_isMicOn);
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => CancelRequested?.Invoke();
}
