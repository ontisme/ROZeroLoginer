using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ROZeroLoginer.Models;

namespace ROZeroLoginer.Services
{
    public class WindowValidationService
    {
        private readonly AppSettings _settings;

        public WindowValidationService(AppSettings settings = null)
        {
            _settings = settings ?? new AppSettings();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public bool IsRagnarokWindow()
        {
            try
            {
                // 獲取當前前台視窗
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return false;

                // 檢查視窗標題
                if (CheckWindowTitle(foregroundWindow))
                    return true;

                // 檢查進程名稱
                if (CheckProcessName(foregroundWindow))
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking Ragnarok window: {ex.Message}");
                return false;
            }
        }

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
                System.Diagnostics.Debug.WriteLine($"Error checking window title: {ex.Message}");
                return false;
            }
        }

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
                System.Diagnostics.Debug.WriteLine($"Error checking process name: {ex.Message}");
                return false;
            }
        }

        public string GetCurrentWindowInfo()
        {
            try
            {
                IntPtr foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return "無法獲取當前視窗";

                // 獲取視窗標題
                int length = GetWindowTextLength(foregroundWindow);
                StringBuilder windowTitle = new StringBuilder(length + 1);
                GetWindowText(foregroundWindow, windowTitle, windowTitle.Capacity);

                // 獲取進程名稱
                uint processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);
                string processName = "Unknown";
                
                if (processId != 0)
                {
                    try
                    {
                        Process process = Process.GetProcessById((int)processId);
                        processName = process.ProcessName;
                    }
                    catch { }
                }

                return $"視窗標題: {windowTitle}\n進程: {processName}";
            }
            catch (Exception ex)
            {
                return $"錯誤: {ex.Message}";
            }
        }
    }
}