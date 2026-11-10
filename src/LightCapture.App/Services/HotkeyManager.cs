using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LightCapture.App.Services;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    public const uint VK_F9 = 0x78;       // Tecla F9
    public const uint VK_F7 = 0x76;       // Tecla F7
    public const uint VK_SNAPSHOT = 0x2C; // Impr Pant (opcional)

    private HwndSource? _source;
    private readonly Dictionary<int, Action> _hotkeyActions = new();
    private int _currentId = 1;

    public void Initialize(HwndSource source)
    {
        _source = source;
        _source.AddHook(HwndHook);
    }

    public bool Register(uint vk, uint modifiers, Action callback)
    {
        int id = _currentId++;
        _hotkeyActions[id] = callback;

        if (_source != null)
        {
            return RegisterHotKey(_source.Handle, id, modifiers, vk);
        }

        return false;
    }

    private int _captureHotkeyId = -1;

    public bool RegisterCaptureHotkey(uint vk, uint modifiers, Action callback)
    {
        if (_source != null && _captureHotkeyId != -1)
        {
            UnregisterHotKey(_source.Handle, _captureHotkeyId);
            _hotkeyActions.Remove(_captureHotkeyId);
            _captureHotkeyId = -1;
        }

        int id = _currentId++;
        _captureHotkeyId = id;
        _hotkeyActions[id] = callback;

        if (_source != null)
        {
            return RegisterHotKey(_source.Handle, id, modifiers, vk);
        }

        return false;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeyActions.TryGetValue(id, out var action))
            {
                action?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source != null)
        {
            foreach (var id in _hotkeyActions.Keys)
            {
                UnregisterHotKey(_source.Handle, id);
            }
            _source.RemoveHook(HwndHook);
        }
        _hotkeyActions.Clear();
    }
}
