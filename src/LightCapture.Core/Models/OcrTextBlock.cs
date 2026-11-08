namespace LightCapture.Core.Models;

public record OcrWordBlock(
    string Text,
    double X,
    double Y,
    double Width,
    double Height
);

public record OcrTextBlock(
    string Text,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<OcrWordBlock> Words
)
{
    public static OcrTextBlock FromWords(IReadOnlyList<OcrWordBlock> words)
    {
        if (words == null || words.Count == 0)
        {
            return new OcrTextBlock(string.Empty, 0, 0, 0, 0, Array.Empty<OcrWordBlock>());
        }

        string fullText = string.Join(" ", words.Select(w => w.Text));
        double minX = words.Min(w => w.X);
        double minY = words.Min(w => w.Y);
        double maxRight = words.Max(w => w.X + w.Width);
        double maxBottom = words.Max(w => w.Y + w.Height);

        return new OcrTextBlock(
            Text: fullText,
            X: minX,
            Y: minY,
            Width: Math.Max(0, maxRight - minX),
            Height: Math.Max(0, maxBottom - minY),
            Words: words
        );
    }
}
