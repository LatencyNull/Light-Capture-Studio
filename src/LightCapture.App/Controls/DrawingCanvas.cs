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

public class DrawingCanvas : FrameworkElement
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

    public event Action? DrawingChanged;

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

        if (CurrentTool == DrawingToolType.Pen)
        {
            _previewElement = new PenDrawingElement(CurrentColor, StrokeThickness, new List<Point> { point });
        }
        else if (CurrentTool == DrawingToolType.Marker)
        {
            _previewElement = new MarkerDrawingElement(CurrentColor, StrokeThickness, new List<Point> { point });
        }
        else if (CurrentTool == DrawingToolType.Redact)
        {
            _previewElement = new RedactDrawingElement(new Rect(point.X, point.Y, 0, 0), Colors.Black);
        }

        InvalidateVisual();
    }

    public void UpdateDrawing(Point currentPoint)
    {
        if (!_isDrawing || CurrentTool == DrawingToolType.None) return;

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
            _previewElement = new LineDrawingElement(CurrentColor, StrokeThickness, _startPoint, currentPoint);
        }
        else if (CurrentTool == DrawingToolType.Arrow)
        {
            _previewElement = new ArrowDrawingElement(CurrentColor, StrokeThickness, _startPoint, currentPoint);
        }
        else if (CurrentTool == DrawingToolType.Rectangle)
        {
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);
            _previewElement = new RectangleDrawingElement(CurrentColor, StrokeThickness, new Rect(x, y, w, h));
        }
        else if (CurrentTool == DrawingToolType.Pixelate)
        {
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);
            _previewElement = new RectangleDrawingElement(Color.FromArgb(120, 100, 100, 100), 1.5, new Rect(x, y, w, h));
        }
        else if (CurrentTool == DrawingToolType.Redact)
        {
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double w = Math.Abs(currentPoint.X - _startPoint.X);
            double h = Math.Abs(currentPoint.Y - _startPoint.Y);
            _previewElement = new RedactDrawingElement(new Rect(x, y, w, h), Colors.Black);
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

    public void AddTextElement(Point position, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _elements.Add(new TextDrawingElement(CurrentColor, 16, position, text));
        _undoStack.Clear();
        InvalidateVisual();
        DrawingChanged?.Invoke();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        // 1. Renderizar bloques de Google Lens In-Place Translation si están activados
        if (ShowTranslations && _lensBlocks.Count > 0)
        {
            foreach (var block in _lensBlocks)
            {
                var bgBrush = new SolidColorBrush(block.BackgroundColor);
                bgBrush.Freeze();
                dc.DrawRoundedRectangle(bgBrush, null, block.Bounds, 3, 3);

                var formattedText = new FormattedText(
                    block.TranslatedText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"),
                    block.FontSize,
                    new SolidColorBrush(block.TextColor),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip
                )
                {
                    MaxTextWidth = Math.Max(10, block.Bounds.Width),
                    MaxTextHeight = Math.Max(10, block.Bounds.Height)
                };

                dc.DrawText(formattedText, new Point(block.Bounds.X + 2, block.Bounds.Y + 1));
            }
        }

        // 2. Renderizar trazos y elementos de dibujo (incluyendo pixelados y pasos numerados)
        foreach (var element in _elements)
        {
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
                    var bgBrush = new SolidColorBrush(block.BackgroundColor);
                    bgBrush.Freeze();
                    dc.DrawRoundedRectangle(bgBrush, null, block.Bounds, 3, 3);

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
                        MaxTextWidth = Math.Max(10, block.Bounds.Width),
                        MaxTextHeight = Math.Max(10, block.Bounds.Height)
                    };
                    dc.DrawText(ft, new Point(block.Bounds.X + 2, block.Bounds.Y + 1));
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
