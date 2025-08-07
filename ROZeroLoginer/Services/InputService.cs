using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ROZeroLoginer.Models;

namespace ROZeroLoginer.Services
{
    public class InputService
    {
        private readonly GameResolutionService _resolutionService;
        
        public InputService()
        {
            _resolutionService = new GameResolutionService();
        }
        
        private const int INPUT_KEYBOARD = 1;
        private const int INPUT_MOUSE = 0;
        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int KEYEVENTF_UNICODE = 0x0004;

        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out System.Drawing.Point lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, IntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUTUNION union;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public KEYBDINPUT keyboard;
            [FieldOffset(0)]
            public MOUSEINPUT mouse;
            [FieldOffset(0)]
            public HARDWAREINPUT hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public void SendLogin(string username, string password, string otp, int otpDelayMs = 2000, AppSettings settings = null)
        {
            var targetWindow = GetForegroundWindow();

            // 確保目標視窗在前台
            SetForegroundWindow(targetWindow);
            Thread.Sleep(100);
            
            // 載入遊戲解析度設定
            if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
            {
                _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
            }
            
            // 根據解析度計算同意按鈕位置
            var (agreeX, agreeY) = _resolutionService.GetAgreeButtonPosition();

            // 按下同意
            LeftClick(agreeX, agreeY);

            // 輸入帳號
            SendText(username);
            Thread.Sleep(100);

            // 按下 TAB 鍵
            SendKey(Keys.Tab);
            Thread.Sleep(100);

            // 輸入密碼
            SendText(password);
            Thread.Sleep(100);

            // 按下 ENTER 鍵
            SendKey(Keys.Enter);

            // 等待 OTP 視窗出現的延遲時間
            Thread.Sleep(otpDelayMs);

            // 輸入 OTP
            SendText(otp);
            Thread.Sleep(100);

            // 按下 ENTER 鍵
            SendKey(Keys.Enter);
        }

        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var inputs = new INPUT[text.Length * 2];
            var inputIndex = 0;

            foreach (char c in text)
            {
                // Key down
                inputs[inputIndex] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    union = new INPUTUNION
                    {
                        keyboard = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                inputIndex++;

                // Key up
                inputs[inputIndex] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    union = new INPUTUNION
                    {
                        keyboard = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                inputIndex++;
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void SendKey(Keys key)
        {
            var inputs = new INPUT[2];

            // Key down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                union = new INPUTUNION
                {
                    keyboard = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYDOWN,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            // Key up
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                union = new INPUTUNION
                {
                    keyboard = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public void SendKeyCombo(Keys[] keys)
        {
            // Press keys down
            foreach (var key in keys)
            {
                var input = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    union = new INPUTUNION
                    {
                        keyboard = new KEYBDINPUT
                        {
                            wVk = (ushort)key,
                            wScan = 0,
                            dwFlags = KEYEVENTF_KEYDOWN,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
            }

            // Release keys in reverse order
            for (int i = keys.Length - 1; i >= 0; i--)
            {
                var input = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    union = new INPUTUNION
                    {
                        keyboard = new KEYBDINPUT
                        {
                            wVk = (ushort)keys[i],
                            wScan = 0,
                            dwFlags = KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
                SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
            }
        }

        public IntPtr GetCurrentForegroundWindow()
        {
            return GetForegroundWindow();
        }

        public void LeftClick(int x, int y)
        {
            // 確保目標視窗在前台
            var targetWindow = GetForegroundWindow();
            SetForegroundWindow(targetWindow);
            Thread.Sleep(200);

            // 使用 ClientToScreen 將客戶區座標轉換為螢幕座標
            var clientPoint = new System.Drawing.Point(x, y);
            if (ClientToScreen(targetWindow, ref clientPoint))
            {
                // clientPoint 現在包含了正確的螢幕座標（已扣除標題列和邊框）
                SetCursorPos(clientPoint.X, clientPoint.Y);
            }
            else
            {
                // 如果無法轉換座標，使用原本的絕對座標
                SetCursorPos(x, y);
            }

            Thread.Sleep(100);

            // 使用 mouse_event API 點擊 (類似按鍵精靈)
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
        }

        public void LeftClickAtCurrentPosition()
        {
            // 使用 mouse_event API 在當前位置點擊
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
        }
    }
}