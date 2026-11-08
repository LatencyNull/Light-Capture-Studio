using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace LightCapture.App.Views;

public partial class PinnedCaptureWindow : Window
{
    private readonly BitmapSource _bitmap;
    private double _currentScale = 1.0;

    public PinnedCaptureWindow(BitmapSource bitmap)
    {
        InitializeComponent();
        _bitmap = bitmap;
        PinnedImage.Source = _bitmap;

        // Limitar tamaño máximo inicial
        double maxWidth = SystemParameters.PrimaryScreenWidth * 0.7;
        double maxHeight = SystemParameters.PrimaryScreenHeight * 0.7;

        if (_bitmap.PixelWidth > maxWidth || _bitmap.PixelHeight > maxHeight)
        {
            double scale = Math.Min(maxWidth / _bitmap.PixelWidth, maxHeight / _bitmap.PixelHeight);
            _currentScale = scale;
            ImageScale.ScaleX = _currentScale;
            ImageScale.ScaleY = _currentScale;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
        {
            _currentScale = Math.Min(3.0, _currentScale + 0.1);
        }
        else
        {
            _currentScale = Math.Max(0.2, _currentScale - 0.1);
        }

        ImageScale.ScaleX = _currentScale;
        ImageScale.ScaleY = _currentScale;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void MenuCopy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetImage(_bitmap);
    }

    private void MenuOpacity100_Click(object sender, RoutedEventArgs e) => Opacity = 1.0;
    private void MenuOpacity80_Click(object sender, RoutedEventArgs e) => Opacity = 0.8;
    private void MenuOpacity60_Click(object sender, RoutedEventArgs e) => Opacity = 0.6;
    private void MenuOpacity40_Click(object sender, RoutedEventArgs e) => Opacity = 0.4;

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    private void MenuClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
