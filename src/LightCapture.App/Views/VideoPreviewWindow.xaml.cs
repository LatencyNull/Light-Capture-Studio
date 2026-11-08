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

        // Extraer peso del archivo
        try
        {
            var fileInfo = new FileInfo(_videoPath);
            double sizeMb = Math.Round((double)fileInfo.Length / (1024 * 1024), 2);
            TxtVideoInfo.Text = $"{sizeMb} MB";
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
                TxtVideoInfo.Text = $"{dur.Minutes:D2}:{dur.Seconds:D2} • {sizeMb} MB";
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

    private void BtnShareWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        if (BtnWhatsApp.ContextMenu != null)
        {
            BtnWhatsApp.ContextMenu.PlacementTarget = BtnWhatsApp;
            BtnWhatsApp.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            BtnWhatsApp.ContextMenu.IsOpen = true;
        }
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

    private void BtnSaveClip_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar clip de video",
            Filter = "Video MP4 (*.mp4)|*.mp4",
            FileName = $"Clip_{DateTime.Now:yyyyMMdd_HHmmss}.mp4"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.Copy(_videoPath, sfd.FileName, true);
                new ToastNotificationWindow(
                    "¡Clip de video guardado con éxito! 💾",
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
