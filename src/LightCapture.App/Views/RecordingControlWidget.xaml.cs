using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfApplication = System.Windows.Application;

namespace LightCapture.App.Views;

public partial class RecordingControlWidget : Window
{
    private bool _isRecording;
    private bool _isAudioOn;
    private bool _isMicOn;
    private bool _isGif;
    private int _fps = 60;

    public bool IsGif => _isGif;
    public bool IsAudioOn => _isAudioOn;
    public bool IsMicOn => _isMicOn;
    public int Fps => _fps;
    public bool IsRecordingActive => _isRecording;

    public event Action<bool, bool, bool, int>? StartRequested; // (captureAudio, captureMic, isGif, fps)
    public event Action? StopRequested;
    public event Action? CancelRequested;
    public event Action<bool>? AudioToggled;
    public event Action<bool>? MicToggled;

    public RecordingControlWidget(bool initialAudioOn, bool initialMicOn, Rect recordBounds, bool initialIsGif = false)
    {
        InitializeComponent();

        _isAudioOn = initialAudioOn;
        _isMicOn = initialMicOn;
        _isGif = initialIsGif;
        _isRecording = false;

        UpdateFormatUi();
        UpdateAudioUi();
        UpdateMicUi();
        UpdateFpsUi();

        // Posicionamiento inteligente fuera de la zona de grabación
        double leftPos = recordBounds.Left + (recordBounds.Width - 400) / 2;
        double topPos = recordBounds.Bottom + 12;

        if (topPos + 75 > SystemParameters.VirtualScreenHeight)
        {
            topPos = Math.Max(10, recordBounds.Top - 75);
        }

        Left = Math.Max(10, Math.Min(leftPos, SystemParameters.VirtualScreenWidth - 420));
        Top = Math.Max(10, topPos);
    }

    public void OnRecordingStarted()
    {
        _isRecording = true;

        // Change main button from Start (green) to Stop (red)
        BtnAction.Background = new SolidColorBrush(MediaColor.FromRgb(239, 68, 68)); // #EF4444
        TxtActionIcon.Text = "⏹";
        TxtActionText.Text = "Stop";
        BtnAction.ToolTip = "Stop recording and open preview";

        // Bloquear cambio de formato y FPS durante la grabación activa
        BtnFpsToggle.IsEnabled = false;
        BtnFpsToggle.Opacity = 0.5;

        if (_isGif)
        {
            BtnFormatMp4.IsEnabled = false;
            BtnFormatMp4.Opacity = 0.5;
        }
        else
        {
            // En MP4, el selector GIF sigue visible a menos que supere 15s
        }

        // Activar pulso rojo en REC
        RecDot.Fill = new SolidColorBrush(MediaColor.FromRgb(239, 68, 68));
        if (TryFindResource("RecDotPulse") is Storyboard sb)
        {
            sb.Begin();
        }
    }

    public void UpdateDuration(TimeSpan duration)
    {
        Dispatcher.Invoke(() =>
        {
            if (_isGif)
            {
                int sec = (int)Math.Min(15, duration.TotalSeconds);
                TxtTimer.Text = $"{sec:D2}s / 15s";

                // Si es GIF y alcanza los 15 segundos, finalizar automáticamente
                if (duration.TotalSeconds >= 15 && _isRecording)
                {
                    StopRequested?.Invoke();
                    return;
                }
            }
            else
            {
                TxtTimer.Text = $"{duration.Minutes:D2}:{duration.Seconds:D2}";
            }

            // If video exceeds 15 seconds, GIF option is disabled
            if (duration.TotalSeconds > 15)
            {
                BtnFormatGif.IsEnabled = false;
                BtnFormatGif.Opacity = 0.35;
                BtnFormatGif.ToolTip = "GIF format unavailable: recording exceeded the 15-second limit.";
            }
        });
    }

    private void BtnFormatMp4_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording) return;

        _isGif = false;
        UpdateFormatUi();
    }

    private void BtnFormatGif_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording) return;

        _isGif = true;
        UpdateFormatUi();
    }

    private void UpdateFormatUi()
    {
        MediaBrush secBrush = WpfApplication.Current?.Resources["SecondaryText"] as MediaBrush ?? new SolidColorBrush(MediaColor.FromRgb(148, 163, 184));

        if (!_isGif)
        {
            // MP4 seleccionado
            BtnFormatMp4.Background = new SolidColorBrush(MediaColor.FromRgb(37, 99, 235)); // #2563EB
            TxtFormatMp4.Foreground = MediaBrushes.White;

            BtnFormatGif.Background = MediaBrushes.Transparent;
            TxtFormatGif.Foreground = secBrush;

            // Mostrar controles de sonido y FPS para MP4
            BtnAudioToggle.Visibility = Visibility.Visible;
            BtnMicToggle.Visibility = Visibility.Visible;
            SepAudio.Visibility = Visibility.Visible;
            BorderFpsToggle.Visibility = Visibility.Visible;

            if (!_isRecording)
            {
                TxtTimer.Text = "00:00";
            }
        }
        else
        {
            // GIF seleccionado
            BtnFormatGif.Background = new SolidColorBrush(MediaColor.FromRgb(2, 132, 199)); // #0284C7
            TxtFormatGif.Foreground = MediaBrushes.White;

            BtnFormatMp4.Background = MediaBrushes.Transparent;
            TxtFormatMp4.Foreground = secBrush;

            // Ocultar controles de sonido y FPS para GIF (formato visual sin audio y con framerate propio)
            BtnAudioToggle.Visibility = Visibility.Collapsed;
            BtnMicToggle.Visibility = Visibility.Collapsed;
            SepAudio.Visibility = Visibility.Collapsed;
            BorderFpsToggle.Visibility = Visibility.Collapsed;

            if (!_isRecording)
            {
                TxtTimer.Text = "00s / 15s";
            }
        }
    }

    private void BtnFpsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording) return;
        _fps = (_fps == 60) ? 30 : 60;
        UpdateFpsUi();
    }

    private void UpdateFpsUi()
    {
        if (_fps == 60)
        {
            TxtFpsIcon.Text = "⚡";
            TxtFps.Text = "60 FPS";
            TxtFps.Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)); // #38BDF8
            BtnFpsToggle.ToolTip = "60 FPS (Ultra smooth for gaming, videos, and motion). Click to switch to 30 FPS.";
        }
        else
        {
            TxtFpsIcon.Text = "🎬";
            TxtFps.Text = "30 FPS";
            TxtFps.Foreground = new SolidColorBrush(MediaColor.FromRgb(251, 191, 36)); // #FBBF24 Amber
            BtnFpsToggle.ToolTip = "30 FPS (Lightweight mode for tutorials and smaller file sizes). Click to switch to 60 FPS.";
        }
    }

    private void BtnAction_Click(object sender, RoutedEventArgs e)
    {
        if (!_isRecording)
        {
            // Start recording with selected FPS
            StartRequested?.Invoke(_isAudioOn, _isMicOn, _isGif, _fps);
        }
        else
        {
            // Stop recording
            StopRequested?.Invoke();
        }
    }

    private void UpdateAudioUi()
    {
        TxtAudioIcon.Text = _isAudioOn ? "🔊" : "🔇";
        BtnAudioToggle.ToolTip = _isAudioOn
            ? "System audio ENABLED. Click to mute."
            : "System audio MUTED. Click to enable.";
        BtnAudioToggle.Opacity = _isAudioOn ? 1.0 : 0.55;
    }

    private void UpdateMicUi()
    {
        TxtMicIcon.Text = _isMicOn ? "🎙️" : "🔇";
        BtnMicToggle.ToolTip = _isMicOn
            ? "Microphone ENABLED. Click to mute."
            : "Microphone MUTED. Click to enable.";
        BtnMicToggle.Opacity = _isMicOn ? 1.0 : 0.55;
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

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => CancelRequested?.Invoke();

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
