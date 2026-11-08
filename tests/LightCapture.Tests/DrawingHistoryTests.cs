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
}
