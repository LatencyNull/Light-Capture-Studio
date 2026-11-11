using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LightCapture.App.Services;
using LightCapture.Core.Services;

namespace LightCapture.App.Views;

public partial class HotkeyConfigWindow : Window
{
    private uint _selectedVk;
    private uint _selectedModifiers;
    private string _selectedDisplayName;

    public HotkeyConfigWindow()
    {
        InitializeComponent();

        _selectedVk = HotkeySettingsManager.CurrentVirtualKey;
        _selectedModifiers = HotkeySettingsManager.CurrentModifiers;
        _selectedDisplayName = HotkeySettingsManager.CurrentDisplayName;

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        TxtCurrentKeyBadge.Text = _selectedDisplayName;
    }

    private void KeyCaptureBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        KeyCaptureBox.Focus();
    }

    private void KeyCaptureBox_GotFocus(object sender, RoutedEventArgs e)
    {
        KeyCaptureBox.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246));
        TxtKeyInstruction.Text = LanguageManager.Get("Hotkey_Listening");
        TxtKeyInstruction.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 165, 250));
    }

    private void KeyCaptureBox_LostFocus(object sender, RoutedEventArgs e)
    {
        KeyCaptureBox.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
        TxtKeyInstruction.Text = LanguageManager.Get("Hotkey_Instruction");
        TxtKeyInstruction.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(147, 197, 253));
    }

    private void KeyCaptureBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Ignore standalone modifier keys while being pressed
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        // If Escape pressed inside box, cancel listening
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            Close();
            return;
        }

        e.Handled = true;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        uint modifiers = 0;
        var parts = new List<string>();

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            modifiers |= 0x0002;
            parts.Add("Ctrl");
        }
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
        {
            modifiers |= 0x0001;
            parts.Add("Alt");
        }
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            modifiers |= 0x0004;
            parts.Add("Shift");
        }

        string keyName = key switch
        {
            Key.Snapshot => "Print Screen",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "Page Up",
            Key.PageDown => "Page Down",
            Key.Pause => "Pause",
            _ => key.ToString()
        };

        parts.Add(keyName);
        string displayName = string.Join(" + ", parts);

        _selectedVk = vk;
        _selectedModifiers = modifiers;
        _selectedDisplayName = displayName;

        UpdateDisplay();
    }

    private void PresetF9_Click(object sender, RoutedEventArgs e)
    {
        _selectedVk = 0x78;
        _selectedModifiers = 0;
        _selectedDisplayName = "F9";
        UpdateDisplay();
    }

    private void PresetPrintScreen_Click(object sender, RoutedEventArgs e)
    {
        _selectedVk = 0x2C;
        _selectedModifiers = 0;
        _selectedDisplayName = "Print Screen";
        UpdateDisplay();
    }

    private void PresetF10_Click(object sender, RoutedEventArgs e)
    {
        _selectedVk = 0x79;
        _selectedModifiers = 0;
        _selectedDisplayName = "F10";
        UpdateDisplay();
    }

    private void PresetF8_Click(object sender, RoutedEventArgs e)
    {
        _selectedVk = 0x77;
        _selectedModifiers = 0;
        _selectedDisplayName = "F8";
        UpdateDisplay();
    }

    private void PresetF7_Click(object sender, RoutedEventArgs e)
    {
        _selectedVk = 0x76;
        _selectedModifiers = 0;
        _selectedDisplayName = "F7";
        UpdateDisplay();
    }

    private void PresetCtrlShiftX_Click(object sender, RoutedEventArgs e)
    {
        _selectedVk = 0x58; // 'X'
        _selectedModifiers = 0x0002 | 0x0004; // Ctrl + Shift
        _selectedDisplayName = "Ctrl + Shift + X";
        UpdateDisplay();
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
    {
        PresetF9_Click(sender, e);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ApplyCaptureHotkey(_selectedVk, _selectedModifiers, _selectedDisplayName);
        }
        Close();
    }
}
