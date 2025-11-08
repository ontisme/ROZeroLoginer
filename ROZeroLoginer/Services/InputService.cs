using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly WindowService _windowService;
        private readonly AppSettings _settings;

        // 記錄已經登入的視窗句柄，避免重複使用
        private static readonly HashSet<IntPtr> _loggedInWindows = new HashSet<IntPtr>();
        private static readonly object _loggedInWindowsLock = new object();

        public InputService(AppSettings settings = null)
        {
            _resolutionService = new GameResolutionService();
            _windowService = new WindowService(settings);
            _settings = settings ?? new AppSettings();
        }

        /// <summary>
        /// 檢查並確保 NumLock 是開啟的
        /// 根據用戶反饋,NumLock 關閉會導致視窗無響應
        /// </summary>
        private void EnsureNumLockEnabled()
        {
            try
            {
                short numLockState = GetKeyState(VK_NUMLOCK);
                bool isNumLockOn = (numLockState & 0x0001) != 0;

                if (!isNumLockOn)
                {
                    LogService.Instance.Warning("[EnsureNumLock] NumLock 未開啟,嘗試自動開啟");

                    // 模擬按下 NumLock 鍵來切換狀態
                    SendKey(Keys.NumLock);
                    Thread.Sleep(100);

                    // 驗證是否成功開啟
                    numLockState = GetKeyState(VK_NUMLOCK);
                    isNumLockOn = (numLockState & 0x0001) != 0;

                    if (isNumLockOn)
                    {
                        LogService.Instance.Info("[EnsureNumLock] NumLock 已自動開啟");
                    }
                    else
                    {
                        LogService.Instance.Warning("[EnsureNumLock] 無法自動開啟 NumLock,建議手動開啟以避免視窗無響應問題");
                    }
                }
                else
                {
                    LogService.Instance.Debug("[EnsureNumLock] NumLock 已開啟");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warning("[EnsureNumLock] 檢查 NumLock 狀態時發生錯誤: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 獲取當前鍵盤鎖定狀態信息(用於日誌記錄)
        /// </summary>
        private string GetKeyboardLockStates()
        {
            try
            {
                short numLockState = GetKeyState(VK_NUMLOCK);
                short capsLockState = GetKeyState(VK_CAPITAL);
                short scrollLockState = GetKeyState(VK_SCROLL);

                bool numLockOn = (numLockState & 0x0001) != 0;
                bool capsLockOn = (capsLockState & 0x0001) != 0;
                bool scrollLockOn = (scrollLockState & 0x0001) != 0;

                return $"NumLock:{(numLockOn ? "開" : "關")}, CapsLock:{(capsLockOn ? "開" : "關")}, ScrollLock:{(scrollLockOn ? "開" : "關")}";
            }
            catch
            {
                return "無法獲取";
            }
        }

        /// <summary>
        /// 檢查標題是否匹配任何配置的遊戲標題（完全匹配）
        /// </summary>
        private bool IsGameTitle(string title)
        {
            return _settings.GetEffectiveGameTitles().Any(gameTitle => title == gameTitle);
        }

        /// <summary>
        /// 檢查標題是否匹配任何配置的遊戲標題（完全匹配）- 靜態版本
        /// </summary>
        private static bool IsGameTitle(string title, AppSettings settings)
        {
            return settings?.GetEffectiveGameTitles()?.Any(gameTitle => title == gameTitle) ?? false;
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
        public static IntPtr WaitForRoWindowByPid(int targetPid, AppSettings settings = null, int timeoutMs = 30000, int checkIntervalMs = 500)
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

                                if (IsGameTitle(title, settings ?? new AppSettings()))
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

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private const int VK_NUMLOCK = 0x90;
        private const int VK_CAPITAL = 0x14;
        private const int VK_SCROLL = 0x91;

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

        public void SendLogin(string username, string password, string otpSecret, int otpDelayMs = 2000, AppSettings settings = null, bool skipAgreeButton = false, int targetProcessId = 0, int server = 1, int character = 1, int lastCharacter = 1, bool autoSelectServer = true, bool autoSelectCharacter = true)
        {
            LogService.Instance.Info("[SendLogin] 開始登入流程 - 用戶: {0}, 跳過同意按鈕: {1}, 目標PID: {2}", username, skipAgreeButton, targetProcessId);

            // 記錄當前鍵盤鎖定狀態
            var keyboardStates = GetKeyboardLockStates();
            LogService.Instance.Info("[SendLogin] 當前鍵盤狀態: {0}", keyboardStates);

            // 確保 NumLock 開啟(根據用戶反饋,這能避免視窗無響應問題)
            EnsureNumLockEnabled();

            try
            {
                // 1. 查找目標遊戲視窗
                var targetWindow = FindTargetWindow(targetProcessId);

                // 2. 準備視窗環境(包含視窗就緒檢測)
                PrepareWindow(targetWindow, targetProcessId, settings);

                // 3. 處理同意按鈕（如果需要）
                if (!skipAgreeButton)
                {
                    HandleAgreeButton(targetWindow, targetProcessId);
                }

                // 4. 輸入帳號密碼
                InputCredentials(targetWindow, username, password, targetProcessId);

                // 5. 輸入 OTP
                InputOTP(targetWindow, otpSecret, otpDelayMs, targetProcessId);

                // 6. 選擇伺服器
                SelectServer(targetWindow, server, autoSelectServer, targetProcessId);

                // 7. 選擇角色
                SelectCharacter(targetWindow, character, lastCharacter, autoSelectCharacter, targetProcessId);

                // 8. 完成登入
                FinalizeLogin(targetWindow);

                LogService.Instance.Info("[SendLogin] 登入流程完成");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("[SendLogin] 登入流程發生錯誤: {0}", ex.Message);
                throw;
            }
        }

        #region 私有登入步驟方法

        /// <summary>
        /// 查找目標遊戲視窗
        /// </summary>
        private IntPtr FindTargetWindow(int targetProcessId)
        {
            IntPtr targetWindow = IntPtr.Zero;

            try
            {
                if (targetProcessId > 0)
                {
                    LogService.Instance.Info("[FindTargetWindow] 使用PID {0} 查找RO視窗 (批次啟動模式)", targetProcessId);
                    targetWindow = FindRagnarokWindowByPid(targetProcessId);
                    LogService.Instance.Debug("[FindTargetWindow] 根據PID {0} 查找視窗結果: {1}", targetProcessId, targetWindow);

                    // 如果PID方法失敗，嘗試通用方法作為備份
                    if (targetWindow == IntPtr.Zero)
                    {
                        LogService.Instance.Warning("[FindTargetWindow] PID方法失敗，嘗試通用方法作為備份");
                        targetWindow = FindRagnarokWindow(true);
                        LogService.Instance.Debug("[FindTargetWindow] 備份通用方法查找結果: {0}", targetWindow);
                    }
                }
                else
                {
                    LogService.Instance.Info("[FindTargetWindow] 使用通用方法查找RO視窗 (單個登入模式)");
                    targetWindow = FindRagnarokWindow(false);
                    LogService.Instance.Debug("[FindTargetWindow] 使用通用方法查找視窗結果: {0}", targetWindow);
                }
            }
            catch (Exception findEx)
            {
                LogService.Instance.Error("[FindTargetWindow] 查找視窗時發生異常: {0}", findEx.ToString());

                // 嘗試最後的備用方法
                if (targetProcessId > 0)
                {
                    LogService.Instance.Info("[FindTargetWindow] 嘗試最後的備用方法");
                    try
                    {
                        targetWindow = FindRagnarokWindow(true);
                        LogService.Instance.Debug("[FindTargetWindow] 備用方法結果: {0}", targetWindow);
                    }
                    catch (Exception backupEx)
                    {
                        LogService.Instance.Error("[FindTargetWindow] 備用方法也失敗: {0}", backupEx.Message);
                    }
                }

                if (targetWindow == IntPtr.Zero)
                {
                    throw;
                }
            }

            if (targetWindow == IntPtr.Zero)
            {
                string errorMsg = targetProcessId > 0
                    ? $"未找到 PID {targetProcessId} 對應的 Ragnarok Online 遊戲視窗！請確認遊戲已經啟動且進程正確。"
                    : "未找到 Ragnarok Online 遊戲視窗！請確認遊戲已經啟動。";

                LogService.Instance.Error("[FindTargetWindow] {0}", errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            LogService.Instance.Info("[FindTargetWindow] 找到 RO 視窗: {0}", targetWindow);
            return targetWindow;
        }

        /// <summary>
        /// 準備視窗環境（設為前台、載入解析度、等待就緒）
        /// </summary>
        private void PrepareWindow(IntPtr targetWindow, int targetProcessId, AppSettings settings)
        {
            LogService.Instance.Info("[PrepareWindow] 開始準備視窗環境");

            // 載入遊戲解析度設定
            if (settings != null && !string.IsNullOrEmpty(settings.RoGamePath))
            {
                _resolutionService.LoadResolutionFromConfig(settings.RoGamePath);
            }

            // 使用增強的視窗就緒檢查(包含焦點設置和就緒狀態檢測)
            bool isReady = _windowService.EnsureWindowFocusedAndReady(
                targetWindow,
                targetProcessId,
                _settings.WindowFocusDelayMs);

            if (!isReady)
            {
                throw new InvalidOperationException("視窗準備失敗: 無法設置焦點或視窗未就緒");
            }

            // 額外等待視窗穩定
            Thread.Sleep(_settings.StepDelayMs);
            LogService.Instance.Info("[PrepareWindow] 視窗環境準備完成");
        }

        /// <summary>
        /// 處理同意按鈕
        /// </summary>
        private void HandleAgreeButton(IntPtr targetWindow, int targetProcessId)
        {
            LogService.Instance.Info("[HandleAgreeButton] 開始同意按鈕點擊流程");

            int agreeX, agreeY;

            // 優先使用自定義位置
            if (_settings.UseCustomAgreeButtonPosition &&
                _settings.CustomAgreeButtonX > 0 &&
                _settings.CustomAgreeButtonY > 0)
            {
                agreeX = _settings.CustomAgreeButtonX;
                agreeY = _settings.CustomAgreeButtonY;
                LogService.Instance.Info("[HandleAgreeButton] 使用自定義同意按鈕位置: ({0}, {1})", agreeX, agreeY);
            }
            else
            {
                // 根據解析度計算同意按鈕位置
                (agreeX, agreeY) = _resolutionService.GetAgreeButtonPosition();
                LogService.Instance.Debug("[HandleAgreeButton] 使用預設同意按鈕位置: ({0}, {1})", agreeX, agreeY);
            }

            // 檢查視窗焦點並點擊同意按鈕
            CheckRagnarokWindowFocus(targetProcessId);
            LogService.Instance.Debug("[HandleAgreeButton] 視窗焦點檢查通過，開始點擊同意按鈕");
            ClickUsingMethod1(targetWindow, agreeX, agreeY);
            Thread.Sleep(_settings.MouseClickDelayMs);
            LogService.Instance.Debug("[HandleAgreeButton] 同意按鈕點擊完成");
        }

        /// <summary>
        /// 輸入帳號密碼
        /// </summary>
        private void InputCredentials(IntPtr targetWindow, string username, string password, int targetProcessId)
        {
            LogService.Instance.Debug("[InputCredentials] 開始輸入帳號密碼");

            // 輸入帳號
            WaitAndEnsureReady(targetWindow, targetProcessId);
            SendText(username);

            // 按下 TAB 鍵切換到密碼欄位
            WaitAndEnsureReady(targetWindow, targetProcessId);
            SendKey(Keys.Tab);

            // 輸入密碼
            WaitAndEnsureReady(targetWindow, targetProcessId);
            SendText(password);

            // 按下 ENTER 鍵提交
            WaitAndEnsureReady(targetWindow, targetProcessId);
            SendKey(Keys.Enter);

            LogService.Instance.Debug("[InputCredentials] 帳號密碼輸入完成");
        }

        /// <summary>
        /// 輸入 OTP
        /// </summary>
        private void InputOTP(IntPtr targetWindow, string otpSecret, int otpDelayMs, int targetProcessId)
        {
            // 檢查是否啟用自動輸入 OTP
            if (!_settings.AutoInputOtp)
            {
                LogService.Instance.Info("[InputOTP] 自動輸入 OTP 已停用，跳過 OTP 輸入");
                return;
            }

            LogService.Instance.Debug("[InputOTP] 開始 OTP 輸入流程");

            // 等待 OTP 視窗出現
            Thread.Sleep(otpDelayMs);

            // 檢查並等待 OTP 時間充足再輸入
            WaitAndEnsureReady(targetWindow, targetProcessId);
            string finalOtp = WaitForValidOtpTime(otpSecret, targetProcessId);
            SendText(finalOtp);
            Thread.Sleep(_settings.KeyboardInputDelayMs);

            // 按下 ENTER 鍵提交
            WaitAndEnsureReady(targetWindow, targetProcessId);
            SendKey(Keys.Enter);

            LogService.Instance.Debug("[InputOTP] OTP 輸入完成");
        }

        /// <summary>
        /// 選擇伺服器
        /// </summary>
        private void SelectServer(IntPtr targetWindow, int server, bool autoSelectServer, int targetProcessId)
        {
            LogService.Instance.Debug("[SelectServer] 開始伺服器選擇 - 伺服器: {0}, 自動選擇: {1}", server, autoSelectServer);

            WaitAndEnsureReady(targetWindow, targetProcessId);

            if (autoSelectServer && server > 0)
            {
                LogService.Instance.Debug("[SelectServer] 執行伺服器選擇邏輯");

                // 強制先往上回到第一個位置
                for (int i = 0; i < 10; i++)
                {
                    SendKey(Keys.Up);
                    Thread.Sleep(_settings.ServerSelectionDelayMs);
                }

                // 然後往下選擇指定的伺服器 (server-1 次，因為已經在第一個位置)
                for (int i = 1; i < server; i++)
                {
                    SendKey(Keys.Down);
                    Thread.Sleep(_settings.ServerSelectionDelayMs);
                }

                // ✅ 只在自動選擇時才按Enter確認
                WaitAndEnsureReady(targetWindow, targetProcessId);
                SendKey(Keys.Enter);
                LogService.Instance.Debug("[SelectServer] 伺服器選擇完成並確認進入");
            }
            else
            {
                // ✅ 如果不自動選擇,停留在伺服器選擇畫面,不執行Enter
                LogService.Instance.Info("[SelectServer] 未勾選自動選擇伺服器,停留在伺服器選擇畫面");
            }
        }

        /// <summary>
        /// 選擇角色
        /// </summary>
        private void SelectCharacter(IntPtr targetWindow, int character, int lastCharacter, bool autoSelectCharacter, int targetProcessId)
        {
            LogService.Instance.Debug("[SelectCharacter] 開始角色選擇 - 角色: {0}, 上次角色: {1}, 自動選擇: {2}", character, lastCharacter, autoSelectCharacter);

            WaitAndEnsureReady(targetWindow, targetProcessId);

            if (autoSelectCharacter && character > 0)
            {
                LogService.Instance.Debug("[SelectCharacter] 執行角色選擇邏輯 - 目標角色: {0}", character);

                // 5x3 網格的絕對定位邏輯
                // 角色排列：1-5第一排，6-10第二排，11-15第三排

                // 強制回到左上角 (角色1的位置)
                for (int i = 0; i < 10; i++)
                {
                    SendKey(Keys.Left);
                    Thread.Sleep(_settings.CharacterSelectionDelayMs);
                }

                for (int i = 0; i < 10; i++)
                {
                    SendKey(Keys.Up);
                    Thread.Sleep(_settings.CharacterSelectionDelayMs);
                }

                // 計算目標位置
                int targetRow = (character - 1) / 5; // 0=第一排, 1=第二排, 2=第三排
                int targetCol = (character - 1) % 5; // 0-4 對應左到右的位置

                LogService.Instance.Debug("[SelectCharacter] 目標位置: 第{0}排, 第{1}列", targetRow + 1, targetCol + 1);

                // 移動到目標行
                for (int i = 0; i < targetRow; i++)
                {
                    SendKey(Keys.Down);
                    Thread.Sleep(_settings.CharacterSelectionDelayMs);
                }

                // 移動到目標列
                for (int i = 0; i < targetCol; i++)
                {
                    SendKey(Keys.Right);
                    Thread.Sleep(_settings.CharacterSelectionDelayMs);
                }

                LogService.Instance.Debug("[SelectCharacter] 已移動到角色{0}的位置", character);

                // ✅ 只在自動選擇時才按Enter確認進入遊戲
                WaitAndEnsureReady(targetWindow, targetProcessId);
                SendKey(Keys.Enter);
                LogService.Instance.Debug("[SelectCharacter] 角色選擇完成並確認進入遊戲");
            }
            else
            {
                // ✅ 如果不自動選擇,停留在角色選擇畫面,不執行Enter
                LogService.Instance.Info("[SelectCharacter] 未勾選自動選擇角色,停留在角色選擇畫面");
            }
        }

        /// <summary>
        /// 完成登入（標記視窗）
        /// </summary>
        private void FinalizeLogin(IntPtr targetWindow)
        {
            LogService.Instance.Debug("[FinalizeLogin] 開始完成登入流程");

            // 標記視窗為已登入，避免重複使用
            MarkWindowAsLoggedIn(targetWindow);
            LogService.Instance.Info("[FinalizeLogin] 登入流程完成，已標記視窗 {0} 為已登入狀態", targetWindow);
        }

        #endregion

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
            Thread.Sleep(_settings.KeyboardInputDelayMs);
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

            Thread.Sleep(_settings.KeyboardInputDelayMs);
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
            Thread.Sleep(_settings.KeyboardInputDelayMs);

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
        /// 查找可用的 Ragnarok Online 視窗 - 匹配配置的遊戲標題且未登入的視窗
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

                    // 檢查是否匹配任何配置的遊戲標題
                    if (IsGameTitle(title))
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
        /// 根據 PID 查找 Ragnarok Online 視窗 - 匹配配置的遊戲標題的視窗
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

                        // 檢查是否匹配任何配置的遊戲標題
                        if (IsGameTitle(title))
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

        public void ClickUsingMethod1(IntPtr targetWindow, int x, int y)
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
                // 檢查視窗標題是否匹配配置的遊戲標題
                var title = GetWindowTitle(windowHandle);
                LogService.Instance.Debug("[IsRagnarokWindow] 檢查視窗標題: '{0}'", title);

                if (string.IsNullOrEmpty(title))
                    return false;

                // 檢查是否匹配任何配置的遊戲標題，如果不是則檢查進程名稱
                if (IsGameTitle(title))
                    return true;

                // 如果標題不匹配配置的標題，但可能是已登入的RO視窗，檢查進程
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

                var process = Process.GetProcessById((int)processId);
                var processName = process.ProcessName.ToLowerInvariant();
                var processPath = process.MainModule?.FileName?.ToLowerInvariant() ?? "";

                LogService.Instance.Debug("[CheckProcessName] 進程名稱: '{0}', 路徑: '{1}'", processName, processPath);

                // 檢查是否為 RO 相關的進程名稱
                bool isRoProcess = _settings.GetEffectiveGameTitles().Any(gameTitle => processName.Contains(gameTitle));

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

        /// <summary>
        /// 確保視窗就緒並獲得焦點(增強版本)
        /// 結合視窗焦點檢查和視窗就緒狀態檢測
        /// </summary>
        /// <param name="windowHandle">視窗句柄</param>
        /// <param name="targetProcessId">目標進程ID</param>
        private void EnsureWindowReadyAndFocused(IntPtr windowHandle, int targetProcessId = 0)
        {
            try
            {
                LogService.Instance.Debug("[EnsureWindowReady] 開始確保視窗就緒: 視窗 {0}, PID {1}",
                    windowHandle.ToInt64(), targetProcessId);

                // 使用 WindowService 的綜合檢查(包含自動重試焦點)
                bool isReady = _windowService.EnsureWindowFocusedAndReady(
                    windowHandle,
                    targetProcessId,
                    _settings.WindowFocusDelayMs,
                    _settings.WindowFocusRetries);

                if (!isReady)
                {
                    string errorMsg = targetProcessId > 0
                        ? $"視窗未就緒或焦點設置失敗（PID: {targetProcessId}）"
                        : "視窗未就緒或焦點設置失敗";

                    LogService.Instance.Error("[EnsureWindowReady] {0}", errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                LogService.Instance.Debug("[EnsureWindowReady] 視窗就緒確認完成");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("[EnsureWindowReady] 確保視窗就緒時發生錯誤: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 在執行輸入操作前等待一小段時間並檢查視窗狀態
        /// </summary>
        private void WaitAndEnsureReady(IntPtr windowHandle, int targetProcessId = 0)
        {
            LogService.Instance.Debug($"[WaitAndEnsureReady] 步驟延遲 ${_settings.StepDelayMs}ms");
            Thread.Sleep(_settings.StepDelayMs);
            EnsureWindowReadyAndFocused(windowHandle, targetProcessId);
        }

        private string WaitForValidOtpTime(string otpSecret, int targetProcessId = 0)
        {
            var otpService = new OtpService();

            while (true)
            {
                var remainingTime = otpService.GetTimeRemaining();

                // 如果剩餘時間大於等於 2 秒，直接生成並返回 OTP
                if (remainingTime >= 2)
                {
                    return otpService.GenerateTotpWithTiming(otpSecret);
                }

                // 如果剩餘時間小於 2 秒，等待到下一個周期
                LogService.Instance.Info("[WaitForValidOtpTime] OTP 剩餘時間不足（{0}秒），等待下一個周期...", remainingTime);

                // 等待到下一個 30 秒周期 + 1 秒緩衝
                Thread.Sleep((remainingTime + 1) * 1000);

                // 重新檢查視窗焦點，確保等待期間視窗沒有失去焦點
                CheckRagnarokWindowFocus(targetProcessId);
            }
        }
    }
}