using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Pen = System.Windows.Media.Pen;
using FlowDirection = System.Windows.FlowDirection;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;

namespace LightCapture.Core.Models;

public enum DrawingToolType
{
    None,
    Pen,
    Line,
    Arrow,
    Rectangle,
    Marker,
    Text,
    Pixelate,
    Redact,
    NumberedStep
}

public abstract record DrawingElement(DrawingToolType ToolType, Color Color, double StrokeThickness)
{
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string DisplayName => ToolType switch
    {
        DrawingToolType.Arrow => "Arrow",
        DrawingToolType.Text => "Text",
        DrawingToolType.Pen => "Pen",
        DrawingToolType.Rectangle => "Rectangle",
        DrawingToolType.Redact => "Redact",
        DrawingToolType.Marker => "Marker",
        DrawingToolType.NumberedStep => "Step",
        _ => "Object"
    };

    public string IconEmoji => ToolType switch
    {
        DrawingToolType.Arrow => "↗️",
        DrawingToolType.Text => "🔤",
        DrawingToolType.Pen => "✏️",
        DrawingToolType.Rectangle => "🔲",
        DrawingToolType.Redact => "🔒",
        DrawingToolType.Marker => "🖍️",
        DrawingToolType.NumberedStep => "🔢",
        _ => "📍"
    };

    public abstract void Render(DrawingContext dc);
}

public record PenDrawingElement(Color Color, double StrokeThickness, IReadOnlyList<Point> Points)
    : DrawingElement(DrawingToolType.Pen, Color, StrokeThickness)
{
    public override void Render(DrawingContext dc)
    {
        if (Points.Count == 0) return;
        if (Points.Count == 1)
        {
            var dotBrush = new SolidColorBrush(Color);
            dotBrush.Freeze();
            dc.DrawEllipse(dotBrush, null, Points[0], Math.Max(1.5, StrokeThickness / 2.0), Math.Max(1.5, StrokeThickness / 2.0));
            return;
        }

        var pen = new Pen(new SolidColorBrush(Color), StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Points[0], false, false);
            for (int i = 1; i < Points.Count; i++)
            {
                ctx.LineTo(Points[i], true, true);
            }
        }
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);
    }
}

public record LineDrawingElement(Color Color, double StrokeThickness, Point Start, Point End)
    : DrawingElement(DrawingToolType.Line, Color, StrokeThickness)
{
    public override void Render(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(Color), StrokeThickness);
        pen.Freeze();
        dc.DrawLine(pen, Start, End);
    }
}

public record ArrowDrawingElement(Color Color, double StrokeThickness, Point Start, Point End)
    : DrawingElement(DrawingToolType.Arrow, Color, StrokeThickness)
{
    public override void Render(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(Color), StrokeThickness);
        pen.Freeze();
        dc.DrawLine(pen, Start, End);

        // Geometría de punta de flecha
        Vector dir = End - Start;
        if (dir.Length < 1) return;
        dir.Normalize();

        Vector perp = new Vector(-dir.Y, dir.X);
        double headLength = Math.Max(12, StrokeThickness * 3.5);
        double headWidth = headLength * 0.55;

        Point p1 = End - (dir * headLength) + (perp * headWidth);
        Point p2 = End - (dir * headLength) - (perp * headWidth);

        var arrowHead = new StreamGeometry();
        using (var ctx = arrowHead.Open())
        {
            ctx.BeginFigure(End, true, true);
            ctx.LineTo(p1, true, true);
            ctx.LineTo(p2, true, true);
        }
        arrowHead.Freeze();

        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        dc.DrawGeometry(brush, null, arrowHead);
    }
}

public record RectangleDrawingElement(Color Color, double StrokeThickness, Rect Bounds)
    : DrawingElement(DrawingToolType.Rectangle, Color, StrokeThickness)
{
    public override void Render(DrawingContext dc)
    {
        var pen = new Pen(new SolidColorBrush(Color), StrokeThickness);
        pen.Freeze();
        dc.DrawRectangle(null, pen, Bounds);
    }
}

public record MarkerDrawingElement(Color Color, double StrokeThickness, IReadOnlyList<Point> Points)
    : DrawingElement(DrawingToolType.Marker, Color, StrokeThickness)
{
    public override void Render(DrawingContext dc)
    {
        if (Points.Count < 2) return;

        // Marcador fluorescente translúcido
        var markerColor = Color.FromArgb(90, Color.R, Color.G, Color.B);
        var brush = new SolidColorBrush(markerColor);
        brush.Freeze();
        var pen = new Pen(brush, StrokeThickness * 4)
        {
            StartLineCap = PenLineCap.Square,
            EndLineCap = PenLineCap.Square
        };
        pen.Freeze();

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(Points[0], false, false);
            for (int i = 1; i < Points.Count; i++)
            {
                ctx.LineTo(Points[i], true, true);
            }
        }
        geometry.Freeze();

        dc.DrawGeometry(null, pen, geometry);
    }
}

public record TextDrawingElement(Color Color, double FontSize, Point Position, string Text)
    : DrawingElement(DrawingToolType.Text, Color, 1)
{
    public double FontSize { get; set; } = FontSize;
    public Point Position { get; set; } = Position;
    public string Text { get; set; } = Text;

    public FormattedText GetFormattedText(double pixelsPerDip = 1.0)
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        return new FormattedText(
            string.IsNullOrEmpty(Text) ? " " : Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"),
            Math.Max(8, FontSize),
            brush,
            pixelsPerDip
        );
    }

    public Rect GetBounds(double pixelsPerDip = 1.0)
    {
        if (string.IsNullOrEmpty(Text)) return new Rect(Position.X, Position.Y, 0, 0);
        var ft = GetFormattedText(pixelsPerDip);
        return new Rect(Position.X, Position.Y, ft.WidthIncludingTrailingWhitespace, ft.Height);
    }

    public override void Render(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        // Sombra sutil para máxima legibilidad estilo Photoshop contra cualquier fondo
        var shadowBrush = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));
        shadowBrush.Freeze();
        var shadowText = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"),
            Math.Max(8, FontSize),
            shadowBrush,
            1.0
        );
        dc.DrawText(shadowText, new Point(Position.X + 1.2, Position.Y + 1.2));

        var formattedText = GetFormattedText(1.0);
        dc.DrawText(formattedText, Position);
    }
}

public record PixelateDrawingElement(Rect Bounds, BitmapSource? PixelatedBitmap)
    : DrawingElement(DrawingToolType.Pixelate, Colors.Transparent, 0)
{
    public override void Render(DrawingContext dc)
    {
        if (PixelatedBitmap != null && Bounds.Width > 0 && Bounds.Height > 0)
        {
            dc.DrawImage(PixelatedBitmap, Bounds);
        }
    }
}

public record RedactDrawingElement(Rect Bounds, Color FillColor)
    : DrawingElement(DrawingToolType.Redact, FillColor, 0)
{
    public override void Render(DrawingContext dc)
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            var brush = new SolidColorBrush(FillColor);
            brush.Freeze();
            dc.DrawRectangle(brush, null, Bounds);
        }
    }
}

public record NumberedStepDrawingElement(Color Color, Point Position, int StepNumber, double Radius = 12)
    : DrawingElement(DrawingToolType.NumberedStep, Color, 0)
{
    public override void Render(DrawingContext dc)
    {
        var brush = new SolidColorBrush(Color);
        brush.Freeze();
        var borderPen = new Pen(Brushes.White, 1.5);
        borderPen.Freeze();

        dc.DrawEllipse(brush, borderPen, Position, Radius, Radius);

        var ft = new FormattedText(
            StepNumber.ToString(),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            Radius * 1.05,
            Brushes.White,
            VisualTreeHelper.GetDpi(new ContainerVisual()).PixelsPerDip
        );

        Point textPos = new Point(Position.X - (ft.Width / 2), Position.Y - (ft.Height / 2));
        dc.DrawText(ft, textPos);
    }
}
