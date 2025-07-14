using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;

namespace ROZeroLoginer.Services
{
    public class GlobalHotkeyService
    {
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly Dictionary<int, Action> _hotkeys = new Dictionary<int, Action>();
        private readonly HotkeyWindow _hotkeyWindow;
        private int _currentId = 1;

        public GlobalHotkeyService()
        {
            _hotkeyWindow = new HotkeyWindow();
            _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;
        }

        public bool RegisterHotkey(Keys key, Action action)
        {
            var id = _currentId++;
            var keyCode = (uint)key;
            
            if (RegisterHotKey(_hotkeyWindow.Handle, id, 0, keyCode))
            {
                _hotkeys[id] = action;
                return true;
            }
            
            return false;
        }

        public bool RegisterHotkey(Keys key, Keys modifiers, Action action)
        {
            var id = _currentId++;
            var keyCode = (uint)key;
            var modifierFlags = GetModifierFlags(modifiers);
            
            if (RegisterHotKey(_hotkeyWindow.Handle, id, modifierFlags, keyCode))
            {
                _hotkeys[id] = action;
                return true;
            }
            
            return false;
        }

        public void UnregisterAllHotkeys()
        {
            foreach (var hotkeyId in _hotkeys.Keys)
            {
                UnregisterHotKey(_hotkeyWindow.Handle, hotkeyId);
            }
            _hotkeys.Clear();
        }

        private uint GetModifierFlags(Keys modifiers)
        {
            uint flags = 0;
            
            if ((modifiers & Keys.Alt) == Keys.Alt)
                flags |= MOD_ALT;
            if ((modifiers & Keys.Control) == Keys.Control)
                flags |= MOD_CONTROL;
            if ((modifiers & Keys.Shift) == Keys.Shift)
                flags |= MOD_SHIFT;
            if ((modifiers & Keys.LWin) == Keys.LWin || (modifiers & Keys.RWin) == Keys.RWin)
                flags |= MOD_WIN;
                
            return flags;
        }

        private void OnHotkeyPressed(int id)
        {
            if (_hotkeys.TryGetValue(id, out var action))
            {
                action?.Invoke();
            }
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
            _hotkeyWindow?.Dispose();
        }

        private class HotkeyWindow : NativeWindow, IDisposable
        {
            public event Action<int> HotkeyPressed;

            public HotkeyWindow()
            {
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    var id = m.WParam.ToInt32();
                    HotkeyPressed?.Invoke(id);
                }
                else
                {
                    base.WndProc(ref m);
                }
            }

            public void Dispose()
            {
                DestroyHandle();
            }
        }
    }
}