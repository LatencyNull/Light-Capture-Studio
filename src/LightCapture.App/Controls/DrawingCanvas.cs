using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightCapture.Core.Models;
using LightCapture.Core.Services;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using FlowDirection = System.Windows.FlowDirection;

namespace LightCapture.App.Controls;

public record TranslatedLensBlock(
    Rect Bounds,
    string TranslatedText,
    Color BackgroundColor,
    Color TextColor,
    double FontSize
);

public class DrawingCanvas : Canvas
{
    private readonly List<DrawingElement> _elements = new();
    private readonly Stack<DrawingElement> _undoStack = new();
    private readonly List<TranslatedLensBlock> _lensBlocks = new();

    private DrawingElement? _previewElement;
    private Point _startPoint;
    private bool _isDrawing;
    private int _currentStepNumber = 1;

    public BitmapSource? BackgroundSource { get; set; }
    public DrawingToolType CurrentTool { get; set; } = DrawingToolType.None;
    public Color CurrentColor { get; set; } = Colors.Red;
    public double StrokeThickness { get; set; } = 3.0;
    public bool ShowTranslations { get; set; } = true;

    public Rect ActiveSelectionBounds { get; set; }

    public TimeSpan? CurrentPlaybackTime { get; set; }
    public TimeSpan DefaultStartTime { get; set; } = TimeSpan.Zero;
    public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan TotalVideoDuration { get; set; } = TimeSpan.FromSeconds(10);
    public IReadOnlyList<DrawingElement> Elements => _elements;

    public event Action? DrawingChanged;

    public void RemoveElement(DrawingElement element)
    {
        _elements.Remove(element);
        _undoStack.Clear();
        InvalidateVisual();
        DrawingChanged?.Invoke();
    }

    public DrawingCanvas()
    {
        Background = System.Windows.Media.Brushes.Transparent;
        ClipToBounds = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        InvalidateVisual();
    }

    public void SetTranslatedBlocks(IEnumerable<TranslatedLensBlock> blocks)
    {
        _lensBlocks.Clear();
        _lensBlocks.AddRange(blocks);
        InvalidateVisual();
    }

    public void ToggleTranslationVisibility()
    {
        ShowTranslations = !ShowTranslations;
        InvalidateVisual();
    }

    public bool Undo()
    {
        if (_elements.Count > 0)
        {
            var last = _elements[^1];
            _elements.RemoveAt(_elements.Count - 1);
            _undoStack.Push(last);

            if (last is NumberedStepDrawingElement)
            {
                _currentStepNumber = Math.Max(1, _currentStepNumber - 1);
            }

            InvalidateVisual();
            DrawingChanged?.Invoke();
            return true;
        }
        return false;
    }

    public void ClearAll()
    {
        _elements.Clear();
        _undoStack.Clear();
        _lensBlocks.Clear();
        _previewElement = null;
        _currentStepNumber = 1;
        InvalidateVisual();
    }

    public void StartDrawing(Point point)
    {
        if (CurrentTool == DrawingToolType.None) return;

        // Estampar paso numerado con un solo clic
        if (CurrentTool == DrawingToolType.NumberedStep)
        {
            var stepElement = new NumberedStepDrawingElement(CurrentColor, point, _currentStepNumber++);
            _elements.Add(stepElement);
            _undoStack.Clear();
            InvalidateVisual();
            DrawingChanged?.Invoke();
            return;
        }

        _startPoint = point;
        _isDrawing = true;

        TimeSpan start = DefaultStartTime;
        TimeSpan end = TotalVideoDuration.TotalSeconds > 0
            ? TimeSpan.FromSeconds(Math.Min(TotalVideoDuration.TotalSeconds, start.TotalSeconds + DefaultDuration.TotalSeconds))
            : start + DefaultDuration;
        if (end <= start) end = start + TimeSpan.FromSeconds(1);

        if (CurrentTool == DrawingToolType.Pen)
        {
            _previewElement = new PenDrawingElement(CurrentColor, StrokeThickness, new List<Point> { point }) { StartTime = start, EndTime = end };
        }
        else if (CurrentTool == DrawingToolType.Marker)
        {
            _previewElement = new MarkerDrawingElement(CurrentColor, StrokeThickness, new List<Point> { point }) { StartTime = start, EndTime = end };
        }
        else if (CurrentTool == DrawingToolType.Arrow)
        {
            _previewElement = new ArrowDrawingElement(CurrentColor, StrokeThickness, point, point) { StartTime = start, EndTime = end };
        }
        else if (CurrentTool == DrawingToolType.Rectangle)
        {
            _previewElement = new RectangleDrawingElement(CurrentColor, StrokeThickness, new Rect(point.X, point.Y, 0, 0)) { StartTime = start, EndTime = end };
        }
        else if (CurrentTool == DrawingToolType.Line)
        {
            _previewElement = new LineDrawingElement(CurrentColor, StrokeThickness, point, point) { StartTime = start, EndTime = end };
        }
        else if (CurrentTool == DrawingToolType.Redact)
        {
            _previewElement = new RedactDrawingElement(new Rect(point.X, point.Y, 0, 0), Colors.Black) { StartTime = start, EndTime = end };
        }

        InvalidateVisual();
    }

    public void UpdateDrawing(Point currentPoint)
    {
        if (!_isDrawing || CurrentTool == DrawingToolType.None) return;

        TimeSpan? prevStart = _previewElement?.StartTime;
        TimeSpan? prevEnd = _previewElement?.EndTime;

        if (CurrentTool == DrawingToolType.Pen && _previewElement is PenDrawingElement penElem)
        {
            var points = new List<Point>(penElem.Points) { currentPoint };
            _previewElement = penElem with { Points = points };
        }
        else if (CurrentTool == DrawingToolType.Marker && _previewElement is MarkerDrawingElement markerElem)
        {
            var points = new List<Point>(markerElem.Points) { currentPoint };
            _previewElement = markerElem with { Points = points };
        }
        else if (CurrentTool == DrawingToolType.Line)
        {
            _previewElement = new LineDrawingElement(CurrentColor, StrokeThickness, _startPoint, currentPoint) { StartTime = prevStart, EndTime = prevEnd };
        }
        else if (CurrentTool == DrawingToolType.Arrow)
        {
            _previewElement = new ArrowDrawingElement(CurrentColor, StrokeThickness, _startPoint, currentPoint) { StartTime = prevStart, EndTime = prevEnd };
        }
        else if (CurrentTool == DrawingToolType.Rectangle)
        {
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);
            _previewElement = new RectangleDrawingElement(CurrentColor, StrokeThickness, new Rect(x, y, w, h)) { StartTime = prevStart, EndTime = prevEnd };
        }
        else if (CurrentTool == DrawingToolType.Pixelate)
        {
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);
            _previewElement = new RectangleDrawingElement(Color.FromArgb(120, 100, 100, 100), 1.5, new Rect(x, y, w, h)) { StartTime = prevStart, EndTime = prevEnd };
        }
        else if (CurrentTool == DrawingToolType.Redact)
        {
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);
            _previewElement = new RedactDrawingElement(new Rect(x, y, w, h), Colors.Black) { StartTime = prevStart, EndTime = prevEnd };
        }

        InvalidateVisual();
    }

    public void EndDrawing(Point endPoint)
    {
        if (!_isDrawing) return;
        _isDrawing = false;

        if (CurrentTool == DrawingToolType.Pixelate)
        {
            double x = Math.Min(_startPoint.X, endPoint.X);
            double y = Math.Min(_startPoint.Y, endPoint.Y);
            double w = Math.Abs(endPoint.X - _startPoint.X);
            double h = Math.Abs(endPoint.Y - _startPoint.Y);
            var bounds = new Rect(x, y, w, h);

            if (bounds.Width > 4 && bounds.Height > 4 && BackgroundSource != null)
            {
                var pixelated = PixelationHelper.CreatePixelatedBlock(BackgroundSource, bounds, 12);
                _elements.Add(new PixelateDrawingElement(bounds, pixelated));
                _undoStack.Clear();
                DrawingChanged?.Invoke();
            }
            _previewElement = null;
        }
        else if (_previewElement != null)
        {
            _elements.Add(_previewElement);
            _undoStack.Clear();
            _previewElement = null;
            DrawingChanged?.Invoke();
        }

        InvalidateVisual();
    }

    public void AddTextElement(Point position, string text, double fontSize = 18)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        TimeSpan start = DefaultStartTime;
        TimeSpan end = TotalVideoDuration.TotalSeconds > 0
            ? TimeSpan.FromSeconds(Math.Min(TotalVideoDuration.TotalSeconds, start.TotalSeconds + DefaultDuration.TotalSeconds))
            : start + DefaultDuration;
        if (end <= start) end = start + TimeSpan.FromSeconds(1);

        var textElem = new TextDrawingElement(CurrentColor, Math.Max(8, fontSize), position, text)
        {
            StartTime = start,
            EndTime = end
        };
        _elements.Add(textElem);
        _undoStack.Clear();
        InvalidateVisual();
        DrawingChanged?.Invoke();
    }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
    {
        if (new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight)).Contains(hitTestParameters.HitPoint))
        {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }
        return base.HitTestCore(hitTestParameters);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // Fondo transparente para asegurar hit testing interactivo en toda la superficie
        dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, null, new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight)));

        // 1. Renderizar bloques de Google Lens In-Place Translation si están activados
        if (ShowTranslations && _lensBlocks.Count > 0)
        {
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            foreach (var block in _lensBlocks)
            {
                var formattedText = new FormattedText(
                    block.TranslatedText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"),
                    block.FontSize,
                    new SolidColorBrush(block.TextColor),
                    dpi
                )
                {
                    MaxTextWidth = Math.Max(16, block.Bounds.Width * 1.25)
                };

                // Píldora de fondo inteligente: cubre el texto original y se adapta a la longitud en español
                double pillWidth = Math.Max(block.Bounds.Width, formattedText.Width + 8);
                double pillHeight = Math.Max(block.Bounds.Height, formattedText.Height + 4);
                var pillRect = new Rect(block.Bounds.X, block.Bounds.Y, pillWidth, pillHeight);

                var bgBrush = new SolidColorBrush(block.BackgroundColor);
                bgBrush.Freeze();
                dc.DrawRoundedRectangle(bgBrush, null, pillRect, 4, 4);

                dc.DrawText(formattedText, new Point(pillRect.X + 3, pillRect.Y + 2));
            }
        }

        // 2. Renderizar trazos y elementos de dibujo activos en el tiempo de reproducción actual
        foreach (var element in _elements)
        {
            if (CurrentPlaybackTime.HasValue && element.StartTime.HasValue && element.EndTime.HasValue)
            {
                if (CurrentPlaybackTime.Value < element.StartTime.Value || CurrentPlaybackTime.Value > element.EndTime.Value)
                {
                    continue; // Fuera del rango de tiempo
                }
            }
            element.Render(dc);
        }

        // 3. Renderizar elemento en previsualización mientras se arrastra
        _previewElement?.Render(dc);
    }

    public bool HasTranslatedBlocks => _lensBlocks.Count > 0;
    public bool HasElements => _elements.Count > 0;

    public void ClearTranslations()
    {
        _lensBlocks.Clear();
        ShowTranslations = true;
        InvalidateVisual();
    }

    public void ExportOverlayToPng(string filePath, int pixelWidth, int pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return;
        double canvasW = ActualWidth > 0 ? ActualWidth : pixelWidth;
        double canvasH = ActualHeight > 0 ? ActualHeight : pixelHeight;

        double scaleX = pixelWidth / canvasW;
        double scaleY = pixelHeight / canvasH;

        var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * scaleX, 96 * scaleY, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();

        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scaleX, scaleY));
            foreach (var element in _elements)
            {
                element.Render(dc);
            }
            dc.Pop();
        }

        rtb.Render(dv);
        rtb.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = System.IO.File.Create(filePath);
        encoder.Save(stream);
    }

    public void ExportSingleElementOverlayToPng(DrawingElement elem, string filePath, int targetVidWidth, int targetVidHeight, double containerW, double containerH)
    {
        if (targetVidWidth <= 0 || targetVidHeight <= 0) return;
        double cw = containerW > 0 ? containerW : targetVidWidth;
        double ch = containerH > 0 ? containerH : targetVidHeight;

        double s = Math.Min(cw / targetVidWidth, ch / targetVidHeight);
        double offsetX = (cw - targetVidWidth * s) / 2.0;
        double offsetY = (ch - targetVidHeight * s) / 2.0;

        var rtb = new RenderTargetBitmap(targetVidWidth, targetVidHeight, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();

        using (var dc = dv.RenderOpen())
        {
            var group = new TransformGroup();
            group.Children.Add(new TranslateTransform(-offsetX, -offsetY));
            group.Children.Add(new ScaleTransform(1.0 / s, 1.0 / s));
            dc.PushTransform(group);

            elem.Render(dc);

            dc.Pop();
        }

        rtb.Render(dv);
        rtb.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = System.IO.File.Create(filePath);
        encoder.Save(stream);
    }

    public void ExportVideoOverlayToPng(string filePath, int targetVidWidth, int targetVidHeight, double containerW, double containerH)
    {
        if (targetVidWidth <= 0 || targetVidHeight <= 0) return;
        double cw = containerW > 0 ? containerW : targetVidWidth;
        double ch = containerH > 0 ? containerH : targetVidHeight;

        double s = Math.Min(cw / targetVidWidth, ch / targetVidHeight);
        double offsetX = (cw - targetVidWidth * s) / 2.0;
        double offsetY = (ch - targetVidHeight * s) / 2.0;

        var rtb = new RenderTargetBitmap(targetVidWidth, targetVidHeight, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();

        using (var dc = dv.RenderOpen())
        {
            var group = new TransformGroup();
            group.Children.Add(new TranslateTransform(-offsetX, -offsetY));
            group.Children.Add(new ScaleTransform(1.0 / s, 1.0 / s));
            dc.PushTransform(group);

            foreach (var element in _elements)
            {
                element.Render(dc);
            }
            dc.Pop();
        }

        rtb.Render(dv);
        rtb.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = System.IO.File.Create(filePath);
        encoder.Save(stream);
    }

    public RenderTargetBitmap RenderFlattenedBitmap(BitmapSource backgroundImage, Rect selection)
    {
        double screenW = SystemParameters.VirtualScreenWidth;
        double screenH = SystemParameters.VirtualScreenHeight;
        double scaleX = backgroundImage.PixelWidth / Math.Max(1.0, screenW);
        double scaleY = backgroundImage.PixelHeight / Math.Max(1.0, screenH);

        int pixelW = Math.Max(1, (int)Math.Round(selection.Width * scaleX));
        int pixelH = Math.Max(1, (int)Math.Round(selection.Height * scaleY));

        var rtb = new RenderTargetBitmap(pixelW, pixelH, 96 * scaleX, 96 * scaleY, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();

        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-selection.X, -selection.Y));

            // Fondo original alineado exactamente con el espacio de coordenadas DIP
            dc.DrawImage(backgroundImage, new Rect(0, 0, screenW, screenH));

            // Traducciones Lens
            if (ShowTranslations)
            {
                foreach (var block in _lensBlocks)
                {
                    var ft = new FormattedText(
                        block.TranslatedText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Semibold"),
                        block.FontSize,
                        new SolidColorBrush(block.TextColor),
                        96.0 / 96.0
                    )
                    {
                        MaxTextWidth = Math.Max(16, block.Bounds.Width * 1.25)
                    };

                    double pillWidth = Math.Max(block.Bounds.Width, ft.Width + 8);
                    double pillHeight = Math.Max(block.Bounds.Height, ft.Height + 4);
                    var pillRect = new Rect(block.Bounds.X, block.Bounds.Y, pillWidth, pillHeight);

                    var bgBrush = new SolidColorBrush(block.BackgroundColor);
                    bgBrush.Freeze();
                    dc.DrawRoundedRectangle(bgBrush, null, pillRect, 4, 4);

                    dc.DrawText(ft, new Point(pillRect.X + 3, pillRect.Y + 2));
                }
            }

            // Elementos de dibujo y censura
            foreach (var element in _elements)
            {
                element.Render(dc);
            }

            dc.Pop();
        }

        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
