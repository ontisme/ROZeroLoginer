using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ROZeroLoginer.Models;
using ROZeroLoginer.Utils;

namespace ROZeroLoginer.Services
{
    public enum ClickStrategy
    {
        MouseEvent = 0,
        SendInput = 1,
        PostMessage = 2,
        SetCursorPos = 3
    }
}

namespace ROZeroLoginer.Services
{
    public class InputService
    {
        private readonly GameResolutionService _resolutionService;

        // 記錄已經登入的視窗句柄，避免重複使用
        private static readonly HashSet<IntPtr> _loggedInWindows = new HashSet<IntPtr>();
        private static readonly object _loggedInWindowsLock = new object();

        public InputService()
        {
            _resolutionService = new GameResolutionService();
        }

        /// <summary>
        /// 清空已登入視窗記錄 - 用於新的批次啟動會話
        /// </summary>
        public static void ClearLoggedInWindows()
        {
            lock (_loggedInWindowsLock)
            {
                _loggedInWindows.Clear();
                LogService.Instance.Info("[InputService] 已清空登入視窗記錄");
            }
        }

        /// <summary>
        /// 標記視窗為已登入
        /// </summary>
        private static void MarkWindowAsLoggedIn(IntPtr windowHandle)
        {
            lock (_loggedInWindowsLock)
            {
                if (_loggedInWindows.Add(windowHandle))
                {
                    LogService.Instance.Info("[InputService] 標記視窗為已登入: {0}", windowHandle);
                }
            }
        }

        /// <summary>
        /// 檢查視窗是否已登入
        /// </summary>
        private static bool IsWindowLoggedIn(IntPtr windowHandle)
        {
            lock (_loggedInWindowsLock)
            {
                return _loggedInWindows.Contains(windowHandle);
            }
        }

        /// <summary>
        /// 等待指定 PID 的 RO 視窗出現，帶有超時機制
        /// </summary>
        /// <param name="targetPid">目標進程 PID</param>
        /// <param name="timeoutMs">超時時間（毫秒），預設 30 秒</param>
        /// <param name="checkIntervalMs">檢查間隔（毫秒），預設 500 毫秒</param>
        /// <returns>找到的視窗句柄，如果超時則返回 IntPtr.Zero</returns>
        public static IntPtr WaitForRoWindowByPid(int targetPid, int timeoutMs = 30000, int checkIntervalMs = 500)
        {
            LogService.Instance.Info("[WaitForRoWindow] 開始等待 PID {0} 的 RO 視窗出現，超時時間: {1}ms", targetPid, timeoutMs);

            var startTime = Environment.TickCount;
            var checkCount = 0;

            while (Environment.TickCount - startTime < timeoutMs)
            {
                checkCount++;
                LogService.Instance.Debug("[WaitForRoWindow] 第 {0} 次檢查 PID {1} 的視窗", checkCount, targetPid);

                try
                {
                    var availableWindows = new List<IntPtr>();

                    EnumWindows((hWnd, lParam) =>
                    {
                        try
                        {
                            GetWindowThreadProcessId(hWnd, out uint processId);

                            if (processId == targetPid)
                            {
                                if (!IsWindowVisible(hWnd))
                                {
                                    LogService.Instance.Debug("[WaitForRoWindow] 跳過不可見的 PID {0} 視窗: {1}", targetPid, hWnd.ToInt64());
                                    return true;
                                }

                                var titleBuilder = new StringBuilder(256);
                                int titleLength = GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                                var title = titleBuilder.ToString();

                                LogService.Instance.Debug("[WaitForRoWindow] PID {0} 視窗 {1} 標題: '{2}'", targetPid, hWnd.ToInt64(), title);

                                if (title == "Ragnarok : Zero")
                                {
                                    bool isLoggedIn = IsWindowLoggedIn(hWnd);
                                    LogService.Instance.Debug("[WaitForRoWindow] 找到符合的視窗 {0}，已登入狀態: {1}", hWnd.ToInt64(), isLoggedIn);

                                    if (!isLoggedIn)
                                    {
                                        availableWindows.Add(hWnd);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Debug("[WaitForRoWindow] 檢查視窗 {0} 時發生錯誤: {1}", hWnd.ToInt64(), ex.Message);
                        }

                        return true;
                    }, IntPtr.Zero);

                    if (availableWindows.Count > 0)
                    {
                        var foundWindow = availableWindows.First();
                        LogService.Instance.Info("[WaitForRoWindow] 成功找到 PID {0} 的 RO 視窗: {1}，等待時間: {2}ms",
                            targetPid, foundWindow.ToInt64(), Environment.TickCount - startTime);
                        return foundWindow;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warning("[WaitForRoWindow] 第 {0} 次檢查時發生錯誤: {1}", checkCount, ex.Message);
                }

                Thread.Sleep(checkIntervalMs);
            }

            LogService.Instance.Warning("[WaitForRoWindow] 等待 PID {0} 的 RO 視窗超時，總檢查次數: {1}", targetPid, checkCount);
            return IntPtr.Zero;
        }

        private const int INPUT_KEYBOARD = 1;
        private const int INPUT_MOUSE = 0;
        private const int KEYEVENTF_KEYDOWN = 0x0000;
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const int KEYEVENTF_UNICODE = 0x0004;

        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;

        // Windows Messages for PostMessage
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_MOUSEMOVE = 0x0200;

        // MK constants for PostMessage
        private const int MK_LBUTTON = 0x0001;

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

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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

        public void SendLogin(string username, string password, string otpSecret, int otpDelayMs = 2000, AppSettings settings = null, bool skipAgreeButton = false, int targetProcessId = 0)
        {
            LogService.Instance.Info("[SendLogin] 開始登入流程 - 用戶: {0}, 跳過同意按鈕: {1}, 目標PID: {2}", username, skipAgreeButton, targetProcessId);

            // 根據 PID 精確尋找視窗，如果沒有提供 PID 則使用通用方法
            IntPtr targetWindow = IntPtr.Zero;
            bool isBatchLaunch = targetProcessId > 0; // 批次啟動會提供PID，單個登入不會

            try
            {
                if (targetProcessId > 0)
                {
                    LogService.Instance.Info("[SendLogin] 使用PID {0} 查找RO視窗 (批次啟動模式)", targetProcessId);
                    targetWindow = FindRagnarokWindowByPid(targetProcessId);
                    LogService.Instance.Debug("[SendLogin] 根據PID {0} 查找視窗結果: {1}", targetProcessId, targetWindow);

                    // 如果PID方法失敗，嘗試通用方法作為備份
                    if (targetWindow == IntPtr.Zero)
                    {
                        LogService.Instance.Warning("[SendLogin] PID方法失敗，嘗試通用方法作為備份");
                        targetWindow = FindRagnarokWindow(true); // 批次模式，需要避開已登入視窗
                        LogService.Instance.Debug("[SendLogin] 備份通用方法查找結果: {0}", targetWindow);
                    }
                }
                else
                {
                    LogService.Instance.Info("[SendLogin] 使用通用方法查找RO視窗 (單個登入模式)");
                    targetWindow = FindRagnarokWindow(false); // 單個登入模式，不需要避開已登入視窗
                    LogService.Instance.Debug("[SendLogin] 使用通用方法查找視窗結果: {0}", targetWindow);
                }
            }
            catch (Exception findEx)
            {
                LogService.Instance.Error("[SendLogin] 查找視窗時發生異常: {0}", findEx.ToString());

                // 嘗試最後的備用方法
                if (targetProcessId > 0)
                {
                    LogService.Instance.Info("[SendLogin] 嘗試最後的備用方法");
                    try
                    {
                        targetWindow = FindRagnarokWindow(true); // 批次模式備份
                        LogService.Instance.Debug("[SendLogin] 備用方法結果: {0}", targetWindow);
                    }
                    catch (Exception backupEx)
                    {
                        LogService.Instance.Error("[SendLogin] 備用方法也失敗: {0}", backupEx.Message);
                    }
                }

                if (targetWindow == IntPtr.Zero)
                {
                    throw; // 重新拋出原異常
                }
            }

            if (targetWindow == IntPtr.Zero)
            {
                string errorMsg = targetProcessId > 0
                    ? $"未找到 PID {targetProcessId} 對應的 Ragnarok Online 遊戲視窗！請確認遊戲已經啟動且進程正確。"
                    : "未找到 Ragnarok Online 遊戲視窗！請確認遊戲已經啟動。";

                LogService.Instance.Error("[SendLogin] {0}", errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            LogService.Instance.Info("[SendLogin] 找到 RO 視窗: {0}", targetWindow);

            // 確保目標視窗在前台，並給予足夠時間切換
            bool setForegroundResult = SetForegroundWindow(targetWindow);
            LogService.Instance.Debug("[SendLogin] SetForegroundWindow 結果: {0}", setForegroundResult);
            Thread.Sleep(500); // 增加等待時間確保視窗完全切換

            // 載入遊戲解析度設定
            if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
            {
                _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
            }

            // 只有在不跳過同意按鈕時才點擊
            if (!skipAgreeButton)
            {
                LogService.Instance.Info("[SendLogin] 開始同意按鈕點擊流程");

                // 根據解析度計算同意按鈕位置
                var (agreeX, agreeY) = _resolutionService.GetAgreeButtonPosition();
                LogService.Instance.Debug("[SendLogin] 同意按鈕位置: ({0}, {1})", agreeX, agreeY);

                // 檢查視窗焦點並點擊同意按鈕
                CheckRagnarokWindowFocus(targetProcessId);
                LogService.Instance.Debug("[SendLogin] 視窗焦點檢查通過，開始點擊同意按鈕");
                ClickUsingMethod1(targetWindow, agreeX, agreeY);
                Thread.Sleep(200);
                LogService.Instance.Debug("[SendLogin] 同意按鈕點擊完成");
            }
            else
            {
                LogService.Instance.Debug("[SendLogin] 跳過同意按鈕點擊");
            }

            // 檢查視窗焦點並輸入帳號
            CheckRagnarokWindowFocus(targetProcessId);
            SendText(username);
            Thread.Sleep(100);

            // 檢查視窗焦點並按下 TAB 鍵
            CheckRagnarokWindowFocus(targetProcessId);
            SendKey(Keys.Tab);
            Thread.Sleep(100);

            // 檢查視窗焦點並輸入密碼
            CheckRagnarokWindowFocus(targetProcessId);
            SendText(password);
            Thread.Sleep(100);

            // 檢查視窗焦點並按下 ENTER 鍵
            CheckRagnarokWindowFocus(targetProcessId);
            SendKey(Keys.Enter);

            // 等待 OTP 視窗出現的延遲時間
            Thread.Sleep(otpDelayMs);

            // 檢查並等待 OTP 時間充足再輸入
            CheckRagnarokWindowFocus(targetProcessId);
            string finalOtp = WaitForValidOtpTime(otpSecret, targetProcessId);
            SendText(finalOtp);
            Thread.Sleep(100);

            // 檢查視窗焦點並按下 ENTER 鍵
            CheckRagnarokWindowFocus(targetProcessId);
            SendKey(Keys.Enter);

            // 標記視窗為已登入，避免重複使用
            MarkWindowAsLoggedIn(targetWindow);
            LogService.Instance.Info("[SendLogin] 登入流程完成，已標記視窗 {0} 為已登入狀態", targetWindow);
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
            // 尋找 RO 視窗並檢查焦點
            var targetWindow = FindRagnarokWindow();
            if (targetWindow == IntPtr.Zero)
            {
                throw new InvalidOperationException("未找到 Ragnarok Online 遊戲視窗！");
            }

            SetForegroundWindow(targetWindow);
            Thread.Sleep(300);

            CheckRagnarokWindowFocus();

            // 使用最可靠的方法1 (SetCursorPos + mouse_event)
            ClickUsingMethod1(targetWindow, x, y);
        }

        public void LeftClickAtCurrentPosition()
        {
            // 使用 mouse_event API 在當前位置點擊
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
        }

        /// <summary>
        /// 查找可用的 Ragnarok Online 視窗 - 只匹配標題為 "Ragnarok : Zero" 且未登入的視窗
        /// </summary>
        private IntPtr FindRagnarokWindow(bool useBatchTracking = false)
        {
            string mode = useBatchTracking ? "批次啟動" : "單個登入";
            LogService.Instance.Info("[FindRagnarokWindow] 開始查找可用的 RO 視窗 ({0} 模式)", mode);

            var availableWindows = new List<(IntPtr handle, bool isLoggedIn)>();
            int totalRoWindows = 0;

            EnumWindows((hWnd, lParam) =>
            {
                try
                {
                    // 檢查視窗是否可見
                    if (!IsWindowVisible(hWnd))
                        return true;

                    // 檢查視窗標題
                    int length = GetWindowTextLength(hWnd);
                    if (length <= 0)
                        return true;

                    var windowTitle = new System.Text.StringBuilder(length + 1);
                    int actualLength = GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                    if (actualLength <= 0)
                        return true;

                    string title = windowTitle.ToString();

                    // 嚴格匹配標題為 "Ragnarok : Zero"
                    if (title == "Ragnarok : Zero")
                    {
                        totalRoWindows++;
                        
                        // 只在批次啟動模式下檢查登入狀態
                        bool isLoggedIn = useBatchTracking ? IsWindowLoggedIn(hWnd) : false;
                        availableWindows.Add((hWnd, isLoggedIn));
                        
                        LogService.Instance.Debug("[FindRagnarokWindow] 發現 RO 視窗: {0}, 已登入檢查: {1}, 結果: {2}", 
                            hWnd, useBatchTracking ? "是" : "否", isLoggedIn);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warning("[FindRagnarokWindow] 枚舉視窗 {0} 時出現異常: {1}", hWnd, ex.Message);
                }

                return true; // 繼續枚舉
            }, IntPtr.Zero);

            if (useBatchTracking)
            {
                LogService.Instance.Info("[FindRagnarokWindow] 找到 {0} 個 RO 視窗，其中 {1} 個可用",
                    totalRoWindows, availableWindows.Count(w => !w.isLoggedIn));

                // 批次啟動模式：優先返回未登入的視窗
                var availableWindow = availableWindows.FirstOrDefault(w => !w.isLoggedIn);
                if (availableWindow.handle != IntPtr.Zero)
                {
                    LogService.Instance.Info("[FindRagnarokWindow] 選擇未登入的視窗: {0}", availableWindow.handle);
                    return availableWindow.handle;
                }

                // 如果沒有未登入的視窗，記錄警告但不返回已登入的
                if (availableWindows.Any())
                {
                    LogService.Instance.Warning("[FindRagnarokWindow] 所有 RO 視窗都已登入，無可用視窗");
                }
                else
                {
                    LogService.Instance.Warning("[FindRagnarokWindow] 未找到任何 RO 視窗");
                }

                return IntPtr.Zero;
            }
            else
            {
                LogService.Instance.Info("[FindRagnarokWindow] 找到 {0} 個 RO 視窗，單個登入模式優先選取前台視窗", totalRoWindows);

                // 單個登入模式：優先選取當前前台視窗
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != IntPtr.Zero)
                {
                    // 檢查前台視窗是否在可用的 RO 視窗列表中
                    var foregroundRoWindow = availableWindows.FirstOrDefault(w => w.handle == foregroundWindow);
                    if (foregroundRoWindow.handle != IntPtr.Zero)
                    {
                        LogService.Instance.Info("[FindRagnarokWindow] 選擇當前前台 RO 視窗: {0}", foregroundWindow);
                        return foregroundWindow;
                    }
                    else
                    {
                        LogService.Instance.Debug("[FindRagnarokWindow] 前台視窗不是 RO 視窗，改選第一個可用的 RO 視窗");
                    }
                }

                // 如果前台視窗不是 RO 視窗，則返回第一個找到的 RO 視窗
                var firstWindow = availableWindows.FirstOrDefault();
                if (firstWindow.handle != IntPtr.Zero)
                {
                    LogService.Instance.Info("[FindRagnarokWindow] 選擇第一個 RO 視窗: {0}", firstWindow.handle);
                    return firstWindow.handle;
                }

                LogService.Instance.Warning("[FindRagnarokWindow] 未找到任何 RO 視窗");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// 根據 PID 查找 Ragnarok Online 視窗 - 只匹配標題為 "Ragnarok : Zero" 的視窗
        /// </summary>
        private IntPtr FindRagnarokWindowByPid(int targetPid)
        {
            LogService.Instance.Info("[FindRagnarokWindowByPid] 開始根據 PID {0} 查找 RO 視窗", targetPid);

            IntPtr foundWindow = IntPtr.Zero;
            int totalWindows = 0;

            try
            {
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        totalWindows++;

                        // 獲取視窗的進程 ID
                        uint processId = 0;
                        uint threadId = GetWindowThreadProcessId(hWnd, out processId);

                        if (threadId == 0 || processId == 0)
                            return true; // 繼續枚舉

                        // 只處理指定 PID 的視窗
                        if ((int)processId != targetPid)
                            return true; // 繼續枚舉

                        // 檢查視窗是否可見
                        if (!IsWindowVisible(hWnd))
                        {
                            LogService.Instance.Debug("[FindRagnarokWindowByPid] 跳過不可見的 PID {0} 視窗: {1}", targetPid, hWnd);
                            return true; // 繼續枚舉
                        }

                        // 獲取視窗標題
                        string title = "";
                        try
                        {
                            int length = GetWindowTextLength(hWnd);
                            if (length > 0 && length < 1000)
                            {
                                var windowTitle = new System.Text.StringBuilder(length + 1);
                                int actualLength = GetWindowText(hWnd, windowTitle, windowTitle.Capacity);
                                if (actualLength > 0)
                                {
                                    title = windowTitle.ToString();
                                }
                            }
                        }
                        catch (Exception titleEx)
                        {
                            LogService.Instance.Warning("[FindRagnarokWindowByPid] 獲取視窗標題失敗: {0}", titleEx.Message);
                        }

                        LogService.Instance.Debug("[FindRagnarokWindowByPid] PID {0} 視窗: {1}, 標題: '{2}'", targetPid, hWnd, title);

                        // 嚴格匹配標題為 "Ragnarok : Zero"
                        if (title == "Ragnarok : Zero")
                        {
                            foundWindow = hWnd;
                            LogService.Instance.Info("[FindRagnarokWindowByPid] 找到 PID {0} 的 RO 視窗: {1}", targetPid, hWnd);
                            return false; // 停止枚舉
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Warning("[FindRagnarokWindowByPid] 枚舉視窗 {0} 時出現異常: {1}", hWnd, ex.Message);
                    }

                    return true; // 繼續枚舉
                }, IntPtr.Zero);
            }
            catch (Exception enumEx)
            {
                LogService.Instance.Error("[FindRagnarokWindowByPid] EnumWindows 調用失敗: {0}", enumEx.ToString());
            }

            LogService.Instance.Info("[FindRagnarokWindowByPid] PID {0} 查找完成，檢查了 {1} 個視窗，結果: {2}",
                targetPid, totalWindows, foundWindow);

            return foundWindow;
        }

        private string GetWindowInfo(IntPtr hWnd)
        {
            try
            {
                // 獲取視窗標題
                int length = GetWindowTextLength(hWnd);
                string windowTitle = "Unknown";
                if (length > 0)
                {
                    var titleBuilder = new System.Text.StringBuilder(length + 1);
                    GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                    windowTitle = titleBuilder.ToString();
                }

                // 獲取進程信息
                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);
                string processName = "Unknown";
                if (processId != 0)
                {
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById((int)processId);
                        processName = process.ProcessName;
                    }
                    catch { }
                }

                // 獲取視窗位置和大小
                RECT windowRect;
                string positionInfo = "無法獲取位置信息";
                if (GetWindowRect(hWnd, out windowRect))
                {
                    int windowWidth = windowRect.Right - windowRect.Left;
                    int windowHeight = windowRect.Bottom - windowRect.Top;
                    positionInfo = $"位置: ({windowRect.Left}, {windowRect.Top})\n大小: {windowWidth}x{windowHeight}";
                }

                return $"標題: {windowTitle}\n進程: {processName}\n{positionInfo}";
            }
            catch
            {
                return "無法獲取視窗信息";
            }
        }

        public (bool success, string message, int x, int y, List<(string method, bool userConfirmed)> results) TestMouseClickMethods(AppSettings settings = null)
        {
            try
            {
                // 尋找 RO 遊戲視窗
                var targetWindow = FindRagnarokWindow();
                if (targetWindow == IntPtr.Zero)
                {
                    return (false, "未找到 Ragnarok Online 遊戲視窗！\n請確認遊戲已經啟動。", 0, 0, new List<(string, bool)>());
                }

                // 獲取視窗標題和進程信息用於顯示
                string windowInfo = GetWindowInfo(targetWindow);

                // 確保目標視窗在前台
                SetForegroundWindow(targetWindow);
                Thread.Sleep(500); // 增加等待時間確保視窗切換完成

                // 載入遊戲解析度設定
                if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
                {
                    _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
                }

                // 獲取當前解析度
                var width = _resolutionService.Width;
                var height = _resolutionService.Height;

                // 根據解析度計算同意按鈕位置
                var (testX, testY) = _resolutionService.GetAgreeButtonPosition();

                // 測試不同的點擊策略
                var testResults = new List<(string method, bool userConfirmed)>();
                string initialMessage = $"找到的遊戲視窗:\n{windowInfo}\n\n解析度: {width}x{height}\n測試座標: ({testX}, {testY})\n\n準備開始測試點擊\"同意\"按鈕...\n\n請確保遊戲畫面顯示登入界面，然後觀察每種方法是否成功點擊到同意按鈕。";

                return (true, initialMessage, testX, testY, testResults);
            }
            catch (Exception ex)
            {
                return (false, $"測試失敗: {ex.Message}", 0, 0, new List<(string, bool)>());
            }
        }

        public (bool success, string message) TestClickMethod1_SetCursorPos(AppSettings settings = null)
        {
            try
            {
                var targetWindow = FindRagnarokWindow();
                if (targetWindow == IntPtr.Zero)
                {
                    return (false, "未找到 RO 遊戲視窗");
                }

                SetForegroundWindow(targetWindow);
                Thread.Sleep(300);

                if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
                {
                    _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
                }

                var (testX, testY) = _resolutionService.GetAgreeButtonPosition();

                // 策略1: SetCursorPos + ClientToScreen + mouse_event
                var clientPoint = new System.Drawing.Point(testX, testY);
                bool converted = ClientToScreen(targetWindow, ref clientPoint);
                if (!converted)
                {
                    return (false, "座標轉換失敗");
                }

                // 移動滑鼠
                SetCursorPos(clientPoint.X, clientPoint.Y);
                Thread.Sleep(200);

                // 點擊
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);

                return (true, $"方法1: SetCursorPos + mouse_event\n已點擊座標: ({clientPoint.X}, {clientPoint.Y})\n\n是否成功點擊到\"同意\"按鈕？");
            }
            catch (Exception ex)
            {
                return (false, $"方法1執行失敗: {ex.Message}");
            }
        }

        public (bool success, string message) TestClickMethod2_SendInputRelative(AppSettings settings = null)
        {
            try
            {
                var targetWindow = FindRagnarokWindow();
                if (targetWindow == IntPtr.Zero)
                {
                    return (false, "未找到 RO 遊戲視窗");
                }

                SetForegroundWindow(targetWindow);
                Thread.Sleep(300);

                if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
                {
                    _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
                }

                var (testX, testY) = _resolutionService.GetAgreeButtonPosition();

                // 策略2: SendInput 相對移動 + 點擊
                var clientPoint = new System.Drawing.Point(testX, testY);
                bool converted = ClientToScreen(targetWindow, ref clientPoint);
                if (!converted)
                {
                    return (false, "座標轉換失敗");
                }

                // 獲取當前滑鼠位置
                System.Drawing.Point currentPos;
                GetCursorPos(out currentPos);

                // 計算相對移動量
                int deltaX = clientPoint.X - currentPos.X;
                int deltaY = clientPoint.Y - currentPos.Y;

                var inputs = new INPUT[3];

                // 移動滑鼠
                inputs[0] = new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = deltaX,
                            dy = deltaY,
                            mouseData = 0,
                            dwFlags = 0x0001, // MOUSEEVENTF_MOVE
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                // 滑鼠按下
                inputs[1] = new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_LEFTDOWN,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                // 滑鼠放開
                inputs[2] = new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_LEFTUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                SendInput(3, inputs, Marshal.SizeOf(typeof(INPUT)));

                return (true, $"方法2: SendInput 相對移動 + 點擊\n已點擊座標: ({clientPoint.X}, {clientPoint.Y})\n\n是否成功點擊到\"同意\"按鈕？");
            }
            catch (Exception ex)
            {
                return (false, $"方法2執行失敗: {ex.Message}");
            }
        }

        public (bool success, string message) TestClickMethod3_SendInputAbsolute(AppSettings settings = null)
        {
            try
            {
                var targetWindow = FindRagnarokWindow();
                if (targetWindow == IntPtr.Zero)
                {
                    return (false, "未找到 RO 遊戲視窗");
                }

                SetForegroundWindow(targetWindow);
                Thread.Sleep(300);

                if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
                {
                    _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
                }

                var (testX, testY) = _resolutionService.GetAgreeButtonPosition();

                // 策略3: SendInput 絕對移動 + 點擊
                var clientPoint = new System.Drawing.Point(testX, testY);
                bool converted = ClientToScreen(targetWindow, ref clientPoint);
                if (!converted)
                {
                    return (false, "座標轉換失敗");
                }

                // 轉換為絕對座標 (0-65535)
                int absX = (clientPoint.X * 65536) / System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
                int absY = (clientPoint.Y * 65536) / System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;

                var inputs = new INPUT[3];

                // 移動滑鼠到絕對位置
                inputs[0] = new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = absX,
                            dy = absY,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_ABSOLUTE | 0x0001, // MOUSEEVENTF_MOVE
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                // 滑鼠按下
                inputs[1] = new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_LEFTDOWN,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                // 滑鼠放開
                inputs[2] = new INPUT
                {
                    type = INPUT_MOUSE,
                    union = new INPUTUNION
                    {
                        mouse = new MOUSEINPUT
                        {
                            dx = 0,
                            dy = 0,
                            mouseData = 0,
                            dwFlags = MOUSEEVENTF_LEFTUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                SendInput(3, inputs, Marshal.SizeOf(typeof(INPUT)));

                return (true, $"方法3: SendInput 絕對移動 + 點擊\n已點擊座標: ({clientPoint.X}, {clientPoint.Y})\n\n是否成功點擊到\"同意\"按鈕？");
            }
            catch (Exception ex)
            {
                return (false, $"方法3執行失敗: {ex.Message}");
            }
        }

        public (bool success, string message) TestClickMethod4_PostMessage(AppSettings settings = null)
        {
            try
            {
                var targetWindow = FindRagnarokWindow();
                if (targetWindow == IntPtr.Zero)
                {
                    return (false, "未找到 RO 遊戲視窗");
                }

                SetForegroundWindow(targetWindow);
                Thread.Sleep(300);

                if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
                {
                    _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
                }

                var (testX, testY) = _resolutionService.GetAgreeButtonPosition();

                // 策略4: PostMessage 直接發送點擊消息
                IntPtr lParam = (IntPtr)((testY << 16) | (testX & 0xFFFF));

                // 發送滑鼠移動消息
                PostMessage(targetWindow, WM_MOUSEMOVE, IntPtr.Zero, lParam);
                Thread.Sleep(50);

                // 發送滑鼠按下消息
                PostMessage(targetWindow, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
                Thread.Sleep(50);

                // 發送滑鼠放開消息
                PostMessage(targetWindow, WM_LBUTTONUP, IntPtr.Zero, lParam);

                return (true, $"方法4: PostMessage 直接點擊\n已點擊座標: ({testX}, {testY}) (視窗相對座標)\n注意：此方法不移動實體滑鼠\n\n是否成功點擊到\"同意\"按鈕？");
            }
            catch (Exception ex)
            {
                return (false, $"方法4執行失敗: {ex.Message}");
            }
        }

        private void ClickUsingMethod1(IntPtr targetWindow, int x, int y)
        {
            // 方法1: SetCursorPos + ClientToScreen + mouse_event (經測試驗證的最佳方法)
            LogService.Instance.Debug("[ClickUsingMethod1] 開始點擊 - 目標座標: ({0}, {1}), 視窗: {2}", x, y, targetWindow);

            var clientPoint = new System.Drawing.Point(x, y);
            bool converted = ClientToScreen(targetWindow, ref clientPoint);
            if (!converted)
            {
                LogService.Instance.Error("[ClickUsingMethod1] 座標轉換失敗");
                throw new InvalidOperationException("座標轉換失敗");
            }

            LogService.Instance.Debug("[ClickUsingMethod1] 座標轉換成功 - 螢幕座標: ({0}, {1})", clientPoint.X, clientPoint.Y);

            // 移動滑鼠到目標位置
            bool moveResult = SetCursorPos(clientPoint.X, clientPoint.Y);
            LogService.Instance.Debug("[ClickUsingMethod1] SetCursorPos 結果: {0}", moveResult);
            Thread.Sleep(200); // 使用測試驗證的等待時間

            // 執行點擊操作
            LogService.Instance.Debug("[ClickUsingMethod1] 開始點擊操作");
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
            Thread.Sleep(50);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
            LogService.Instance.Debug("[ClickUsingMethod1] 點擊操作完成");
        }

        public bool IsCurrentWindowRagnarok(int targetProcessId = 0)
        {
            var currentWindow = GetForegroundWindow();
            if (currentWindow == IntPtr.Zero)
            {
                LogService.Instance.Debug("[IsCurrentWindowRagnarok] 無法取得前台視窗");
                return false;
            }

            // 如果有指定 PID，使用 PID 精確比對
            if (targetProcessId > 0)
            {
                uint processId;
                GetWindowThreadProcessId(currentWindow, out processId);
                bool pidMatch = (int)processId == targetProcessId;
                LogService.Instance.Debug("[IsCurrentWindowRagnarok] 前台視窗PID: {0}, 目標PID: {1}, 匹配: {2}", processId, targetProcessId, pidMatch);
                return pidMatch;
            }

            // 沒有指定 PID 時使用增強的檢測邏輯
            return IsRagnarokWindow(currentWindow);
        }

        private string GetWindowTitle(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return string.Empty;

            try
            {
                int length = GetWindowTextLength(windowHandle);
                if (length <= 0)
                    return string.Empty;

                var titleBuilder = new StringBuilder(length + 1);
                int actualLength = GetWindowText(windowHandle, titleBuilder, titleBuilder.Capacity);
                return actualLength > 0 ? titleBuilder.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("[GetWindowTitle] 取得視窗標題時發生錯誤: {0}", ex.Message);
                return string.Empty;
            }
        }

        private bool IsRagnarokWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
                return false;

            try
            {
                // 檢查視窗標題是否為 "Ragnarok : Zero"
                var title = GetWindowTitle(windowHandle);
                LogService.Instance.Debug("[IsRagnarokWindow] 檢查視窗標題: '{0}'", title);

                if (string.IsNullOrEmpty(title))
                    return false;

                // 只檢查是否為 "Ragnarok : Zero"，如果不是則檢查進程名稱
                if (title.Equals("Ragnarok : Zero", StringComparison.OrdinalIgnoreCase))
                    return true;

                // 如果標題不是 "Ragnarok : Zero"，但可能是已登入的RO視窗，檢查進程
                return CheckProcessName(windowHandle);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "[IsRagnarokWindow] 檢查視窗時發生錯誤");
                return false;
            }
        }

        private bool CheckProcessName(IntPtr windowHandle)
        {
            try
            {
                uint processId;
                GetWindowThreadProcessId(windowHandle, out processId);

                var process = System.Diagnostics.Process.GetProcessById((int)processId);
                var processName = process.ProcessName.ToLowerInvariant();
                var processPath = process.MainModule?.FileName?.ToLowerInvariant() ?? "";

                LogService.Instance.Debug("[CheckProcessName] 進程名稱: '{0}', 路徑: '{1}'", processName, processPath);

                // 檢查是否為 RO 相關的進程名稱
                bool isRoProcess = processName.Contains("Ragnarok : Zero");

                LogService.Instance.Debug("[CheckProcessName] RO進程檢測結果: {0}", isRoProcess);
                return isRoProcess;
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("[CheckProcessName] 檢查進程名稱時發生錯誤: {0}", ex.Message);
                return false;
            }
        }

        private void CheckRagnarokWindowFocus(int targetProcessId = 0)
        {
            if (!IsCurrentWindowRagnarok(targetProcessId))
            {
                if (targetProcessId > 0)
                {
                    LogService.Instance.Error("[CheckRagnarokWindowFocus] 前台視窗PID不匹配，目標PID: {0}", targetProcessId);
                    throw new InvalidOperationException($"當前前台視窗不是目標 Ragnarok Online 遊戲視窗（PID: {targetProcessId}）！操作已停止以確保安全。");
                }
                else
                {
                    LogService.Instance.Error("[CheckRagnarokWindowFocus] 前台視窗不是RO視窗");
                    throw new InvalidOperationException("當前前台視窗不是 Ragnarok Online 遊戲視窗！操作已停止以確保安全。");
                }
            }
        }

        private string WaitForValidOtpTime(string otpSecret, int targetProcessId = 0)
        {
            var totpGenerator = new TotpGenerator();

            while (true)
            {
                var remainingTime = totpGenerator.GetTimeRemaining();

                // 如果剩餘時間大於等於 5 秒，直接生成並返回 OTP
                if (remainingTime >= 5)
                {
                    return totpGenerator.GenerateTotpWithTiming(otpSecret);
                }

                // 如果剩餘時間小於 5 秒，等待到下一個周期
                LogService.Instance.Info("[WaitForValidOtpTime] OTP 剩餘時間不足（{0}秒），等待下一個周期...", remainingTime);

                // 等待到下一個 30 秒周期 + 1 秒緩衝
                Thread.Sleep((remainingTime + 1) * 1000);

                // 重新檢查視窗焦點，確保等待期間視窗沒有失去焦點
                CheckRagnarokWindowFocus(targetProcessId);
            }
        }
    }
}