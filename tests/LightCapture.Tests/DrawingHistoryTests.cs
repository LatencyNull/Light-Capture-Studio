using System.Windows;
using System.Windows.Media;
using LightCapture.Core.Models;

namespace LightCapture.Tests;

public class DrawingHistoryTests
{
    [Fact]
    public void DrawingStack_PushAndUndo_MaintainsCorrectOrder()
    {
        // Arrange
        var stack = new Stack<DrawingElement>();

        var pen = new PenDrawingElement(Colors.Red, 3, new[] { new Point(0, 0), new Point(10, 10) });
        var line = new LineDrawingElement(Colors.Blue, 2, new Point(5, 5), new Point(20, 20));

        // Act
        stack.Push(pen);
        stack.Push(line);

        // Assert
        Assert.Equal(2, stack.Count);

        var undone = stack.Pop();
        Assert.Equal(DrawingToolType.Line, undone.ToolType);
        Assert.Single(stack);

        var nextUndone = stack.Pop();
        Assert.Equal(DrawingToolType.Pen, nextUndone.ToolType);
        Assert.Empty(stack);
    }

    [Fact]
    public void VideoAnnotationElements_InstantiateWithCorrectProperties()
    {
        var arrow = new ArrowDrawingElement(Colors.Yellow, 3, new Point(10, 10), new Point(100, 100));
        var rect = new RectangleDrawingElement(Colors.Green, 2, new Rect(20, 20, 150, 80));
        var text = new TextDrawingElement(Colors.White, 16, new Point(30, 40), "Test Note");
        var redact = new RedactDrawingElement(new Rect(50, 50, 120, 40), Colors.Black);

        Assert.Equal(DrawingToolType.Arrow, arrow.ToolType);
        Assert.Equal(Colors.Yellow, arrow.Color);

        Assert.Equal(DrawingToolType.Rectangle, rect.ToolType);
        Assert.Equal(150, rect.Bounds.Width);

        Assert.Equal(DrawingToolType.Text, text.ToolType);
        Assert.Equal("Test Note", text.Text);

        Assert.Equal(DrawingToolType.Redact, redact.ToolType);
        Assert.Equal(Colors.Black, redact.FillColor);
    }
}
