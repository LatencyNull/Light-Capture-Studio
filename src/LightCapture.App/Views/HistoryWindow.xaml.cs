using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using LightCapture.App.Services;
using LightCapture.Core.Services;
using Clipboard = System.Windows.Clipboard;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace LightCapture.App.Views;

public class HistoryCardViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public string Id { get; init; } = string.Empty;
    public string ImagePath { get; init; } = string.Empty;
    public BitmapSource? Thumbnail { get; init; }
    public string TimestampText { get; init; } = string.Empty;
    public string OcrTextSnippet { get; init; } = string.Empty;
    public string FullOcrText { get; init; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class HistoryWindow : Window
{
    private readonly ICaptureHistoryService _historyService;
    private List<HistoryCardViewModel> _currentItems = new();
    private bool _isUpdatingSelectAll = false;

    public HistoryWindow(ICaptureHistoryService historyService)
    {
        InitializeComponent();
        _historyService = historyService;

        Loaded += (s, e) =>
        {
            ChkIncognito.IsChecked = !_historyService.IsHistoryEnabled;
            RefreshList();
        };

        LanguageManager.LanguageChanged += _ => Dispatcher.Invoke(() => RefreshList(TxtSearch.Text));
    }

    private static BitmapSource? LoadThumbnailWithoutLock(string filePath, int decodeWidth = 320)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            byte[] bytes = File.ReadAllBytes(filePath);
            using var ms = new MemoryStream(bytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void RefreshList(string query = "")
    {
        HistoryItemsList.ItemsSource = null;

        var entries = string.IsNullOrWhiteSpace(query)
            ? _historyService.GetHistory()
            : _historyService.Search(query);

        _currentItems = entries.Select(e =>
        {
            string path = _historyService.GetImagePath(e);
            string snippet = string.IsNullOrWhiteSpace(e.OcrText)
                ? "(No text detected)"
                : (e.OcrText.Length > 80 ? e.OcrText[..80] + "..." : e.OcrText);

            return new HistoryCardViewModel
            {
                Id = e.Id,
                ImagePath = path,
                Thumbnail = LoadThumbnailWithoutLock(path, 320),
                TimestampText = e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                OcrTextSnippet = snippet,
                FullOcrText = e.OcrText,
                IsSelected = false
            };
        }).ToList();

        HistoryItemsList.ItemsSource = _currentItems;
        TxtTotalCount.Text = LanguageManager.Get("History_CapturesCount", _currentItems.Count);

        if (EmptyHistoryPlaceholder != null)
        {
            EmptyHistoryPlaceholder.Visibility = _currentItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateSelectionButton();
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
                byte[] bytes = File.ReadAllBytes(item.ImagePath);
                using var ms = new MemoryStream(bytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                ClipboardHelper.CopyImageToClipboard(bitmap);
                MessageBox.Show(LanguageManager.Get("Toast_ImageCopied"), LanguageManager.Get("History_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy image: {ex.Message}", LanguageManager.Get("History_Title"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnCopyText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is HistoryCardViewModel item)
        {
            if (!string.IsNullOrWhiteSpace(item.FullOcrText))
            {
                Clipboard.SetText(item.FullOcrText);
                MessageBox.Show(LanguageManager.Get("Toast_TextCopied"), LanguageManager.Get("History_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(LanguageManager.Get("Toast_NoText"), LanguageManager.Get("History_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
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

    private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _currentItems.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(
                LanguageManager.CurrentLanguage == AppLanguage.Spanish ? "Por favor selecciona una o más capturas para eliminar." : "Please select one or more captures to delete.",
                LanguageManager.Get("History_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            LanguageManager.CurrentLanguage == AppLanguage.Spanish
                ? $"¿Seguro que deseas eliminar {selected.Count} {(selected.Count == 1 ? "captura seleccionada" : "capturas seleccionadas")} del disco y del historial?"
                : $"Are you sure you want to delete {selected.Count} selected {(selected.Count == 1 ? "capture" : "captures")} from disk and history?",
            LanguageManager.Get("History_DeleteSelected"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );

        if (result == MessageBoxResult.Yes)
        {
            _historyService.DeleteEntries(selected.Select(s => s.Id));
            RefreshList(TxtSearch.Text);
        }
    }

    private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelectAll) return;
        foreach (var item in _currentItems)
        {
            item.IsSelected = true;
        }
        UpdateSelectionButton();
    }

    private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingSelectAll) return;
        foreach (var item in _currentItems)
        {
            item.IsSelected = false;
        }
        UpdateSelectionButton();
    }

    private void ItemCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateSelectionButton();
    }

    private void UpdateSelectionButton()
    {
        int count = _currentItems.Count(i => i.IsSelected);
        BtnDeleteSelected.Content = count > 0 ? $"{LanguageManager.Get("History_DeleteSelected")} ({count})" : LanguageManager.Get("History_DeleteSelected");

        _isUpdatingSelectAll = true;
        ChkSelectAll.IsChecked = _currentItems.Count > 0 && count == _currentItems.Count;
        _isUpdatingSelectAll = false;
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
            MessageBox.Show($"Could not open folder: {ex.Message}", "Capture History", MessageBoxButton.OK, MessageBoxImage.Error);
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
        if (_currentItems.Count == 0)
        {
            MessageBox.Show(
                LanguageManager.CurrentLanguage == AppLanguage.Spanish ? "El historial de capturas ya está vacío." : "Capture history is already empty.",
                LanguageManager.Get("History_Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            LanguageManager.CurrentLanguage == AppLanguage.Spanish
                ? $"¿Seguro que deseas vaciar completamente todo el historial ({_currentItems.Count} elementos)?\nEsto eliminará permanentemente las imágenes de tu carpeta de capturas."
                : $"Are you sure you want to completely clear the entire capture history ({_currentItems.Count} items)?\nThis will permanently delete the images from your captures folder.",
            LanguageManager.Get("History_ClearConfirmTitle"),
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
