using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace LightCapture.App.Views;

public partial class VideoPreviewWindow : Window
{
    private readonly string _videoPath;
    private bool _isPlaying = true;

    public VideoPreviewWindow(string videoPath)
    {
        InitializeComponent();

        _videoPath = videoPath;

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
                var dur = MediaVideoPlayer.NaturalDuration.TimeSpan;
                var fileInfo = new FileInfo(_videoPath);
                double sizeMb = Math.Round((double)fileInfo.Length / (1024 * 1024), 2);
                string formatTag = isGif ? "GIF • " : "";
                TxtVideoInfo.Text = $"{formatTag}{dur.Minutes:D2}:{dur.Seconds:D2} • {sizeMb} MB";
            }
            MediaVideoPlayer.Play();
        };

        MediaVideoPlayer.Play();
    }

    private void MediaVideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
        // Bucle infinito fluido (efecto GIF)
        MediaVideoPlayer.Position = TimeSpan.Zero;
        MediaVideoPlayer.Play();
    }

    private void VideoPlayer_MouseClick(object sender, MouseButtonEventArgs e)
    {
        if (_isPlaying)
        {
            MediaVideoPlayer.Pause();
            _isPlaying = false;
            PauseIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            MediaVideoPlayer.Play();
            _isPlaying = true;
            PauseIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CopyVideoFileToClipboard()
    {
        try
        {
            var files = new StringCollection { _videoPath };
            System.Windows.Clipboard.SetFileDropList(files);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error al copiar clip: {ex.Message}");
        }
    }

    private void BtnCopyClip_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();
        new ToastNotificationWindow(
            "¡Clip de video copiado! Pega con Ctrl+V en tu chat 💬",
            "/Assets/Icons/video.png"
        ).Show();
    }


    private void ShareWhatsAppApp_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();

        bool appOpened = false;
        try
        {
            Process.Start(new ProcessStartInfo("whatsapp://") { UseShellExecute = true });
            appOpened = true;
        }
        catch { }

        if (appOpened)
        {
            new ToastNotificationWindow(
                "¡Clip copiado! Pega con Ctrl+V en WhatsApp App 💬",
                "/Assets/Icons/whatsapp.png"
            ).Show();
        }
        else
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://web.whatsapp.com/") { UseShellExecute = true });
            }
            catch { }

            new ToastNotificationWindow(
                "WhatsApp App no detectado. Abriendo WhatsApp Web... 💬",
                "/Assets/Icons/whatsapp.png"
            ).Show();
        }
    }

    private void ShareWhatsAppWeb_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();

        try
        {
            Process.Start(new ProcessStartInfo("https://web.whatsapp.com/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado para WhatsApp Web! Pega con Ctrl+V en tu chat 💬",
            "/Assets/Icons/whatsapp.png"
        ).Show();
    }

    private void BtnShareDiscord_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();

        new ToastNotificationWindow(
            "¡Clip copiado para Discord! Pega con Ctrl+V en tu canal 🎮",
            "/Assets/Icons/discord.png"
        ).Show();
    }

    private void BtnMoreSocial_Click(object sender, RoutedEventArgs e)
    {
        if (BtnMoreSocial.ContextMenu != null)
        {
            BtnMoreSocial.ContextMenu.PlacementTarget = BtnMoreSocial;
            BtnMoreSocial.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            BtnMoreSocial.ContextMenu.VerticalOffset = -6;
            BtnMoreSocial.ContextMenu.IsOpen = true;
        }
    }

    private void ShareTikTok_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();
        try
        {
            Process.Start(new ProcessStartInfo("https://www.tiktok.com/upload") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo TikTok Web para subir... 🎵",
            "/Assets/Icons/tiktok.png"
        ).Show();
    }

    private void ShareInstagram_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();
        try
        {
            Process.Start(new ProcessStartInfo("https://www.instagram.com/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo Instagram... 📷",
            "/Assets/Icons/instagram.png"
        ).Show();
    }

    private void ShareFacebook_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();
        try
        {
            Process.Start(new ProcessStartInfo("https://www.facebook.com/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo Facebook... 📘",
            "/Assets/Icons/facebook.png"
        ).Show();
    }

    private void ShareTelegram_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();
        try
        {
            Process.Start(new ProcessStartInfo("https://web.telegram.org/") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado para Telegram! Pega con Ctrl+V en tu chat ✈️",
            "/Assets/Icons/telegram.png"
        ).Show();
    }

    private void ShareTwitter_Click(object sender, RoutedEventArgs e)
    {
        CopyVideoFileToClipboard();
        try
        {
            Process.Start(new ProcessStartInfo("https://x.com/compose/post") { UseShellExecute = true });
        }
        catch { }

        new ToastNotificationWindow(
            "¡Clip copiado! Abriendo X / Twitter... ✖️",
            "/Assets/Icons/twitter.png"
        ).Show();
    }

    private void BtnSaveClip_Click(object sender, RoutedEventArgs e)
    {
        bool isGif = _videoPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
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
                File.Copy(_videoPath, sfd.FileName, true);
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
            MediaVideoPlayer.Stop();
            MediaVideoPlayer.Close();
        }
        catch { }

        Close();
    }
}
