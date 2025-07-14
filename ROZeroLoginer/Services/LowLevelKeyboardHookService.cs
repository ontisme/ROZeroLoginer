using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ROZeroLoginer.Services
{
    public class LowLevelKeyboardHookService
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int HC_ACTION = 0;

        private readonly LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;
        private static Keys _targetKey = Keys.Home;
        private static Action _hotkeyAction;
        private static WindowValidationService _windowValidationService;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        public LowLevelKeyboardHookService()
        {
            _windowValidationService = new WindowValidationService();
        }

        public bool RegisterHotkey(Keys key, Action action)
        {
            try
            {
                _targetKey = key;
                _hotkeyAction = action;
                
                _hookID = SetHook(_proc);
                return _hookID != IntPtr.Zero;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register hotkey: {ex.Message}");
                return false;
            }
        }

        public void UnregisterAllHotkeys()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            _hotkeyAction = null;
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= HC_ACTION && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                // 檢查是否為目標按鍵
                if (key == _targetKey)
                {
                    // 檢查當前視窗是否為 RO 遊戲視窗
                    if (_windowValidationService?.IsRagnarokWindow() == true)
                    {
                        // 是RO視窗，攔截按鍵並執行動作
                        try
                        {
                            _hotkeyAction?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Hotkey action error: {ex.Message}");
                        }
                        
                        // 返回非零值攔截按鍵
                        return (IntPtr)1;
                    }
                    // 不是RO視窗，讓按鍵正常傳遞
                }
            }

            // 調用下一個鉤子或讓按鍵正常處理
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
    }
}