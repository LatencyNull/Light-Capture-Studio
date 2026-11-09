using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using LightCapture.App.Services;

namespace LightCapture.App.Views;

public partial class VideoPreviewWindow : Window
{
    private readonly string _videoPath;
    private string? _cachedTrimmedPath;
    private bool _isPlaying = true;

    private TimeSpan _totalDuration = TimeSpan.Zero;
    private TimeSpan _trimStart = TimeSpan.Zero;
    private TimeSpan _trimEnd = TimeSpan.Zero;

    private bool _isDraggingStart;
    private bool _isDraggingEnd;
    private DispatcherTimer? _playbackTimer;
    private DateTime _lastSocialMenuCloseTime = DateTime.MinValue;

    public VideoPreviewWindow(string videoPath)
    {
        InitializeComponent();

        _videoPath = videoPath;

        if (BtnMoreSocial.ContextMenu != null)
        {
            BtnMoreSocial.ContextMenu.Closed += (s, e) => _lastSocialMenuCloseTime = DateTime.UtcNow;
        }

        bool isGif = _videoPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
        if (isGif)
        {
            TxtHeaderIcon.Text = "🎞️";
            TxtHeaderTitle.Text = "Previsualización del GIF Animado";
            Title = "Previsualización del GIF - Light Capture Studio";
        }

        // Extraer peso del archivo
        try
        {
            var fileInfo = new FileInfo(_videoPath);
            double sizeMb = Math.Round((double)fileInfo.Length / (1024 * 1024), 2);
            TxtVideoInfo.Text = isGif ? $"GIF • {sizeMb} MB" : $"{sizeMb} MB";
        }
        catch { }

        // Inicializar reproductor de video
        MediaVideoPlayer.Source = new Uri(_videoPath, UriKind.Absolute);
        MediaVideoPlayer.MediaOpened += (s, e) =>
        {
            if (MediaVideoPlayer.NaturalDuration.HasTimeSpan)
            {
                _totalDuration = MediaVideoPlayer.NaturalDuration.TimeSpan;
                _trimStart = TimeSpan.Zero;
                _trimEnd = _totalDuration;

                var fileInfo = new FileInfo(_videoPath);
                double sizeMb = Math.Round((double)fileInfo.Length / (1024 * 1024), 2);
                string formatTag = isGif ? "GIF • " : "";
                TxtVideoInfo.Text = $"{formatTag}{_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2} • {sizeMb} MB";

                UpdateTimelinePins();
                StartPlaybackTimer();
            }
            MediaVideoPlayer.Play();
            _isPlaying = true;
            TxtPlayPauseIcon.Text = "⏸";
        };

        MediaVideoPlayer.Play();
    }

    private void StartPlaybackTimer()
    {
        _playbackTimer?.Stop();
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _playbackTimer.Tick += (s, e) =>
        {
            if (!_isPlaying || _isDraggingStart || _isDraggingEnd || _totalDuration.TotalSeconds <= 0) return;

            var pos = MediaVideoPlayer.Position;

            // Bucle inteligente dentro del rango de recorte
            if (pos >= _trimEnd || pos < _trimStart)
            {
                MediaVideoPlayer.Position = _trimStart;
                pos = _trimStart;
            }

            // Actualizar aguja de reproducción (playhead)
            double trackWidth = TimelineContainer.ActualWidth;
            if (trackWidth > 0)
            {
                double ratio = pos.TotalSeconds / _totalDuration.TotalSeconds;
                double x = Math.Max(0, Math.Min(ratio * trackWidth, trackWidth - 3));
                Canvas.SetLeft(Playhead, x);
            }

            TxtPlaybackPosition.Text = $"{pos.Minutes:D2}:{pos.Seconds:D2} / {_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";
        };
        _playbackTimer.Start();
    }

    private void TimelineContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTimelinePins();
    }

    private void UpdateTimelinePins()
    {
        double trackWidth = TimelineContainer.ActualWidth;
        if (trackWidth <= 0 || _totalDuration.TotalSeconds <= 0) return;

        double startRatio = _trimStart.TotalSeconds / _totalDuration.TotalSeconds;
        double endRatio = _trimEnd.TotalSeconds / _totalDuration.TotalSeconds;

        double startX = Math.Max(0, startRatio * trackWidth);
        double endX = Math.Min(trackWidth - PinEnd.ActualWidth, endRatio * trackWidth - PinEnd.ActualWidth);

        Canvas.SetLeft(PinStart, startX);
        Canvas.SetLeft(PinEnd, Math.Max(startX + 14, endX));

        ActiveRangeTrack.Margin = new Thickness(startX, 0, 0, 0);
        ActiveRangeTrack.Width = Math.Max(0, (endX + PinEnd.ActualWidth) - startX);

        UpdateTrimLabels();
    }

    private void UpdateTrimLabels()
    {
        var trimDuration = _trimEnd - _trimStart;
        bool isTrimmed = _trimStart > TimeSpan.FromMilliseconds(150) || 
                         (_totalDuration - _trimEnd) > TimeSpan.FromMilliseconds(150);

        TxtTrimDuration.Text = isTrimmed 
            ? $"{trimDuration.Minutes:D2}:{trimDuration.Seconds:D2}.{trimDuration.Milliseconds / 100}" 
            : $"{_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";

        BtnResetTrim.Visibility = isTrimmed ? Visibility.Visible : Visibility.Collapsed;
        BadgeTrimInfo.BorderBrush = isTrimmed 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)) 
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));

        TxtCopyClipLabel.Text = isTrimmed ? "Copiar Recorte" : "Copiar Clip";
        TxtSaveClipLabel.Text = isTrimmed ? "Guardar Recorte..." : "Guardar...";
    }

    private void PinStart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingStart = true;
        PinStart.CaptureMouse();
        MediaVideoPlayer.Pause();
        e.Handled = true;
    }

    private void PinEnd_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingEnd = true;
        PinEnd.CaptureMouse();
        MediaVideoPlayer.Pause();
        e.Handled = true;
    }

    private void Pin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingStart)
        {
            _isDraggingStart = false;
            PinStart.ReleaseMouseCapture();
            _cachedTrimmedPath = null;
        }

        if (_isDraggingEnd)
        {
            _isDraggingEnd = false;
            PinEnd.ReleaseMouseCapture();
            _cachedTrimmedPath = null;
        }

        if (_isPlaying)
        {
            MediaVideoPlayer.Play();
        }
        e.Handled = true;
    }

    private void PinStart_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingStart || _totalDuration.TotalSeconds <= 0) return;

        double trackWidth = TimelineContainer.ActualWidth;
        double curX = e.GetPosition(TimelineCanvas).X;
        double endX = Canvas.GetLeft(PinEnd);

        double newStartX = Math.Max(0, Math.Min(curX, endX - 14));
        Canvas.SetLeft(PinStart, newStartX);

        ActiveRangeTrack.Margin = new Thickness(newStartX, 0, 0, 0);
        ActiveRangeTrack.Width = Math.Max(0, (endX + PinEnd.ActualWidth) - newStartX);

        _trimStart = TimeSpan.FromSeconds((newStartX / trackWidth) * _totalDuration.TotalSeconds);
        MediaVideoPlayer.Position = _trimStart;
        Canvas.SetLeft(Playhead, newStartX);

        UpdateTrimLabels();
    }

    private void PinEnd_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingEnd || _totalDuration.TotalSeconds <= 0) return;

        double trackWidth = TimelineContainer.ActualWidth;
        double curX = e.GetPosition(TimelineCanvas).X;
        double startX = Canvas.GetLeft(PinStart);

        double newEndX = Math.Max(startX + 14, Math.Min(curX, trackWidth - PinEnd.ActualWidth));
        Canvas.SetLeft(PinEnd, newEndX);

        ActiveRangeTrack.Width = Math.Max(0, (newEndX + PinEnd.ActualWidth) - startX);

        _trimEnd = TimeSpan.FromSeconds(((newEndX + PinEnd.ActualWidth) / trackWidth) * _totalDuration.TotalSeconds);
        MediaVideoPlayer.Position = _trimEnd;
        Canvas.SetLeft(Playhead, newEndX);

        UpdateTrimLabels();
    }

    private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_totalDuration.TotalSeconds <= 0) return;

        double trackWidth = TimelineContainer.ActualWidth;
        double clickX = e.GetPosition(TimelineCanvas).X;

        double ratio = Math.Max(0, Math.Min(clickX / trackWidth, 1.0));
        var targetTime = TimeSpan.FromSeconds(ratio * _totalDuration.TotalSeconds);

        // Clamp dentro del rango recortado
        if (targetTime < _trimStart) targetTime = _trimStart;
        if (targetTime > _trimEnd) targetTime = _trimEnd;

        MediaVideoPlayer.Position = targetTime;
        Canvas.SetLeft(Playhead, Math.Max(0, Math.Min(clickX, trackWidth - 3)));
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void BtnResetTrim_Click(object sender, RoutedEventArgs e)
    {
        _trimStart = TimeSpan.Zero;
        _trimEnd = _totalDuration;
        _cachedTrimmedPath = null;
        UpdateTimelinePins();
    }

    private void TogglePlayPause()
    {
        if (_isPlaying)
        {
            MediaVideoPlayer.Pause();
            _isPlaying = false;
            TxtPlayPauseIcon.Text = "▶";
            PauseIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            if (MediaVideoPlayer.Position >= _trimEnd || MediaVideoPlayer.Position < _trimStart)
            {
                MediaVideoPlayer.Position = _trimStart;
            }
            MediaVideoPlayer.Play();
            _isPlaying = true;
            TxtPlayPauseIcon.Text = "⏸";
            PauseIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void MediaVideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        MediaVideoPlayer.Position = _trimStart;
        MediaVideoPlayer.Play();
    }

    private void VideoPlayer_MouseClick(object sender, MouseButtonEventArgs e)
    {
        TogglePlayPause();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async Task<string> GetEffectiveVideoPathAsync()
    {
        bool isTrimmed = _trimStart > TimeSpan.FromMilliseconds(150) || 
                         (_totalDuration - _trimEnd) > TimeSpan.FromMilliseconds(150);

        if (!isTrimmed)
        {
            return _videoPath;
        }

        if (!string.IsNullOrEmpty(_cachedTrimmedPath) && File.Exists(_cachedTrimmedPath))
        {
            return _cachedTrimmedPath;
        }

        try
        {
            new ToastNotificationWindow("✂️ Procesando recorte del clip...", "/Assets/Icons/save.png").Show();
            _cachedTrimmedPath = await VideoTrimmerService.TrimAsync(_videoPath, _trimStart, _trimEnd);
            return _cachedTrimmedPath;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"No se pudo recortar el clip: {ex.Message}. Se usará el video original.", "Recorte de Clip");
            return _videoPath;
        }
    }

    private async void BtnCopyClip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = await GetEffectiveVideoPathAsync();
            var files = new StringCollection { path };
            System.Windows.Clipboard.SetFileDropList(files);

            new ToastNotificationWindow(
                "¡Clip de video copiado! Pega con Ctrl+V en tu chat 💬",
                "/Assets/Icons/video.png"
            ).Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error al copiar clip: {ex.Message}");
        }
    }

    private async void ShareWhatsAppApp_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
        }
        catch { }

        bool appOpened = false;
        try
        {
            Process.Start(new ProcessStartInfo("whatsapp://") { UseShellExecute = true });
            appOpened = true;
        }
        catch { }

        new ToastNotificationWindow(
            appOpened ? "¡Clip copiado! Pega con Ctrl+V en WhatsApp App 💬" : "Abriendo WhatsApp Web... 💬",
            "/Assets/Icons/whatsapp.png"
        ).Show();
    }

    private async void ShareWhatsAppWeb_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
            Process.Start(new ProcessStartInfo("https://web.whatsapp.com/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado para WhatsApp Web! Pega con Ctrl+V en tu chat 💬",
            "/Assets/Icons/whatsapp.png"
        ).Show();
    }

    private async void BtnShareDiscord_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado para Discord! Pega con Ctrl+V en tu canal 🎮",
            "/Assets/Icons/discord.png"
        ).Show();
    }

    private void BtnMoreSocial_Click(object sender, RoutedEventArgs e)
    {
        if (BtnMoreSocial.ContextMenu == null) return;

        if (BtnMoreSocial.ContextMenu.IsOpen)
        {
            BtnMoreSocial.ContextMenu.IsOpen = false;
            return;
        }

        if ((DateTime.UtcNow - _lastSocialMenuCloseTime).TotalMilliseconds < 350)
        {
            return;
        }

        BtnMoreSocial.ContextMenu.PlacementTarget = BtnMoreSocial;
        BtnMoreSocial.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        BtnMoreSocial.ContextMenu.VerticalOffset = -6;
        BtnMoreSocial.ContextMenu.IsOpen = true;
    }

    private async void ShareTikTok_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
            Process.Start(new ProcessStartInfo("https://www.tiktok.com/upload") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo TikTok Web para subir... 🎵",
            "/Assets/Icons/tiktok.png"
        ).Show();
    }

    private async void ShareInstagram_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
            Process.Start(new ProcessStartInfo("https://www.instagram.com/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo Instagram... 📷",
            "/Assets/Icons/instagram.png"
        ).Show();
    }

    private async void ShareFacebook_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
            Process.Start(new ProcessStartInfo("https://www.facebook.com/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo Facebook... 📘",
            "/Assets/Icons/facebook.png"
        ).Show();
    }

    private async void ShareTelegram_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
            Process.Start(new ProcessStartInfo("https://web.telegram.org/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado para Telegram! Pega con Ctrl+V en tu chat ✈️",
            "/Assets/Icons/telegram.png"
        ).Show();
    }

    private async void ShareTwitter_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        try
        {
            System.Windows.Clipboard.SetFileDropList(new StringCollection { path });
            Process.Start(new ProcessStartInfo("https://x.com/compose/post") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo X / Twitter... ✖️",
            "/Assets/Icons/twitter.png"
        ).Show();
    }

    private async void BtnSaveClip_Click(object sender, RoutedEventArgs e)
    {
        string path = await GetEffectiveVideoPathAsync();
        bool isGif = path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = isGif ? "Guardar GIF animado" : "Guardar clip de video",
            Filter = isGif ? "GIF Animado (*.gif)|*.gif|Todos los archivos (*.*)|*.*" : "Video MP4 (*.mp4)|*.mp4|Todos los archivos (*.*)|*.*",
            FileName = isGif ? $"Gif_{DateTime.Now:yyyyMMdd_HHmmss}.gif" : $"Clip_{DateTime.Now:yyyyMMdd_HHmmss}.mp4"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.Copy(path, sfd.FileName, true);
                new ToastNotificationWindow(
                    isGif ? "¡GIF animado guardado con éxito! 🎞️" : "¡Clip de video guardado con éxito! 💾",
                    "/Assets/Icons/save.png"
                ).Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error al guardar: {ex.Message}");
            }
        }
    }

    private void BtnDiscard_Click(object sender, RoutedEventArgs e)
    {
        CleanupAndClose();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CleanupAndClose();
    }

    private void CleanupAndClose()
    {
        try
        {
            _playbackTimer?.Stop();
            MediaVideoPlayer.Stop();
            MediaVideoPlayer.Close();
        }
        catch { }

        Close();
    }
}
