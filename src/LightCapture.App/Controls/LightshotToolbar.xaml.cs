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
            BtnTranslate.ToolTip = $"Showing Translation to {langName}. Click to return to Original text.\nRight-click: Change language to {otherName} or copy text.";
        }
        else if (_lastIsTranslated && !_lastIsShowingTranslation)
        {
            BtnTranslate.ToolTip = $"Showing Original. Click to view Translated text ({langName}).\nRight-click: Change language to {otherName} or copy text.";
        }
        else
        {
            BtnTranslate.ToolTip = $"Galaxy AI Translate [🌐 Ctrl+T]\nTarget: {langName} (Click to translate / toggle original)\nRight-click: Change language to {otherName} or copy text.";
        }
    }

    public void UpdatePosition(Rect sel, double screenWidth, double screenHeight)
    {
        // 1. Medir dimensiones dinámicas reales de ambas barras (con respaldo robusto)
        if (VerticalBar.ActualWidth <= 0 || VerticalBar.ActualHeight <= 0)
        {
            VerticalBar.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        if (HorizontalBar.ActualWidth <= 0 || HorizontalBar.ActualHeight <= 0)
        {
            HorizontalBar.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        }

        double vertWidth = VerticalBar.ActualWidth > 0 ? VerticalBar.ActualWidth : (VerticalBar.DesiredSize.Width > 0 ? VerticalBar.DesiredSize.Width : 42);
        double vertHeight = VerticalBar.ActualHeight > 0 ? VerticalBar.ActualHeight : (VerticalBar.DesiredSize.Height > 0 ? VerticalBar.DesiredSize.Height : 275);

        double horizWidth = HorizontalBar.ActualWidth > 0 ? HorizontalBar.ActualWidth : (HorizontalBar.DesiredSize.Width > 0 ? HorizontalBar.DesiredSize.Width : 352);
        double horizHeight = HorizontalBar.ActualHeight > 0 ? HorizontalBar.ActualHeight : (HorizontalBar.DesiredSize.Height > 0 ? HorizontalBar.DesiredSize.Height : 42);

        // 2. Parámetros de márgenes y separaciones
        const double ScreenMargin = 12;   // Margen seguro respecto a los bordes de la pantalla (evita cortes, taskbar y sombras)
        const double SelectionGap = 6;    // Separación exterior respecto al borde de selección
        const double ToolbarsGap = 8;     // Separación entre ambas barras cuando se colocan juntas

        // 3. Evaluar si cabe en el exterior de la selección (Comportamiento clásico Lightshot)
        bool canFitRightOutside = (sel.Right + SelectionGap + vertWidth + ScreenMargin <= screenWidth);
        bool canFitBottomOutside = (sel.Bottom + SelectionGap + horizHeight + ScreenMargin <= screenHeight);

        double vertX;
        double vertY;
        double horizX;
        double horizY;

        if (canFitRightOutside && canFitBottomOutside)
        {
            // CASO 1: Selección con espacio exterior (Derecha e Inferior)
            vertX = sel.Right + SelectionGap;
            double preferredVertY = Math.Max(sel.Top, sel.Bottom - vertHeight);
            vertY = Math.Max(ScreenMargin, Math.Min(preferredVertY, screenHeight - vertHeight - ScreenMargin));

            horizX = Math.Max(ScreenMargin, Math.Min(sel.Right - horizWidth, screenWidth - horizWidth - ScreenMargin));
            horizY = sel.Bottom + SelectionGap;
        }
        else if (!canFitRightOutside && canFitBottomOutside)
        {
            // CASO 2: Sin espacio a la derecha exterior -> Barra vertical se invierte adentro
            vertX = Math.Max(ScreenMargin, sel.Right - vertWidth - ScreenMargin);
            double preferredVertY = Math.Max(sel.Top, sel.Bottom - vertHeight);
            vertY = Math.Max(ScreenMargin, Math.Min(preferredVertY, screenHeight - vertHeight - ScreenMargin));

            horizX = Math.Max(ScreenMargin, Math.Min(sel.Right - horizWidth, screenWidth - horizWidth - ScreenMargin));
            horizY = sel.Bottom + SelectionGap;
        }
        else if (canFitRightOutside && !canFitBottomOutside)
        {
            // CASO 3: Sin espacio abajo exterior -> Barra horizontal se invierte adentro
            vertX = sel.Right + SelectionGap;
            vertY = Math.Max(ScreenMargin, Math.Min(sel.Bottom - vertHeight, screenHeight - vertHeight - ScreenMargin));

            horizX = Math.Max(ScreenMargin, Math.Min(sel.Right - horizWidth, screenWidth - horizWidth - ScreenMargin));
            horizY = Math.Max(ScreenMargin, sel.Bottom - horizHeight - ScreenMargin);
        }
        else
        {
            // CASO 4: PANTALLA COMPLETA O ESQUINA INFERIOR DERECHA (Ambas se invierten adentro sin cortarse)
            // Barra horizontal abajo adentro
            horizY = Math.Max(ScreenMargin, Math.Min(sel.Bottom - horizHeight - ScreenMargin, screenHeight - horizHeight - ScreenMargin));

            // Barra vertical a la derecha adentro
            vertX = Math.Max(ScreenMargin, Math.Min(sel.Right - vertWidth - ScreenMargin, screenWidth - vertWidth - ScreenMargin));

            // Colocar la barra vertical directamente SOBRE la barra horizontal (esquina inferior derecha idéntica a Lightshot)
            double vertYAboveHorizontal = horizY - vertHeight - ToolbarsGap;
            if (vertYAboveHorizontal >= ScreenMargin && vertYAboveHorizontal >= sel.Top)
            {
                vertY = vertYAboveHorizontal;
                horizX = Math.Max(ScreenMargin, Math.Min(sel.Right - horizWidth - ScreenMargin, screenWidth - horizWidth - ScreenMargin));
            }
            else
            {
                // Si la selección tiene muy poca altura para apilarlas verticalmente, se colocan lado a lado
                vertY = Math.Max(ScreenMargin, Math.Min(sel.Bottom - vertHeight - ScreenMargin, screenHeight - vertHeight - ScreenMargin));
                horizX = Math.Max(ScreenMargin, vertX - horizWidth - ToolbarsGap);
            }
        }

        // 4. Clamping estricto de seguridad para garantizar visibilidad al 100% dentro de la pantalla
        vertX = Math.Max(ScreenMargin, Math.Min(vertX, screenWidth - vertWidth - ScreenMargin));
        vertY = Math.Max(ScreenMargin, Math.Min(vertY, screenHeight - vertHeight - ScreenMargin));
        horizX = Math.Max(ScreenMargin, Math.Min(horizX, screenWidth - horizWidth - ScreenMargin));
        horizY = Math.Max(ScreenMargin, Math.Min(horizY, screenHeight - horizHeight - ScreenMargin));

        Canvas.SetLeft(VerticalBar, vertX);
        Canvas.SetTop(VerticalBar, vertY);

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
        // Radio calibrado para enfocar la cresta únicamente en el icono bajo el cursor (~1.4 botones)
        const double radius = 46.0;
        const double maxScale = 1.35;

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
                    // Curva de campana cosenoidal de caída rápida (exponente 2.4):
                    // El icono activo recibe el 100% de la cresta (1.35), mientras que el adyacente solo un sutil 1.05
                    double normalized = distance / radius;
                    double cosVal = Math.Cos(normalized * (Math.PI / 2.0));
                    double waveFactor = Math.Pow(cosVal, 2.4);
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
