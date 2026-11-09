using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using LightCapture.Core.Models;
using LightCapture.Core.Services;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using UserControl = System.Windows.Controls.UserControl;
using Shape = System.Windows.Shapes.Shape;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using RadioButton = System.Windows.Controls.RadioButton;
using ColorConverter = System.Windows.Media.ColorConverter;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

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
    public event Action? GeminiRequested;
    public event Action? GeminiPromptCopyRequested;
    public event Action? GeminiGoogleSearchRequested;
    public event Action? GeminiPerplexityRequested;
    public event Action? GeminiWebChatRequested;
    public event Action? TranslateRequested;
    public event Action? TranslateTextCopyRequested;
    public event Action? CopyOcrTextRequested;
    public event Action<string>? TargetLanguageChanged;
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

    private bool _lastIsTranslated = false;
    private bool _lastIsShowingTranslation = false;

    public LightshotToolbar()
    {
        InitializeComponent();
        Loaded += (s, e) => UpdateLanguageUi();
        TranslationLanguageManager.LanguageChanged += _ => Dispatcher.Invoke(UpdateLanguageUi);

        if (BtnShare.ContextMenu != null)
        {
            BtnShare.ContextMenu.Closed += (s, e) => _lastShareMenuCloseTime = DateTime.UtcNow;
        }

        IsVisibleChanged += (s, e) =>
        {
            if (!IsVisible)
            {
                ResetWaveEffect(HorizontalStackPanel);
                ResetWaveEffect(VerticalStackPanel);
            }
        };
    }

    public void SetTranslateActiveState(bool isTranslated, bool isShowingTranslation)
    {
        _lastIsTranslated = isTranslated;
        _lastIsShowingTranslation = isShowingTranslation;

        if (!isTranslated)
        {
            TranslateIndicator.Visibility = Visibility.Collapsed;
        }
        else if (isShowingTranslation)
        {
            TranslateIndicator.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            TranslateIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            TranslateIndicator.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            TranslateIndicator.Visibility = Visibility.Visible;
        }

        UpdateTranslateTooltip();
    }

    public void SetTargetLanguage(string lang)
    {
        TranslationLanguageManager.SetLanguage(lang);
        UpdateLanguageUi();
    }

    public void UpdateLanguageUi()
    {
        string lang = TranslationLanguageManager.CurrentLanguage;
        bool isEs = lang == TranslationLanguageManager.Spanish;

        if (TxtLangBadge != null)
        {
            TxtLangBadge.Text = isEs ? "ES" : "EN";
        }
        if (BadgeLanguage != null)
        {
            BadgeLanguage.Background = isEs
                ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) // Azul #2563EB para Español
                : new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Esmeralda #10B981 para Inglés
        }
        if (CheckLangEs != null)
        {
            CheckLangEs.Visibility = isEs ? Visibility.Visible : Visibility.Collapsed;
        }
        if (CheckLangEn != null)
        {
            CheckLangEn.Visibility = !isEs ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateTranslateTooltip();
    }

    private void UpdateTranslateTooltip()
    {
        string langName = TranslationLanguageManager.CurrentLanguageDisplayName;
        string otherName = TranslationLanguageManager.OtherLanguageDisplayName;

        if (_lastIsTranslated && _lastIsShowingTranslation)
        {
            BtnTranslate.ToolTip = $"Mostrando Traducción a {langName}. Haz clic para volver al texto Original.\nClic derecho: Cambiar idioma a {otherName} o copiar texto.";
        }
        else if (_lastIsTranslated && !_lastIsShowingTranslation)
        {
            BtnTranslate.ToolTip = $"Mostrando Original. Haz clic para ver el texto Traducido ({langName}).\nClic derecho: Cambiar idioma a {otherName} o copiar texto.";
        }
        else
        {
            BtnTranslate.ToolTip = $"Galaxy AI Traducir [🌐 Ctrl+T]\nDestino: {langName} (Clic para traducir / alternar original)\nClic derecho: Cambiar idioma a {otherName} o copiar texto.";
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

    private DrawingToolType _currentActiveTool = DrawingToolType.None;

    public void ResetToolSelection()
    {
        _currentActiveTool = DrawingToolType.None;
        BtnPen.IsChecked = false;
        BtnArrow.IsChecked = false;
        BtnRect.IsChecked = false;
        BtnMarker.IsChecked = false;
        BtnRedact.IsChecked = false;
        BtnText.IsChecked = false;
    }

    private void ToggleTool(RadioButton btn, DrawingToolType tool)
    {
        if (_currentActiveTool == tool)
        {
            // Deseleccionar herramienta activa al hacer clic nuevamente
            btn.IsChecked = false;
            _currentActiveTool = DrawingToolType.None;
            ToolSelected?.Invoke(DrawingToolType.None);
        }
        else
        {
            _currentActiveTool = tool;
            btn.IsChecked = true;
            ToolSelected?.Invoke(tool);
        }
    }

    private void BtnPen_Click(object sender, RoutedEventArgs e) => ToggleTool(BtnPen, DrawingToolType.Pen);
    private void BtnArrow_Click(object sender, RoutedEventArgs e) => ToggleTool(BtnArrow, DrawingToolType.Arrow);
    private void BtnRect_Click(object sender, RoutedEventArgs e) => ToggleTool(BtnRect, DrawingToolType.Rectangle);
    private void BtnMarker_Click(object sender, RoutedEventArgs e) => ToggleTool(BtnMarker, DrawingToolType.Marker);
    private void BtnRedact_Click(object sender, RoutedEventArgs e) => ToggleTool(BtnRedact, DrawingToolType.Redact);
    private void BtnRedact_RightClick(object sender, MouseButtonEventArgs e)
    {
        _currentActiveTool = DrawingToolType.Pixelate;
        BtnRedact.IsChecked = true;
        ToolSelected?.Invoke(DrawingToolType.Pixelate);
    }
    private void BtnText_Click(object sender, RoutedEventArgs e) => ToggleTool(BtnText, DrawingToolType.Text);

    private void BtnColor_Click(object sender, RoutedEventArgs e)
    {
        _colorIndex = (_colorIndex + 1) % Palette.Length;
        var selectedColor = Palette[_colorIndex];
        ColorBox.Background = new SolidColorBrush(selectedColor);
        ColorSelected?.Invoke(selectedColor);
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e) => UndoRequested?.Invoke();

    private void BtnTranslate_Click(object sender, RoutedEventArgs e) => TranslateRequested?.Invoke();

    private void BtnTranslate_RightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (BtnTranslate.ContextMenu != null)
        {
            UpdateLanguageUi();
            BtnTranslate.ContextMenu.PlacementTarget = BtnTranslate;
            BtnTranslate.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            BtnTranslate.ContextMenu.IsOpen = true;
        }
        else
        {
            TranslateTextCopyRequested?.Invoke();
        }
    }

    private void MenuLangEs_Click(object sender, RoutedEventArgs e)
    {
        SetTargetLanguage(TranslationLanguageManager.Spanish);
        TargetLanguageChanged?.Invoke(TranslationLanguageManager.Spanish);
    }

    private void MenuLangEn_Click(object sender, RoutedEventArgs e)
    {
        SetTargetLanguage(TranslationLanguageManager.English);
        TargetLanguageChanged?.Invoke(TranslationLanguageManager.English);
    }

    private void MenuCopyTranslated_Click(object sender, RoutedEventArgs e) => TranslateTextCopyRequested?.Invoke();
    private void MenuCopyOcr_Click(object sender, RoutedEventArgs e) => CopyOcrTextRequested?.Invoke();
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

    private DateTime _lastShareMenuCloseTime = DateTime.MinValue;

    private void BtnShare_Click(object sender, RoutedEventArgs e)
    {
        if (BtnShare.ContextMenu == null) return;

        if (BtnShare.ContextMenu.IsOpen)
        {
            BtnShare.ContextMenu.IsOpen = false;
            return;
        }

        if ((DateTime.UtcNow - _lastShareMenuCloseTime).TotalMilliseconds < 350)
        {
            return;
        }

        BtnShare.ContextMenu.PlacementTarget = BtnShare;
        BtnShare.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        BtnShare.ContextMenu.IsOpen = true;
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

    // =========================================================================
    // EFECTO OLA MAC OS DOCK / WINSTEP NEXUS (Magnificación Continua y Suave)
    // =========================================================================
    private void HorizontalBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (HorizontalStackPanel == null) return;
        Point pos = e.GetPosition(HorizontalStackPanel);
        ApplyWaveEffect(HorizontalStackPanel, pos, isHorizontal: true);
    }

    private void HorizontalBar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (HorizontalStackPanel == null) return;
        ResetWaveEffect(HorizontalStackPanel);
    }

    private void VerticalBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (VerticalStackPanel == null) return;
        Point pos = e.GetPosition(VerticalStackPanel);
        ApplyWaveEffect(VerticalStackPanel, pos, isHorizontal: false);
    }

    private void VerticalBar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (VerticalStackPanel == null) return;
        ResetWaveEffect(VerticalStackPanel);
    }

    private void ApplyWaveEffect(Panel panel, Point mousePos, bool isHorizontal)
    {
        const double radius = 80.0;     // Radio de influencia de la ola en píxeles (~2.5 botones contiguos)
        const double maxScale = 1.38;   // Magnificación máxima en la cresta de la ola (+25%+ zoom)

        foreach (UIElement child in panel.Children)
        {
            if (child is ButtonBase btn && btn.Content is FrameworkElement icon)
            {
                double w = btn.ActualWidth > 0 ? btn.ActualWidth : 32.0;
                double h = btn.ActualHeight > 0 ? btn.ActualHeight : 32.0;
                Point btnCenter = btn.TranslatePoint(new Point(w / 2.0, h / 2.0), panel);
                double distance = isHorizontal
                    ? Math.Abs(mousePos.X - btnCenter.X)
                    : Math.Abs(mousePos.Y - btnCenter.Y);

                double scale = 1.0;
                if (distance < radius)
                {
                    // Curva de campana cosenoidal: fluida, continua y natural como en macOS Dock / Nexus
                    double waveFactor = Math.Cos((distance / radius) * (Math.PI / 2.0));
                    scale = 1.0 + (maxScale - 1.0) * waveFactor;
                }

                if (icon.RenderTransform is not ScaleTransform st || st.IsFrozen)
                {
                    st = new ScaleTransform(1.0, 1.0);
                    icon.RenderTransformOrigin = new Point(0.5, 0.5);
                    icon.RenderTransform = st;
                }

                // Desactivar animaciones activas para latencia 0ms durante el movimiento continuo del mouse
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = scale;
                st.ScaleY = scale;

                // Elevar dinámicamente en Z el icono ampliado para superposición limpia
                Panel.SetZIndex(btn, (int)(scale * 100));
            }
        }
    }

    private void ResetWaveEffect(Panel? panel)
    {
        if (panel == null) return;

        foreach (UIElement child in panel.Children)
        {
            if (child is ButtonBase btn && btn.Content is FrameworkElement icon)
            {
                if (icon.RenderTransform is ScaleTransform st && !st.IsFrozen)
                {
                    if (Math.Abs(st.ScaleX - 1.0) > 0.005 || Math.Abs(st.ScaleY - 1.0) > 0.005)
                    {
                        var anim = new DoubleAnimation
                        {
                            To = 1.0,
                            Duration = TimeSpan.FromMilliseconds(160),
                            DecelerationRatio = 0.8
                        };
                        st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                        st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                    }
                }
                Panel.SetZIndex(btn, 0);
            }
        }
    }
}
