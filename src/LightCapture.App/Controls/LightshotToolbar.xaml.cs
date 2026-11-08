using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LightCapture.Core.Models;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;
using Shape = System.Windows.Shapes.Shape;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using RadioButton = System.Windows.Controls.RadioButton;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace LightCapture.App.Controls;

public partial class LightshotToolbar : UserControl
{
    private static readonly Color[] Palette = new[]
    {
        Color.FromRgb(239, 68, 68),   // Red
        Color.FromRgb(59, 130, 246),  // Blue
        Color.FromRgb(16, 185, 129),  // Green
        Color.FromRgb(245, 158, 11),  // Orange
        Color.FromRgb(139, 92, 246),  // Purple
        Colors.White
    };
    private int _colorIndex = 0;

    public event Action<DrawingToolType>? ToolSelected;
    public event Action<Color>? ColorSelected;
    public event Action? UndoRequested;
#pragma warning disable CS0067
    public event Action? UploadRequested;
    public event Action? ShareRequested;
    public event Action? PrintRequested;
    public event Action? SpeakRequested;
    public event Action? PinRequested;
#pragma warning restore CS0067
    public event Action? SearchRequested;
    public event Action<bool, bool>? RecordVideoRequested;
    public event Action? RecordGifRequested;
    public event Action? GeminiRequested;
    public event Action? GeminiPromptCopyRequested;
    public event Action? GeminiGoogleSearchRequested;
    public event Action? GeminiPerplexityRequested;
    public event Action? GeminiWebChatRequested;
    public event Action? TranslateRequested;
    public event Action? TranslateTextCopyRequested;
    public event Action? CopyOcrTextRequested;
    public event Action? ShareWhatsAppRequested;
    public event Action? ShareWhatsAppAppRequested;
    public event Action? ShareWhatsAppWebRequested;
    public event Action? ShareDiscordRequested;
    public event Action? ShareFacebookRequested;
    public event Action? ShareTikTokRequested;
    public event Action? ShareInstagramRequested;
    public event Action? ShareTelegramRequested;
    public event Action? ShareTwitterRequested;
    public event Action? ShareEmailRequested;
    public event Action? ShareCloudRequested;
    public event Action? CopyRequested;
    public event Action? SaveRequested;
    public event Action? CloseRequested;

    public LightshotToolbar()
    {
        InitializeComponent();
    }

    public void SetTranslateActiveState(bool isTranslated, bool isShowingTranslation)
    {
        if (!isTranslated)
        {
            BtnTranslate.ToolTip = "Galaxy AI Traducir [🌐 Ctrl+T] (Alterna entre Original y Traducción)";
            TranslateIndicator.Visibility = Visibility.Collapsed;
        }
        else if (isShowingTranslation)
        {
            BtnTranslate.ToolTip = "Mostrando Traducción. Haz clic para volver al texto Original.";
            TranslateIndicator.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            TranslateIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            BtnTranslate.ToolTip = "Mostrando Original. Haz clic para ver el texto Traducido.";
            TranslateIndicator.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            TranslateIndicator.Visibility = Visibility.Visible;
        }
    }

    public void UpdatePosition(Rect sel, double screenWidth, double screenHeight)
    {
        // 1. Barra vertical a la derecha
        double vertX = sel.Right + 6;
        double vertY = sel.Top;

        if (vertX + 44 > screenWidth)
        {
            vertX = Math.Max(6, sel.Right - 44);
        }

        Canvas.SetLeft(VerticalBar, vertX);
        Canvas.SetTop(VerticalBar, Math.Max(6, Math.Min(vertY, screenHeight - 315)));

        // 2. Barra horizontal abajo a la derecha (11 herramientas limpias con IA, Video, Compartir e Imprimir)
        const double horizWidth = 390;
        double horizX = sel.Right - horizWidth;
        double horizY = sel.Bottom + 6;

        if (horizX < 6) horizX = 6;
        if (horizY + 48 > screenHeight)
        {
            horizY = Math.Max(6, sel.Bottom - 48);
        }

        Canvas.SetLeft(HorizontalBar, horizX);
        Canvas.SetTop(HorizontalBar, horizY);
    }

    private void BtnPen_Click(object sender, RoutedEventArgs e) => ToolSelected?.Invoke(DrawingToolType.Pen);
    private void BtnArrow_Click(object sender, RoutedEventArgs e) => ToolSelected?.Invoke(DrawingToolType.Arrow);
    private void BtnRect_Click(object sender, RoutedEventArgs e) => ToolSelected?.Invoke(DrawingToolType.Rectangle);
    private void BtnMarker_Click(object sender, RoutedEventArgs e) => ToolSelected?.Invoke(DrawingToolType.Marker);
    private void BtnRedact_Click(object sender, RoutedEventArgs e) => ToolSelected?.Invoke(DrawingToolType.Redact);
    private void BtnRedact_RightClick(object sender, MouseButtonEventArgs e)
    {
        BtnRedact.IsChecked = true;
        ToolSelected?.Invoke(DrawingToolType.Pixelate);
    }
    private void BtnText_Click(object sender, RoutedEventArgs e) => ToolSelected?.Invoke(DrawingToolType.Text);

    private void BtnColor_Click(object sender, RoutedEventArgs e)
    {
        _colorIndex = (_colorIndex + 1) % Palette.Length;
        var selectedColor = Palette[_colorIndex];
        ColorBox.Background = new SolidColorBrush(selectedColor);
        ColorSelected?.Invoke(selectedColor);
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => UndoRequested?.Invoke();

    private void BtnTranslate_Click(object sender, RoutedEventArgs e) => TranslateRequested?.Invoke();
    private void BtnTranslate_RightClick(object sender, MouseButtonEventArgs e) => TranslateTextCopyRequested?.Invoke();
    private void BtnSearch_Click(object sender, RoutedEventArgs e) => SearchRequested?.Invoke();
    private void BtnGemini_Click(object sender, RoutedEventArgs e) => GeminiRequested?.Invoke();
    private void BtnGemini_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (BtnGemini.ContextMenu != null)
        {
            BtnGemini.ContextMenu.PlacementTarget = BtnGemini;
            BtnGemini.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            BtnGemini.ContextMenu.IsOpen = true;
        }
        else
        {
            GeminiPromptCopyRequested?.Invoke();
        }
    }
    private void GeminiGoogleSearch_Click(object sender, RoutedEventArgs e) => GeminiGoogleSearchRequested?.Invoke();
    private void GeminiPerplexity_Click(object sender, RoutedEventArgs e) => GeminiPerplexityRequested?.Invoke();
    private void GeminiWebChat_Click(object sender, RoutedEventArgs e) => GeminiWebChatRequested?.Invoke();
    private void GeminiPrompt_Click(object sender, RoutedEventArgs e) => GeminiPromptCopyRequested?.Invoke();

    private void BtnRecordVideo_Click(object sender, RoutedEventArgs e) => RecordVideoRequested?.Invoke(true, true);
    private void BtnRecordVideo_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (BtnRecordVideo.ContextMenu != null)
        {
            BtnRecordVideo.ContextMenu.PlacementTarget = BtnRecordVideo;
            BtnRecordVideo.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            BtnRecordVideo.ContextMenu.IsOpen = true;
        }
    }
    private void RecordFullAudio_Click(object sender, RoutedEventArgs e) => RecordVideoRequested?.Invoke(true, true);
    private void RecordMicOnly_Click(object sender, RoutedEventArgs e) => RecordVideoRequested?.Invoke(false, true);
    private void RecordAudioOnly_Click(object sender, RoutedEventArgs e) => RecordVideoRequested?.Invoke(true, false);
    private void RecordMuted_Click(object sender, RoutedEventArgs e) => RecordVideoRequested?.Invoke(false, false);
    private void RecordGif_Click(object sender, RoutedEventArgs e) => RecordGifRequested?.Invoke();
    private void BtnRecordGif_Click(object sender, RoutedEventArgs e) => RecordGifRequested?.Invoke();

    private void BtnShare_Click(object sender, RoutedEventArgs e)
    {
        if (BtnShare.ContextMenu != null)
        {
            BtnShare.ContextMenu.PlacementTarget = BtnShare;
            BtnShare.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            BtnShare.ContextMenu.IsOpen = true;
        }
    }

    private void ShareWhatsAppApp_Click(object sender, RoutedEventArgs e)
    {
        ShareWhatsAppAppRequested?.Invoke();
        ShareWhatsAppRequested?.Invoke();
    }
    private void ShareWhatsAppWeb_Click(object sender, RoutedEventArgs e) => ShareWhatsAppWebRequested?.Invoke();
    private void ShareWhatsApp_Click(object sender, RoutedEventArgs e) => ShareWhatsAppAppRequested?.Invoke();
    private void ShareDiscord_Click(object sender, RoutedEventArgs e) => ShareDiscordRequested?.Invoke();
    private void ShareFacebook_Click(object sender, RoutedEventArgs e) => ShareFacebookRequested?.Invoke();
    private void ShareTikTok_Click(object sender, RoutedEventArgs e) => ShareTikTokRequested?.Invoke();
    private void ShareInstagram_Click(object sender, RoutedEventArgs e) => ShareInstagramRequested?.Invoke();
    private void ShareTelegram_Click(object sender, RoutedEventArgs e) => ShareTelegramRequested?.Invoke();
    private void ShareTwitter_Click(object sender, RoutedEventArgs e) => ShareTwitterRequested?.Invoke();
    private void ShareEmail_Click(object sender, RoutedEventArgs e) => ShareEmailRequested?.Invoke();
    private void ShareCloud_Click(object sender, RoutedEventArgs e) => ShareCloudRequested?.Invoke();

    private void BtnPrint_Click(object sender, RoutedEventArgs e) => PrintRequested?.Invoke();
    private void BtnCopyOcrText_Click(object sender, RoutedEventArgs e) => CopyOcrTextRequested?.Invoke();
    private void BtnCopy_Click(object sender, RoutedEventArgs e) => CopyRequested?.Invoke();
    private void BtnSave_Click(object sender, RoutedEventArgs e) => SaveRequested?.Invoke();
    private void BtnClose_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();
}
