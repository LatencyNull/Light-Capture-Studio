using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using LightCapture.App.Services;
using LightCapture.Core.Models;

namespace LightCapture.App.Views;

public partial class VideoPreviewWindow : Window
{
    private readonly string _videoPath;
    private string? _cachedTrimmedPath;
    private bool _isPlaying = true;

    private static readonly System.Windows.Media.Color[] AnnotationPalette = new[]
    {
        System.Windows.Media.Color.FromRgb(239, 68, 68),   // Rojo
        System.Windows.Media.Color.FromRgb(59, 130, 246),  // Azul
        System.Windows.Media.Color.FromRgb(16, 185, 129),  // Verde
        System.Windows.Media.Color.FromRgb(245, 158, 11),  // Naranja/Amarillo
        System.Windows.Media.Color.FromRgb(139, 92, 246),  // Morado
        System.Windows.Media.Colors.White
    };
    private int _paletteIndex = 0;
    private DrawingToolType _currentVideoTool = DrawingToolType.None;
    private Point _textInputPosition;

    private TimeSpan _totalDuration = TimeSpan.Zero;
    private TimeSpan _trimStart = TimeSpan.Zero;
    private TimeSpan _trimEnd = TimeSpan.Zero;

    private bool _isDraggingStart;
    private bool _isDraggingEnd;
    private double _dragOffsetX;
    private DateTime _lastDragSeekTime = DateTime.MinValue;
    private bool _isLooping = false;
    private DateTime _lastLoopSeekTime = DateTime.MinValue;
    private DispatcherTimer? _playbackTimer;
    private DateTime _lastSocialMenuCloseTime = DateTime.MinValue;
    private bool _isScrubbingTimeline;
    private DateTime _lastScrubSeekTime = DateTime.MinValue;

    private readonly List<(DrawingElement Element, Border Pill, TextBlock TimeBadge, Border BadgeBorder, Canvas Canvas)> _trackPillBindings = new();

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

        // Configurar capa de anotaciones vectoriales
        VideoDrawingCanvas.CurrentColor = AnnotationPalette[0];
        VideoDrawingCanvas.StrokeThickness = 3.5;
        VideoDrawingCanvas.DrawingChanged += () =>
        {
            _cachedTrimmedPath = null;
            Dispatcher.Invoke(RebuildObjectTracks);
        };

        // Inicializar reproductor de video
        MediaVideoPlayer.Source = new Uri(_videoPath, UriKind.Absolute);
        MediaVideoPlayer.Play();
    }

    private void MediaVideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (MediaVideoPlayer.NaturalDuration.HasTimeSpan)
        {
            _totalDuration = MediaVideoPlayer.NaturalDuration.TimeSpan;
            _trimStart = TimeSpan.Zero;
            _trimEnd = _totalDuration;

            VideoDrawingCanvas.TotalVideoDuration = _totalDuration;
            VideoDrawingCanvas.DefaultStartTime = TimeSpan.Zero;

            bool isGif = _videoPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
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
    }

    private void StartPlaybackTimer()
    {
        _playbackTimer?.Stop();
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _playbackTimer.Tick += (s, e) =>
        {
            if (!_isPlaying || _isDraggingStart || _isDraggingEnd || _isScrubbingTimeline || _totalDuration.TotalSeconds <= 0) return;

            var pos = MediaVideoPlayer.Position;

            // Si estábamos en proceso de rebobinado por bucle, verificar si ya se completó el seek
            if (_isLooping)
            {
                if (Math.Abs((pos - _trimStart).TotalMilliseconds) < 300 || 
                    (DateTime.UtcNow - _lastLoopSeekTime).TotalMilliseconds > 400)
                {
                    _isLooping = false;
                }
                else
                {
                    return; // Esperar a que el decodificador termine el seek a _trimStart
                }
            }

            // Al alcanzar el límite final del recorte, detener la reproducción (no repetir en bucle infinito)
            if (pos >= _trimEnd)
            {
                MediaVideoPlayer.Pause();
                _isPlaying = false;
                TxtPlayPauseIcon.Text = "▶";
                PauseIndicator.Visibility = Visibility.Visible;
                MediaVideoPlayer.Position = _trimEnd;
                pos = _trimEnd;
            }

            // Actualizar aguja de reproducción (playhead) en el espacio del track
            double trackWidth = TimelineContainer.ActualWidth;
            double pinWidth = PinStart.ActualWidth > 0 ? PinStart.ActualWidth : 14;
            double maxTravel = trackWidth - pinWidth;
            if (maxTravel > 0)
            {
                double ratio = Math.Max(0, Math.Min(pos.TotalSeconds / _totalDuration.TotalSeconds, 1.0));
                double needleX = Math.Max(0, Math.Min(ratio * maxTravel + (pinWidth / 2) - 1, trackWidth - 3));
                Canvas.SetLeft(Playhead, needleX);
            }

            TxtPlaybackPosition.Text = $"{pos.Minutes:D2}:{pos.Seconds:D2} / {_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";
            VideoDrawingCanvas.CurrentPlaybackTime = pos;
            VideoDrawingCanvas.InvalidateVisual();
            HighlightActiveTracks(pos);
        };
        _playbackTimer.Start();
    }

    private void TimelineContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTimelinePins();
        UpdateAllPillPositions();
    }

    private void UpdateTimelinePins()
    {
        double trackWidth = TimelineContainer.ActualWidth;
        if (trackWidth <= 0 || _totalDuration.TotalSeconds <= 0) return;

        double pinWidth = PinStart.ActualWidth > 0 ? PinStart.ActualWidth : 14;
        double maxTravel = trackWidth - pinWidth;
        if (maxTravel <= 0) return;

        double startRatio = Math.Max(0, Math.Min(_trimStart.TotalSeconds / _totalDuration.TotalSeconds, 1.0));
        double endRatio = Math.Max(0, Math.Min(_trimEnd.TotalSeconds / _totalDuration.TotalSeconds, 1.0));

        double startX = startRatio * maxTravel;
        double endX = endRatio * maxTravel;

        Canvas.SetLeft(PinStart, startX);
        Canvas.SetLeft(PinEnd, Math.Max(startX + 20, endX));

        ActiveRangeTrack.Margin = new Thickness(startX, 0, 0, 0);
        ActiveRangeTrack.Width = Math.Max(0, (endX + pinWidth) - startX);

        double needleX = Math.Max(0, Math.Min(startX + (pinWidth / 2) - 1, trackWidth - 3));
        Canvas.SetLeft(Playhead, needleX);

        UpdateTrimLabels();
    }

    private void UpdateTrimLabels()
    {
        var trimDuration = _trimEnd - _trimStart;
        bool isTrimmed = _trimStart > TimeSpan.FromMilliseconds(250) || 
                         (_totalDuration - _trimEnd) > TimeSpan.FromMilliseconds(250);

        // Formato fijo invariable mm:ss (siempre exactamente 5 caracteres para ancho constante)
        TxtTrimDuration.Text = $"{trimDuration.Minutes:D2}:{trimDuration.Seconds:D2}";

        // Usar Visibility.Hidden para que el espacio se preserve y la línea de tiempo permanezca 100% inmóvil
        BtnResetTrim.Visibility = isTrimmed ? Visibility.Visible : Visibility.Hidden;
        BadgeTrimInfo.BorderBrush = isTrimmed 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)) 
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));

        TxtCopyClipLabel.Text = isTrimmed ? "Copiar Recorte" : "Copiar Clip";
        TxtSaveClipLabel.Text = isTrimmed ? "Guardar Recorte..." : "Guardar...";
    }

    private void PinStart_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingStart = true;
        _dragOffsetX = e.GetPosition(PinStart).X;
        PinStart.CaptureMouse();
        MediaVideoPlayer.Pause();
        e.Handled = true;
    }

    private void PinEnd_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingEnd = true;
        _dragOffsetX = e.GetPosition(PinEnd).X;
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
            MediaVideoPlayer.Position = _trimStart;
        }

        if (_isDraggingEnd)
        {
            _isDraggingEnd = false;
            PinEnd.ReleaseMouseCapture();
            _cachedTrimmedPath = null;
            // Al soltar el corchete de fin, volver al inicio del recorte para reproducción continua limpia
            MediaVideoPlayer.Position = _trimStart;
        }

        _isLooping = false;

        // Reposicionar aguja y medidor de tiempo con precisión
        double trackWidth = TimelineContainer.ActualWidth;
        double pinWidth = PinStart.ActualWidth > 0 ? PinStart.ActualWidth : 14;
        double maxTravel = trackWidth - pinWidth;
        if (maxTravel > 0 && _totalDuration.TotalSeconds > 0)
        {
            double ratio = _trimStart.TotalSeconds / _totalDuration.TotalSeconds;
            double needleX = Math.Max(0, Math.Min(ratio * maxTravel + (pinWidth / 2) - 1, trackWidth - 3));
            Canvas.SetLeft(Playhead, needleX);
        }
        TxtPlaybackPosition.Text = $"{_trimStart.Minutes:D2}:{_trimStart.Seconds:D2} / {_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";

        MediaVideoPlayer.Pause();
        _isPlaying = false;
        TxtPlayPauseIcon.Text = "▶";
        PauseIndicator.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void PinStart_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingStart || _totalDuration.TotalSeconds <= 0) return;

        double trackWidth = TimelineContainer.ActualWidth;
        double pinWidth = PinStart.ActualWidth > 0 ? PinStart.ActualWidth : 14;
        double maxTravel = trackWidth - pinWidth;
        if (maxTravel <= 0) return;

        double curX = e.GetPosition(TimelineCanvas).X - _dragOffsetX;
        double endX = Canvas.GetLeft(PinEnd);

        // Distancia mínima de 20px entre corchetes
        double newStartX = Math.Max(0, Math.Min(curX, endX - 20));

        // Ajuste magnético suave al borde izquierdo
        if (newStartX <= 4)
        {
            newStartX = 0;
            _trimStart = TimeSpan.Zero;
        }
        else
        {
            _trimStart = TimeSpan.FromSeconds((newStartX / maxTravel) * _totalDuration.TotalSeconds);
        }

        Canvas.SetLeft(PinStart, newStartX);
        ActiveRangeTrack.Margin = new Thickness(newStartX, 0, 0, 0);
        ActiveRangeTrack.Width = Math.Max(0, (endX + pinWidth) - newStartX);

        // Aguja visual centrada en el corchete
        double needleX = Math.Max(0, Math.Min(newStartX + (pinWidth / 2) - 1, trackWidth - 3));
        Canvas.SetLeft(Playhead, needleX);

        // Sincronización exacta en tiempo real del medidor de tiempo
        TxtPlaybackPosition.Text = $"{_trimStart.Minutes:D2}:{_trimStart.Seconds:D2} / {_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";
        UpdateTrimLabels();

        // Throttle para previsualización suave del fotograma sin saturar el decodificador
        if ((DateTime.UtcNow - _lastDragSeekTime).TotalMilliseconds > 60)
        {
            _lastDragSeekTime = DateTime.UtcNow;
            MediaVideoPlayer.Position = _trimStart;
        }
    }

    private void PinEnd_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingEnd || _totalDuration.TotalSeconds <= 0) return;

        double trackWidth = TimelineContainer.ActualWidth;
        double pinWidth = PinEnd.ActualWidth > 0 ? PinEnd.ActualWidth : 14;
        double maxTravel = trackWidth - pinWidth;
        if (maxTravel <= 0) return;

        double curX = e.GetPosition(TimelineCanvas).X - _dragOffsetX;
        double startX = Canvas.GetLeft(PinStart);

        // Distancia mínima de 20px entre corchetes
        double newEndX = Math.Max(startX + 20, Math.Min(curX, maxTravel));

        // Ajuste magnético suave al borde derecho
        if (newEndX >= maxTravel - 4)
        {
            newEndX = maxTravel;
            _trimEnd = _totalDuration;
        }
        else
        {
            _trimEnd = TimeSpan.FromSeconds((newEndX / maxTravel) * _totalDuration.TotalSeconds);
        }

        Canvas.SetLeft(PinEnd, newEndX);
        ActiveRangeTrack.Width = Math.Max(0, (newEndX + pinWidth) - startX);

        // Aguja visual centrada en el corchete
        double needleX = Math.Max(0, Math.Min(newEndX + (pinWidth / 2) - 1, trackWidth - 3));
        Canvas.SetLeft(Playhead, needleX);

        // Sincronización exacta en tiempo real del medidor de tiempo
        TxtPlaybackPosition.Text = $"{_trimEnd.Minutes:D2}:{_trimEnd.Seconds:D2} / {_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";
        UpdateTrimLabels();

        // Throttle para previsualización suave del fotograma sin saturar el decodificador
        if ((DateTime.UtcNow - _lastDragSeekTime).TotalMilliseconds > 60)
        {
            _lastDragSeekTime = DateTime.UtcNow;
            MediaVideoPlayer.Position = _trimEnd;
        }
    }

    private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingStart || _isDraggingEnd) return;
        if (_totalDuration.TotalSeconds <= 0) return;

        _isScrubbingTimeline = true;

        // Pausar y mantener en pausa
        MediaVideoPlayer.Pause();
        _isPlaying = false;
        TxtPlayPauseIcon.Text = "▶";
        PauseIndicator.Visibility = Visibility.Collapsed;

        TimelineCanvas.CaptureMouse();
        _lastScrubSeekTime = DateTime.UtcNow;
        SeekToTimelinePosition(e.GetPosition(TimelineCanvas).X);
        e.Handled = true;
    }

    private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isScrubbingTimeline && e.LeftButton == MouseButtonState.Pressed)
        {
            // Throttle a 25ms (~40 fps) para decodificación suave y fluida en tiempo real
            if ((DateTime.UtcNow - _lastScrubSeekTime).TotalMilliseconds >= 25)
            {
                _lastScrubSeekTime = DateTime.UtcNow;
                SeekToTimelinePosition(e.GetPosition(TimelineCanvas).X);
            }
            e.Handled = true;
        }
    }

    private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isScrubbingTimeline)
        {
            _isScrubbingTimeline = false;
            TimelineCanvas.ReleaseMouseCapture();

            // Garantizar fotograma exacto en el punto de soltado
            SeekToTimelinePosition(e.GetPosition(TimelineCanvas).X);

            // Mantenerse siempre en pausa ("quedar muerta" hasta que el usuario presione Play)
            MediaVideoPlayer.Pause();
            _isPlaying = false;
            TxtPlayPauseIcon.Text = "▶";
            PauseIndicator.Visibility = Visibility.Visible;

            e.Handled = true;
        }
    }

    private void SeekToTimelinePosition(double clickX)
    {
        if (_totalDuration.TotalSeconds <= 0) return;

        double trackWidth = TimelineContainer.ActualWidth;
        double pinWidth = PinStart.ActualWidth > 0 ? PinStart.ActualWidth : 14;
        double maxTravel = trackWidth - pinWidth;
        if (maxTravel <= 0) return;

        // Centrado respecto al pin para que el cursor y la aguja coincidan con precisión matemática
        double adjustedX = clickX - (pinWidth / 2.0);
        double ratio = Math.Max(0, Math.Min(adjustedX / maxTravel, 1.0));
        var targetTime = TimeSpan.FromSeconds(ratio * _totalDuration.TotalSeconds);

        // Clamp dentro del rango recortado
        if (targetTime < _trimStart) targetTime = _trimStart;
        if (targetTime > _trimEnd) targetTime = _trimEnd;

        _isLooping = false;
        MediaVideoPlayer.Position = targetTime;

        double needleRatio = targetTime.TotalSeconds / _totalDuration.TotalSeconds;
        double needleX = Math.Max(0, Math.Min(needleRatio * maxTravel + (pinWidth / 2) - 1, trackWidth - 3));
        Canvas.SetLeft(Playhead, needleX);
        TxtPlaybackPosition.Text = $"{targetTime.Minutes:D2}:{targetTime.Seconds:D2} / {_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";
        VideoDrawingCanvas.CurrentPlaybackTime = targetTime;
        VideoDrawingCanvas.InvalidateVisual();
        HighlightActiveTracks(targetTime);
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        TogglePlayPause();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void StopPlayback()
    {
        MediaVideoPlayer.Pause();
        _isPlaying = false;
        TxtPlayPauseIcon.Text = "▶";
        PauseIndicator.Visibility = Visibility.Visible;

        var targetTime = _trimStart;
        MediaVideoPlayer.Position = targetTime;

        double trackWidth = TimelineContainer.ActualWidth;
        double pinWidth = PinStart.ActualWidth > 0 ? PinStart.ActualWidth : 14;
        double maxTravel = trackWidth - pinWidth;
        if (maxTravel > 0 && _totalDuration.TotalSeconds > 0)
        {
            double needleRatio = targetTime.TotalSeconds / _totalDuration.TotalSeconds;
            double needleX = Math.Max(0, Math.Min(needleRatio * maxTravel + (pinWidth / 2) - 1, trackWidth - 3));
            Canvas.SetLeft(Playhead, needleX);
        }

        TxtPlaybackPosition.Text = $"{targetTime.Minutes:D2}:{targetTime.Seconds:D2} / {_totalDuration.Minutes:D2}:{_totalDuration.Seconds:D2}";
        VideoDrawingCanvas.CurrentPlaybackTime = targetTime;
        VideoDrawingCanvas.InvalidateVisual();
        HighlightActiveTracks(targetTime);
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
        MediaVideoPlayer.Pause();
        _isPlaying = false;
        TxtPlayPauseIcon.Text = "▶";
        PauseIndicator.Visibility = Visibility.Visible;
        MediaVideoPlayer.Position = _trimEnd;
    }

    private void SetVideoTool(DrawingToolType tool)
    {
        CommitFloatingText();
        _currentVideoTool = tool;
        VideoDrawingCanvas.CurrentTool = tool;
        VideoDrawingCanvas.DefaultStartTime = MediaVideoPlayer.Position;
        VideoDrawingCanvas.TotalVideoDuration = _totalDuration;

        if (tool != DrawingToolType.None && _isPlaying)
        {
            MediaVideoPlayer.Pause();
            _isPlaying = false;
            TxtPlayPauseIcon.Text = "▶";
            PauseIndicator.Visibility = Visibility.Visible;
        }

        VideoDrawingCanvas.Cursor = tool switch
        {
            DrawingToolType.None => System.Windows.Input.Cursors.Hand,
            DrawingToolType.Text => System.Windows.Input.Cursors.IBeam,
            _ => System.Windows.Input.Cursors.Cross
        };
    }

    private void BtnToolPointer_Click(object sender, RoutedEventArgs e) => SetVideoTool(DrawingToolType.None);
    private void BtnToolArrow_Click(object sender, RoutedEventArgs e) => SetVideoTool(DrawingToolType.Arrow);
    private void BtnToolText_Click(object sender, RoutedEventArgs e) => SetVideoTool(DrawingToolType.Text);
    private void BtnToolPen_Click(object sender, RoutedEventArgs e) => SetVideoTool(DrawingToolType.Pen);
    private void BtnToolRect_Click(object sender, RoutedEventArgs e) => SetVideoTool(DrawingToolType.Rectangle);
    private void BtnToolRedact_Click(object sender, RoutedEventArgs e) => SetVideoTool(DrawingToolType.Redact);

    private void BtnToolColor_Click(object sender, RoutedEventArgs e)
    {
        _paletteIndex = (_paletteIndex + 1) % AnnotationPalette.Length;
        var newColor = AnnotationPalette[_paletteIndex];
        VideoDrawingCanvas.CurrentColor = newColor;
        VideoColorIndicator.Background = new SolidColorBrush(newColor);
        VideoFloatingInput.Foreground = new SolidColorBrush(newColor);
    }

    private void BtnToolUndo_Click(object sender, RoutedEventArgs e)
    {
        CommitFloatingText();
        VideoDrawingCanvas.Undo();
    }

    private void BtnToolClear_Click(object sender, RoutedEventArgs e)
    {
        CommitFloatingText();
        VideoDrawingCanvas.ClearAll();
    }

    private void VideoDrawingCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        // Si el clic ocurrió sobre la barra de herramientas de anotación, no interferir
        if (e.OriginalSource is DependencyObject dep)
        {
            DependencyObject? cur = dep;
            while (cur != null && cur != VideoDrawingCanvas)
            {
                if (cur == AnnotationToolbar) return;
                cur = VisualTreeHelper.GetParent(cur);
            }
        }

        Point p = e.GetPosition(VideoDrawingCanvas);

        if (_currentVideoTool == DrawingToolType.None)
        {
            TogglePlayPause();
            e.Handled = true;
            return;
        }

        VideoDrawingCanvas.DefaultStartTime = MediaVideoPlayer.Position;
        VideoDrawingCanvas.TotalVideoDuration = _totalDuration;

        if (_currentVideoTool == DrawingToolType.Text)
        {
            CommitFloatingText();
            ShowFloatingTextInput(p);
            e.Handled = true;
            return;
        }

        CommitFloatingText();
        VideoDrawingCanvas.CaptureMouse();
        VideoDrawingCanvas.StartDrawing(p);
        e.Handled = true;
    }

    private void VideoDrawingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_currentVideoTool == DrawingToolType.None || _currentVideoTool == DrawingToolType.Text) return;
        if (e.LeftButton == MouseButtonState.Pressed && VideoDrawingCanvas.IsMouseCaptured)
        {
            Point p = e.GetPosition(VideoDrawingCanvas);
            VideoDrawingCanvas.UpdateDrawing(p);
            e.Handled = true;
        }
    }

    private void VideoDrawingCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_currentVideoTool == DrawingToolType.None || _currentVideoTool == DrawingToolType.Text) return;
        if (VideoDrawingCanvas.IsMouseCaptured)
        {
            VideoDrawingCanvas.ReleaseMouseCapture();
        }
        Point p = e.GetPosition(VideoDrawingCanvas);
        VideoDrawingCanvas.EndDrawing(p);
        e.Handled = true;
    }

    private void ShowFloatingTextInput(Point p)
    {
        double maxLeft = Math.Max(0, VideoContainer.ActualWidth - 140);
        double maxTop = Math.Max(0, VideoContainer.ActualHeight - 40);
        Point adjusted = new Point(Math.Min(maxLeft, Math.Max(0, p.X)), Math.Min(maxTop, Math.Max(0, p.Y)));
        _textInputPosition = adjusted;

        VideoFloatingInput.Margin = new Thickness(adjusted.X, adjusted.Y, 0, 0);
        VideoFloatingInput.Foreground = new SolidColorBrush(VideoDrawingCanvas.CurrentColor);
        VideoFloatingInput.Text = string.Empty;
        VideoFloatingInput.Visibility = Visibility.Visible;
        VideoFloatingInput.Focus();
    }

    private void CommitFloatingText()
    {
        if (VideoFloatingInput.Visibility != Visibility.Visible) return;
        string text = VideoFloatingInput.Text.Trim();
        VideoFloatingInput.Visibility = Visibility.Collapsed;
        if (!string.IsNullOrEmpty(text))
        {
            VideoDrawingCanvas.AddTextElement(_textInputPosition, text);
        }
    }

    private void VideoFloatingInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            CommitFloatingText();
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            VideoFloatingInput.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void VideoFloatingInput_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitFloatingText();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Z && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            CommitFloatingText();
            VideoDrawingCanvas.Undo();
            e.Handled = true;
        }
    }

    private void VideoPlayer_MouseClick(object sender, MouseButtonEventArgs e)
    {
        TogglePlayPause();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        System.Windows.Point pos = e.GetPosition(this);
        // Zona superior de la ventana (estilo barra de título de Windows, primeros 65 píxeles)
        if (pos.Y <= 65)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? current = dep;
                while (current != null && current != this)
                {
                    if (current is System.Windows.Controls.Primitives.ButtonBase)
                        return; // Permitir que los botones (como el botón ✕) procesen su clic
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            try
            {
                DragMove();
            }
            catch { }
        }
    }

    #region Pistas de Objetos estilo CapCut

    private void RebuildObjectTracks()
    {
        _trackPillBindings.Clear();
        ObjectTracksStackPanel.Children.Clear();

        var elements = VideoDrawingCanvas.Elements;
        if (elements.Count == 0)
        {
            ObjectTracksPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ObjectTracksPanel.Visibility = Visibility.Visible;
        TxtObjectTracksCount.Text = $"{elements.Count} {(elements.Count == 1 ? "objeto" : "objetos")}";

        foreach (var elem in elements)
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 2, 0, 2), Height = 28 };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Columna 0: Información y botón de eliminar
            var infoPanel = new DockPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };

            var btnDelete = new System.Windows.Controls.Button
            {
                Content = new TextBlock { Text = "✕", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)) },
                Width = 20,
                Height = 20,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Eliminar este objeto del video",
                Margin = new Thickness(4, 0, 0, 0)
            };
            DockPanel.SetDock(btnDelete, Dock.Right);
            btnDelete.Click += (s, e) => VideoDrawingCanvas.RemoveElement(elem);
            infoPanel.Children.Add(btnDelete);

            var badgeBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var txtTimeBadge = new TextBlock
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Segoe UI"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Center
            };
            badgeBorder.Child = txtTimeBadge;
            DockPanel.SetDock(badgeBorder, Dock.Right);
            infoPanel.Children.Add(badgeBorder);

            var leftStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var txtIcon = new TextBlock { Text = elem.IconEmoji, FontSize = 12, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center };
            var colorDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(elem.Color),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            string labelText = elem.DisplayName;
            if (elem is TextDrawingElement tde && !string.IsNullOrWhiteSpace(tde.Text))
            {
                string clean = tde.Text.Replace("\r", " ").Replace("\n", " ").Trim();
                string preview = clean.Length > 8 ? clean.Substring(0, 8) + "…" : clean;
                labelText = $"\"{preview}\"";
            }

            var txtName = new TextBlock
            {
                Text = labelText,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = (System.Windows.Media.Brush)FindResource("PrimaryText"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 65
            };
            leftStack.Children.Add(txtIcon);
            leftStack.Children.Add(colorDot);
            leftStack.Children.Add(txtName);

            infoPanel.Children.Add(leftStack);
            Grid.SetColumn(infoPanel, 0);
            rowGrid.Children.Add(infoPanel);

            // Columna 1: Pista y barra interactiva (Pill)
            var trackBorder = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 15, 23, 42)),
                BorderBrush = (System.Windows.Media.Brush)FindResource("ToolbarSeparator"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true
            };
            var trackCanvas = new Canvas
            {
                Background = System.Windows.Media.Brushes.Transparent,
                ClipToBounds = true,
                Height = 22
            };
            trackBorder.Child = trackCanvas;
            Grid.SetColumn(trackBorder, 1);
            rowGrid.Children.Add(trackBorder);

            // Bloque / Pill arrastrable y redimensionable
            var pill = new Border
            {
                Height = 18,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, elem.Color.R, elem.Color.G, elem.Color.B)),
                BorderBrush = new SolidColorBrush(elem.Color),
                BorderThickness = new Thickness(1.5),
                Cursor = System.Windows.Input.Cursors.Hand,
                ClipToBounds = true,
                ToolTip = "Arrastra el bloque para moverlo en el tiempo, o los bordes para cambiar cuánto dura"
            };
            Canvas.SetTop(pill, 2);

            var pillGrid = new Grid();
            var pillText = new TextBlock
            {
                Text = $"{elem.IconEmoji} {labelText}",
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 0, 6, 0),
                IsHitTestVisible = false
            };
            pillGrid.Children.Add(pillText);

            var leftHandle = new Border
            {
                Width = 5,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 255, 255, 255)),
                Cursor = System.Windows.Input.Cursors.SizeWE,
                IsHitTestVisible = false
            };
            var rightHandle = new Border
            {
                Width = 5,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 255, 255, 255)),
                Cursor = System.Windows.Input.Cursors.SizeWE,
                IsHitTestVisible = false
            };
            pillGrid.Children.Add(leftHandle);
            pillGrid.Children.Add(rightHandle);
            pill.Child = pillGrid;

            trackCanvas.Children.Add(pill);

            var binding = (elem, pill, txtTimeBadge, badgeBorder, trackCanvas);
            _trackPillBindings.Add(binding);

            SetupPillInteractions(elem, pill, trackCanvas, txtTimeBadge);

            ObjectTracksStackPanel.Children.Add(rowGrid);
        }

        UpdateAllPillPositions();
    }

    private void SetupPillInteractions(DrawingElement elem, Border pill, Canvas trackCanvas, TextBlock txtTimeBadge)
    {
        int dragMode = 0; // 0=None, 1=Move, 2=ResizeStart, 3=ResizeEnd
        double dragStartMouseX = 0;
        TimeSpan initialStart = TimeSpan.Zero;
        TimeSpan initialEnd = TimeSpan.Zero;

        pill.MouseMove += (s, e) =>
        {
            if (!pill.IsMouseCaptured)
            {
                Point pt = e.GetPosition(pill);
                if (pt.X <= 6 || pt.X >= pill.ActualWidth - 6)
                {
                    pill.Cursor = System.Windows.Input.Cursors.SizeWE;
                }
                else
                {
                    pill.Cursor = System.Windows.Input.Cursors.Hand;
                }
                return;
            }

            double currentMouseX = e.GetPosition(trackCanvas).X;
            double deltaX = currentMouseX - dragStartMouseX;
            double cWidth = trackCanvas.ActualWidth;
            if (cWidth <= 0 || _totalDuration.TotalSeconds <= 0) return;

            double deltaSec = (deltaX / cWidth) * _totalDuration.TotalSeconds;

            if (dragMode == 1) // Mover bloque completo
            {
                var duration = initialEnd - initialStart;
                double newStart = Math.Max(0, Math.Min(_totalDuration.TotalSeconds - duration.TotalSeconds, initialStart.TotalSeconds + deltaSec));
                elem.StartTime = TimeSpan.FromSeconds(newStart);
                elem.EndTime = elem.StartTime + duration;
            }
            else if (dragMode == 2) // Redimensionar inicio
            {
                double minDur = 0.3;
                double newStart = Math.Max(0, Math.Min(initialEnd.TotalSeconds - minDur, initialStart.TotalSeconds + deltaSec));
                elem.StartTime = TimeSpan.FromSeconds(newStart);
            }
            else if (dragMode == 3) // Redimensionar fin
            {
                double minDur = 0.3;
                double newEnd = Math.Max(initialStart.TotalSeconds + minDur, Math.Min(_totalDuration.TotalSeconds, initialEnd.TotalSeconds + deltaSec));
                elem.EndTime = TimeSpan.FromSeconds(newEnd);
            }

            UpdateSinglePillPosition(elem, pill, trackCanvas, txtTimeBadge);
            _cachedTrimmedPath = null;
            VideoDrawingCanvas.InvalidateVisual();

            var targetTime = (dragMode == 3) ? (elem.EndTime ?? TimeSpan.Zero) : (elem.StartTime ?? TimeSpan.Zero);
            MediaVideoPlayer.Position = targetTime;
            e.Handled = true;
        };

        pill.MouseLeftButtonDown += (s, e) =>
        {
            Point pt = e.GetPosition(pill);
            if (pt.X <= 6)
                dragMode = 2; // Resize start
            else if (pt.X >= pill.ActualWidth - 6)
                dragMode = 3; // Resize end
            else
                dragMode = 1; // Move

            dragStartMouseX = e.GetPosition(trackCanvas).X;
            initialStart = elem.StartTime ?? TimeSpan.Zero;
            initialEnd = elem.EndTime ?? _totalDuration;

            pill.CaptureMouse();
            MediaVideoPlayer.Pause();
            _isPlaying = false;
            TxtPlayPauseIcon.Text = "▶";
            PauseIndicator.Visibility = Visibility.Visible;
            e.Handled = true;
        };

        pill.MouseLeftButtonUp += (s, e) =>
        {
            if (pill.IsMouseCaptured)
            {
                pill.ReleaseMouseCapture();
                dragMode = 0;
                _cachedTrimmedPath = null;
                e.Handled = true;
            }
        };

        trackCanvas.SizeChanged += (s, e) =>
        {
            UpdateSinglePillPosition(elem, pill, trackCanvas, txtTimeBadge);
        };
    }

    private void UpdateSinglePillPosition(DrawingElement elem, Border pill, Canvas trackCanvas, TextBlock txtTimeBadge)
    {
        double cWidth = trackCanvas.ActualWidth;
        if (cWidth <= 0 || _totalDuration.TotalSeconds <= 0) return;

        var start = elem.StartTime ?? TimeSpan.Zero;
        var end = elem.EndTime ?? _totalDuration;
        if (end > _totalDuration) end = _totalDuration;
        if (end < start) end = start + TimeSpan.FromSeconds(0.5);

        double startRatio = Math.Max(0, Math.Min(start.TotalSeconds / _totalDuration.TotalSeconds, 1.0));
        double endRatio = Math.Max(0, Math.Min(end.TotalSeconds / _totalDuration.TotalSeconds, 1.0));

        double x = startRatio * cWidth;
        double w = Math.Max(18, (endRatio - startRatio) * cWidth);

        Canvas.SetLeft(pill, x);
        pill.Width = Math.Min(w, cWidth - x);

        txtTimeBadge.Text = $"{start.Minutes:D2}:{start.Seconds:D2} - {end.Minutes:D2}:{end.Seconds:D2}";
    }

    private void UpdateAllPillPositions()
    {
        foreach (var b in _trackPillBindings)
        {
            UpdateSinglePillPosition(b.Element, b.Pill, b.Canvas, b.TimeBadge);
        }
    }

    private void HighlightActiveTracks(TimeSpan currentPos)
    {
        foreach (var b in _trackPillBindings)
        {
            var start = b.Element.StartTime ?? TimeSpan.Zero;
            var end = b.Element.EndTime ?? _totalDuration;
            bool isActive = currentPos >= start && currentPos <= end;

            if (isActive)
            {
                b.Pill.BorderThickness = new Thickness(2.0);
                b.BadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 56, 189, 248));
                b.TimeBadge.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                b.Pill.BorderThickness = new Thickness(1.5);
                b.BadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 56, 189, 248));
                b.TimeBadge.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
            }
        }
    }

    #endregion

    private async Task<string> GetEffectiveVideoPathAsync()
    {
        bool isTrimmed = _trimStart > TimeSpan.FromMilliseconds(150) || 
                         (_totalDuration - _trimEnd) > TimeSpan.FromMilliseconds(150);
        bool hasAnnotations = VideoDrawingCanvas != null && VideoDrawingCanvas.HasElements;

        if (!isTrimmed && !hasAnnotations)
        {
            return _videoPath;
        }

        if (!string.IsNullOrEmpty(_cachedTrimmedPath) && File.Exists(_cachedTrimmedPath))
        {
            return _cachedTrimmedPath;
        }

        try
        {
            List<TimedOverlay>? timedOverlays = null;
            if (hasAnnotations && VideoDrawingCanvas != null)
            {
                int natW = MediaVideoPlayer.NaturalVideoWidth > 0 ? MediaVideoPlayer.NaturalVideoWidth : (int)VideoContainer.ActualWidth;
                int natH = MediaVideoPlayer.NaturalVideoHeight > 0 ? MediaVideoPlayer.NaturalVideoHeight : (int)VideoContainer.ActualHeight;

                string tempDir = Path.Combine(Path.GetTempPath(), "LightCapture_Recordings");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                timedOverlays = new List<TimedOverlay>();
                foreach (var elem in VideoDrawingCanvas.Elements)
                {
                    string elemPng = Path.Combine(tempDir, $"OverlayElem_{elem.Id}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png");
                    VideoDrawingCanvas.ExportSingleElementOverlayToPng(elem, elemPng, natW, natH, VideoContainer.ActualWidth, VideoContainer.ActualHeight);

                    var start = elem.StartTime ?? TimeSpan.Zero;
                    var end = elem.EndTime ?? _totalDuration;
                    if (end <= start) end = start + TimeSpan.FromSeconds(1);

                    timedOverlays.Add(new TimedOverlay(elemPng, start, end));
                }
            }

            string toastMsg = hasAnnotations && isTrimmed
                ? "✂️🎨 Procesando recorte y pistas del video..."
                : (hasAnnotations ? "🎨 Estampando pistas de objetos en el video..." : "✂️ Procesando recorte del clip...");

            new ToastNotificationWindow(toastMsg, "/Assets/Icons/save.png").Show();
            _cachedTrimmedPath = await VideoTrimmerService.TrimAsync(_videoPath, _trimStart, _trimEnd, null, timedOverlays);
            return _cachedTrimmedPath;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"No se pudo procesar el clip: {ex.Message}. Se usará el video original.", "Exportación de Clip");
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
