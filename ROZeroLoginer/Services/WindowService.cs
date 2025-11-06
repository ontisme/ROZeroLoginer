using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ROZeroLoginer.Models;

namespace ROZeroLoginer.Services
{
    /// <summary>
    /// 統一的視窗管理服務
    /// 整合視窗驗證、就緒檢測、焦點管理等功能
    /// </summary>
    public class WindowService
    {
        private readonly AppSettings _settings;

        private const int WM_NULL = 0x0000;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint SMTO_BLOCK = 0x0001;

        public WindowService(AppSettings settings = null)
        {
            _settings = settings ?? new AppSettings();
        }

        #region Win32 API

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        #endregion

        #region 視窗驗證功能 (來自 WindowValidationService)

        /// <summary>
        /// 檢查當前前台視窗是否為 Ragnarok Online 遊戲視窗
        /// </summary>
        public bool IsRagnarokWindow()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return false;

                return IsRagnarokWindow(foregroundWindow);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("[WindowService] 檢查 RO 視窗時發生錯誤: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 檢查指定視窗是否為 Ragnarok Online 遊戲視窗
        /// </summary>
        public bool IsRagnarokWindow(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return false;

                // 檢查視窗標題
                if (CheckWindowTitle(windowHandle))
                    return true;

                // 檢查進程名稱
                if (CheckProcessName(windowHandle))
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("[WindowService] 檢查視窗時發生錯誤: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 檢查視窗標題是否匹配 RO 遊戲
        /// </summary>
        private bool CheckWindowTitle(IntPtr windowHandle)
        {
            try
            {
                int length = GetWindowTextLength(windowHandle);
                if (length == 0)
                    return false;

                StringBuilder windowTitle = new StringBuilder(length + 1);
                GetWindowText(windowHandle, windowTitle, windowTitle.Capacity);

                string title = windowTitle.ToString();

                // 檢查是否匹配任何配置的遊戲標題（完全匹配）
                bool exactMatch = _settings.GetEffectiveGameTitles().Any(gameTitle => title == gameTitle);

                // 如果沒有完全匹配，檢查是否包含其他可能的 RO 標題（向後兼容）
                if (!exactMatch)
                {
                    return title.Contains("Ragnarok Online") ||
                           title.Contains("RO：仙境傳說") ||
                           title.Contains("仙境傳說") ||
                           _settings.GetEffectiveGameTitles().Any(gameTitle => title.Contains(gameTitle));
                }

                return exactMatch;
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("[WindowService] 檢查視窗標題時發生錯誤: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 檢查進程名稱是否為 RO 遊戲
        /// </summary>
        private bool CheckProcessName(IntPtr windowHandle)
        {
            try
            {
                uint processId;
                GetWindowThreadProcessId(windowHandle, out processId);

                if (processId == 0)
                    return false;

                Process process = Process.GetProcessById((int)processId);
                string processName = process.ProcessName.ToLower();

                // 檢查是否是 RO 相關的進程
                return processName.Contains("ragexe");
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("[WindowService] 檢查進程名稱時發生錯誤: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 獲取當前前台視窗的詳細信息
        /// </summary>
        public string GetCurrentWindowInfo()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return "無法獲取當前視窗";

                return GetWindowInfo(foregroundWindow);
            }
            catch (Exception ex)
            {
                return $"錯誤: {ex.Message}";
            }
        }

        /// <summary>
        /// 獲取指定視窗的詳細信息
        /// </summary>
        public string GetWindowInfo(IntPtr windowHandle)
        {
            try
            {
                if (windowHandle == IntPtr.Zero)
                    return "無效的視窗句柄";

                // 獲取視窗標題
                int length = GetWindowTextLength(windowHandle);
                StringBuilder windowTitle = new StringBuilder(length + 1);
                GetWindowText(windowHandle, windowTitle, windowTitle.Capacity);

                // 獲取進程信息
                uint processId;
                GetWindowThreadProcessId(windowHandle, out processId);
                string processName = "Unknown";
                string processPath = "Unknown";

                if (processId != 0)
                {
                    try
                    {
                        Process process = Process.GetProcessById((int)processId);
                        processName = process.ProcessName;
                        processPath = process.MainModule?.FileName ?? "Unknown";
                    }
                    catch { }
                }

                return $"視窗句柄: {windowHandle.ToInt64()}\n" +
                       $"視窗標題: {windowTitle}\n" +
                       $"進程ID: {processId}\n" +
                       $"進程名稱: {processName}\n" +
                       $"進程路徑: {processPath}";
            }
            catch (Exception ex)
            {
                return $"錯誤: {ex.Message}";
            }
        }

        #endregion

        #region 視窗就緒檢測功能 (來自 WindowReadinessService)

        /// <summary>
        /// 檢查視窗是否完全就緒並可接收輸入
        /// </summary>
        /// <param name="windowHandle">視窗句柄</param>
        /// <param name="maxRetries">最大重試次數</param>
        /// <param name="retryDelayMs">重試間隔(毫秒)</param>
        /// <returns>視窗是否就緒</returns>
        public bool IsWindowReady(IntPtr windowHandle, int maxRetries = 10, int retryDelayMs = 300)
        {
            if (windowHandle == IntPtr.Zero)
            {
                LogService.Instance.Warning("[WindowService] 視窗句柄為空");
                return false;
            }

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // 1. 檢查視窗是否存在且可見
                    if (!IsWindow(windowHandle))
                    {
                        LogService.Instance.Warning("[WindowService] 嘗試 {0}/{1}: 視窗不存在", attempt, maxRetries);
                        Thread.Sleep(retryDelayMs);
                        continue;
                    }

                    if (!IsWindowVisible(windowHandle))
                    {
                        LogService.Instance.Debug("[WindowService] 嘗試 {0}/{1}: 視窗不可見", attempt, maxRetries);
                        Thread.Sleep(retryDelayMs);
                        continue;
                    }

                    // 2. 檢查視窗是否響應
                    IntPtr result;
                    IntPtr sendResult = SendMessageTimeout(
                        windowHandle,
                        WM_NULL,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        SMTO_ABORTIFHUNG | SMTO_BLOCK,
                        1000,
                        out result);

                    if (sendResult == IntPtr.Zero)
                    {
                        LogService.Instance.Debug("[WindowService] 嘗試 {0}/{1}: 視窗無響應", attempt, maxRetries);
                        Thread.Sleep(retryDelayMs);
                        continue;
                    }

                    // 3. 檢查視窗客戶區
                    RECT clientRect;
                    if (!GetClientRect(windowHandle, out clientRect))
                    {
                        LogService.Instance.Debug("[WindowService] 嘗試 {0}/{1}: 無法獲取客戶區", attempt, maxRetries);
                        Thread.Sleep(retryDelayMs);
                        continue;
                    }

                    int width = clientRect.Right - clientRect.Left;
                    int height = clientRect.Bottom - clientRect.Top;

                    if (width <= 0 || height <= 0)
                    {
                        LogService.Instance.Debug("[WindowService] 嘗試 {0}/{1}: 客戶區無效 ({2}x{3})",
                            attempt, maxRetries, width, height);
                        Thread.Sleep(retryDelayMs);
                        continue;
                    }

                    // 4. 檢查視窗繪製上下文
                    IntPtr dc = GetWindowDC(windowHandle);
                    if (dc == IntPtr.Zero)
                    {
                        LogService.Instance.Debug("[WindowService] 嘗試 {0}/{1}: 無法獲取DC", attempt, maxRetries);
                        Thread.Sleep(retryDelayMs);
                        continue;
                    }
                    ReleaseDC(windowHandle, dc);

                    // 所有檢查通過
                    LogService.Instance.Info("[WindowService] 視窗就緒檢查通過 (嘗試 {0}/{1}): 視窗 {2}, 客戶區 {3}x{4}",
                        attempt, maxRetries, windowHandle.ToInt64(), width, height);
                    return true;
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warning("[WindowService] 嘗試 {0}/{1} 檢查時發生錯誤: {2}",
                        attempt, maxRetries, ex.Message);

                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(retryDelayMs);
                    }
                }
            }

            LogService.Instance.Error("[WindowService] 視窗就緒檢查失敗: 已達最大重試次數 {0}", maxRetries);
            return false;
        }

        /// <summary>
        /// 等待視窗就緒(帶超時)
        /// </summary>
        /// <param name="windowHandle">視窗句柄</param>
        /// <param name="timeoutMs">超時時間(毫秒)</param>
        /// <param name="checkIntervalMs">檢查間隔(毫秒)</param>
        /// <returns>視窗是否就緒</returns>
        public bool WaitForWindowReady(IntPtr windowHandle, int timeoutMs = 5000, int checkIntervalMs = 300)
        {
            LogService.Instance.Info("[WindowService] 開始等待視窗就緒: {0}, 超時: {1}ms",
                windowHandle.ToInt64(), timeoutMs);

            var startTime = Environment.TickCount;
            int maxRetries = Math.Max(1, timeoutMs / checkIntervalMs);

            bool isReady = IsWindowReady(windowHandle, maxRetries, checkIntervalMs);

            var elapsedMs = Environment.TickCount - startTime;
            if (isReady)
            {
                LogService.Instance.Info("[WindowService] 視窗就緒完成: 耗時 {0}ms", elapsedMs);
            }
            else
            {
                LogService.Instance.Warning("[WindowService] 視窗就緒超時: 耗時 {0}ms", elapsedMs);
            }

            return isReady;
        }

        /// <summary>
        /// 確保視窗獲得焦點並就緒
        /// </summary>
        /// <param name="windowHandle">視窗句柄</param>
        /// <param name="targetProcessId">目標進程ID(用於驗證)</param>
        /// <param name="focusDelayMs">設置焦點後的等待時間</param>
        /// <returns>操作是否成功</returns>
        public bool EnsureWindowFocusedAndReady(IntPtr windowHandle, int targetProcessId = 0, int focusDelayMs = 300, int maxFocusRetries = 3)
        {
            try
            {
                LogService.Instance.Info("[WindowService] 確保視窗焦點和就緒: 視窗 {0}, PID {1}",
                    windowHandle.ToInt64(), targetProcessId);

                // 1. 檢查視窗是否已經是前台視窗,如果不是則嘗試獲取焦點(支持重試)
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != windowHandle)
                {
                    LogService.Instance.Debug("[WindowService] 當前前台視窗: {0}, 目標視窗: {1}, 需要切換焦點",
                        foregroundWindow.ToInt64(), windowHandle.ToInt64());

                    // 多次嘗試設置焦點
                    bool focusSuccess = false;
                    for (int retry = 1; retry <= maxFocusRetries; retry++)
                    {
                        LogService.Instance.Debug("[WindowService] 嘗試設置焦點 (第 {0}/{1} 次)", retry, maxFocusRetries);

                        // 設置為前台視窗
                        if (!SetForegroundWindow(windowHandle))
                        {
                            LogService.Instance.Warning("[WindowService] SetForegroundWindow API 調用失敗 (第 {0} 次)", retry);
                            if (retry < maxFocusRetries)
                            {
                                Thread.Sleep(focusDelayMs);
                                continue;
                            }
                            else
                            {
                                return false;
                            }
                        }

                        // 等待焦點切換生效
                        Thread.Sleep(focusDelayMs);

                        // 驗證焦點切換成功
                        foregroundWindow = GetForegroundWindow();
                        if (foregroundWindow == windowHandle)
                        {
                            LogService.Instance.Info("[WindowService] 焦點切換成功 (第 {0} 次嘗試)", retry);
                            focusSuccess = true;
                            break;
                        }
                        else
                        {
                            LogService.Instance.Warning("[WindowService] 焦點切換失敗 (第 {0}/{1} 次): 當前前台 {2}, 目標 {3}",
                                retry, maxFocusRetries, foregroundWindow.ToInt64(), windowHandle.ToInt64());

                            // 如果不是最後一次重試,等待後再試
                            if (retry < maxFocusRetries)
                            {
                                Thread.Sleep(focusDelayMs);
                            }
                        }
                    }

                    // 所有重試都失敗
                    if (!focusSuccess)
                    {
                        LogService.Instance.Error("[WindowService] 經過 {0} 次嘗試後仍無法獲取焦點", maxFocusRetries);
                        return false;
                    }
                }
                else
                {
                    LogService.Instance.Debug("[WindowService] 視窗已經是前台視窗,無需切換焦點");
                }

                // 2. 如果指定了進程ID,驗證視窗屬於該進程
                if (targetProcessId > 0)
                {
                    uint windowProcessId;
                    GetWindowThreadProcessId(windowHandle, out windowProcessId);

                    if ((int)windowProcessId != targetProcessId)
                    {
                        LogService.Instance.Warning("[WindowService] 進程ID不匹配: 視窗PID {0}, 目標PID {1}",
                            windowProcessId, targetProcessId);
                        return false;
                    }
                }

                // 3. 檢查視窗就緒狀態
                if (!IsWindowReady(windowHandle, maxRetries: 5, retryDelayMs: 200))
                {
                    LogService.Instance.Warning("[WindowService] 視窗未就緒");
                    return false;
                }

                LogService.Instance.Info("[WindowService] 視窗焦點和就緒檢查通過");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("[WindowService] 確保視窗焦點和就緒時發生錯誤: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 檢查視窗是否可以接收鍵盤輸入
        /// </summary>
        public bool CanReceiveKeyboardInput(IntPtr windowHandle)
        {
            try
            {
                // 檢查視窗是否為前台視窗
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow != windowHandle)
                {
                    LogService.Instance.Debug("[WindowService] 視窗不是前台視窗,無法接收鍵盤輸入");
                    return false;
                }

                // 檢查視窗是否可見和就緒
                if (!IsWindowVisible(windowHandle) || !IsWindowReady(windowHandle, maxRetries: 3, retryDelayMs: 100))
                {
                    LogService.Instance.Debug("[WindowService] 視窗不可見或未就緒,無法接收鍵盤輸入");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warning("[WindowService] 檢查鍵盤輸入能力時發生錯誤: {0}", ex.Message);
                return false;
            }
        }

        #endregion
    }
}
