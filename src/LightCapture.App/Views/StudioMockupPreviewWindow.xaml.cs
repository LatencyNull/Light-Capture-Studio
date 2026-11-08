using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LightCapture.App.Views;

public partial class StudioMockupPreviewWindow : Window
{
    private readonly BitmapSource _mockupBitmap;

    public StudioMockupPreviewWindow(BitmapSource mockupBitmap)
    {
        InitializeComponent();
        _mockupBitmap = mockupBitmap;
        MockupImage.Source = _mockupBitmap;
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetImage(_mockupBitmap);
        MessageBox.Show("Imagen embellecida copiada nuevamente al portapapeles.", "Modo Studio", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Imagen PNG (*.png)|*.png",
            FileName = $"StudioMockup_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (sfd.ShowDialog() == true)
        {
            using var fileStream = File.Create(sfd.FileName);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_mockupBitmap));
            encoder.Save(fileStream);

            MessageBox.Show("Imagen guardada exitosamente.", "Modo Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
