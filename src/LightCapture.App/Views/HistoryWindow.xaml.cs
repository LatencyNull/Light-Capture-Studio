using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LightCapture.Core.Services;
using Clipboard = System.Windows.Clipboard;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace LightCapture.App.Views;

public record HistoryCardViewModel(
    string Id,
    string ImagePath,
    string TimestampText,
    string OcrTextSnippet,
    string FullOcrText
);

public partial class HistoryWindow : Window
{
    private readonly ICaptureHistoryService _historyService;

    public HistoryWindow(ICaptureHistoryService historyService)
    {
        InitializeComponent();
        _historyService = historyService;

        Loaded += (s, e) =>
        {
            ChkIncognito.IsChecked = !_historyService.IsHistoryEnabled;
            RefreshList();
        };
    }

    private void RefreshList(string query = "")
    {
        var entries = string.IsNullOrWhiteSpace(query)
            ? _historyService.GetHistory()
            : _historyService.Search(query);

        var viewModels = entries.Select(e =>
        {
            string path = _historyService.GetImagePath(e);
            string snippet = string.IsNullOrWhiteSpace(e.OcrText)
                ? "(Sin texto detectado)"
                : (e.OcrText.Length > 80 ? e.OcrText[..80] + "..." : e.OcrText);

            return new HistoryCardViewModel(
                Id: e.Id,
                ImagePath: path,
                TimestampText: e.Timestamp.ToString("dd/MM/yyyy HH:mm:ss"),
                OcrTextSnippet: snippet,
                FullOcrText: e.OcrText
            );
        }).ToList();

        HistoryItemsList.ItemsSource = viewModels;
        TxtTotalCount.Text = $"{viewModels.Count} {(viewModels.Count == 1 ? "captura" : "capturas")}";
        if (EmptyHistoryPlaceholder != null)
        {
            EmptyHistoryPlaceholder.Visibility = viewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        string text = TxtSearch.Text;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        RefreshList(text);
    }

    private void BtnCopyImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HistoryCardViewModel item && File.Exists(item.ImagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(item.ImagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                ClipboardHelper.CopyImageToClipboard(bitmap);
                MessageBox.Show("Imagen copiada al portapapeles.", "Historial", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }
    }

    private void BtnCopyText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HistoryCardViewModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.FullOcrText))
            {
                Clipboard.SetText(item.FullOcrText);
                MessageBox.Show("Texto copiado al portapapeles.", "Historial", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Esta captura no contiene texto detectado.", "Historial", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HistoryCardViewModel item)
        {
            _historyService.DeleteEntry(item.Id);
            RefreshList(TxtSearch.Text);
        }
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Directory.Exists(_historyService.HistoryDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _historyService.HistoryDirectory,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo abrir la carpeta: {ex.Message}", "Historial", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ChkIncognito_Checked(object sender, RoutedEventArgs e)
    {
        _historyService.IsHistoryEnabled = false;
    }

    private void ChkIncognito_Unchecked(object sender, RoutedEventArgs e)
    {
        _historyService.IsHistoryEnabled = true;
    }

    private void BtnClearAll_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "¿Estás seguro de que deseas vaciar por completo todo el historial de capturas?\nEsta acción no se puede deshacer.",
            "Vaciar Historial",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (result == MessageBoxResult.Yes)
        {
            _historyService.ClearAllHistory();
            RefreshList(TxtSearch.Text);
        }
    }
}
