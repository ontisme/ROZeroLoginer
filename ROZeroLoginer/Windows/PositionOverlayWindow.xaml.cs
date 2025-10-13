using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ROZeroLoginer.Models;
using ROZeroLoginer.Services;

namespace ROZeroLoginer.Windows
{
    public partial class PositionOverlayWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private AppSettings _settings;
        private System.Windows.Threading.DispatcherTimer _updateTimer;
        private int _capturedX = 0;
        private int _capturedY = 0;
        private string _capturedWindowTitle = "";
        private string _capturedWindowSize = "";
        private bool _positionCaptured = false;
        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelMouseProc _proc;
        private IntPtr _overlayHandle;
        private bool _isProcessingClick = false;

        public int CapturedX => _capturedX;
        public int CapturedY => _capturedY;
        public string CapturedWindowTitle => _capturedWindowTitle;
        public bool PositionCaptured => _positionCaptured;

        public PositionOverlayWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            _proc = HookCallback;

            // 設置視窗覆蓋整個螢幕
            this.Left = 0;
            this.Top = 0;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;

            // 監聽視窗載入事件
            this.Loaded += PositionOverlayWindow_Loaded;

            // 啟動定時器更新滑鼠位置
            _updateTimer = new System.Windows.Threading.DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            // 監聽 Esc 鍵
            this.PreviewKeyDown += PositionOverlayWindow_PreviewKeyDown;

            LogService.Instance.Info("位置覆蓋層已開啟");
        }

        private void PositionOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 獲取視窗句柄
            _overlayHandle = new WindowInteropHelper(this).Handle;

            // 設置視窗為點擊穿透
            int exStyle = GetWindowLong(_overlayHandle, GWL_EXSTYLE);
            SetWindowLong(_overlayHandle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);

            // 設置滑鼠鉤子
            _hookID = SetHook(_proc);

            LogService.Instance.Info("Overlay 視窗樣式已設置為穿透模式，鉤子已安裝");
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN && !_positionCaptured && !_isProcessingClick)
            {
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                POINT screenPoint = hookStruct.pt;

                // 使用 Dispatcher 在 UI 線程上處理
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    HandleMouseClick(screenPoint);
                }));

                // 返回 1 表示已處理，阻止事件傳遞
                return (IntPtr)1;
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void HandleMouseClick(POINT screenPoint)
        {
            // 防止重複處理
            if (_isProcessingClick)
            {
                return;
            }

            _isProcessingClick = true;

            try
            {
                // 獲取點擊位置的視窗（由於 Overlay 是穿透的，會直接獲取下層視窗）
                IntPtr hWnd = WindowFromPoint(screenPoint);

                if (hWnd == IntPtr.Zero || hWnd == _overlayHandle)
                {
                    LogService.Instance.Warning("Overlay 點擊沒有獲取到有效視窗");
                    return;
                }

                // 獲取視窗標題
                System.Text.StringBuilder title = new System.Text.StringBuilder(256);
                GetWindowText(hWnd, title, 256);
                string windowTitle = title.ToString();

                // 檢查是否為 RO 視窗
                bool isRoWindow = false;
                var gameTitles = _settings.GetEffectiveGameTitles();
                foreach (var gameTitle in gameTitles)
                {
                    if (windowTitle == gameTitle)
                    {
                        isRoWindow = true;
                        break;
                    }
                }

                if (isRoWindow)
                {
                    // 轉換為客戶區座標
                    POINT clientPoint = screenPoint;
                    ScreenToClient(hWnd, ref clientPoint);

                    // 獲取客戶區大小以驗證座標
                    GetClientRect(hWnd, out RECT clientRect);
                    int clientWidth = clientRect.Right - clientRect.Left;
                    int clientHeight = clientRect.Bottom - clientRect.Top;

                    // 檢查座標是否在客戶區內
                    if (clientPoint.x >= 0 && clientPoint.x < clientWidth &&
                        clientPoint.y >= 0 && clientPoint.y < clientHeight)
                    {
                        _capturedX = clientPoint.x;
                        _capturedY = clientPoint.y;
                        _capturedWindowTitle = windowTitle;
                        _capturedWindowSize = $"{clientWidth}x{clientHeight}";
                        _positionCaptured = true;

                        LogService.Instance.Info("Overlay 成功捕獲位置: ({0}, {1}) 在視窗 '{2}'",
                            _capturedX, _capturedY, _capturedWindowTitle);

                        // 立即關閉並返回結果
                        CloseOverlay(true);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"點擊位置超出遊戲視窗客戶區範圍\n\n" +
                            $"點擊座標: ({clientPoint.x}, {clientPoint.y})\n" +
                            $"視窗大小: {clientWidth}x{clientHeight}\n\n" +
                            $"請確保點擊在遊戲視窗內部，而不是標題欄或邊框。",
                            "座標超出範圍",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        LogService.Instance.Warning("Overlay 點擊位置超出客戶區: ({0}, {1}), 客戶區大小: {2}x{3}",
                            clientPoint.x, clientPoint.y, clientWidth, clientHeight);
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"點擊的不是 RO 遊戲視窗\n\n" +
                        $"視窗標題: {windowTitle}\n\n" +
                        $"請確保點擊在 RO 遊戲視窗上。\n" +
                        $"如果遊戲標題不同，請先在設定中添加正確的遊戲標題。",
                        "錯誤視窗",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    LogService.Instance.Warning("Overlay 點擊的不是 RO 視窗: {0}", windowTitle);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "Overlay 處理點擊時發生錯誤");
                MessageBox.Show($"處理點擊時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 重置處理標誌，允許下次點擊（除非已經捕獲成功）
                if (!_positionCaptured)
                {
                    _isProcessingClick = false;
                }
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // 獲取滑鼠位置
            if (GetCursorPos(out POINT cursorPos))
            {
                // 更新當前位置顯示
                CurrentPositionTextBlock.Text = $"X: {cursorPos.x}, Y: {cursorPos.y}";

                // 更新十字線位置
                Canvas.SetLeft(VerticalLine, cursorPos.x - this.Left);
                Canvas.SetTop(HorizontalLine, cursorPos.y - this.Top);
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            // 滑鼠移動時由定時器處理
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 由於視窗是穿透的，這個事件不會被觸發
            // 點擊由鉤子處理
        }

        private void PositionOverlayWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                LogService.Instance.Info("用戶按下 Esc 取消捕獲");
                CloseOverlay(false);
            }
        }

        private void CloseOverlay(bool success)
        {
            try
            {
                // 解除鉤子
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }

                // 停止定時器
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    _updateTimer.Tick -= UpdateTimer_Tick;
                    _updateTimer = null;
                }

                // 檢查是否在 UI 線程
                if (Dispatcher.CheckAccess())
                {
                    // 已經在 UI 線程，直接執行
                    try
                    {
                        DialogResult = success;
                        Close();
                    }
                    catch (InvalidOperationException ex)
                    {
                        // 如果無法設置 DialogResult，直接關閉
                        LogService.Instance.Warning("無法設置 DialogResult: {0}", ex.Message);
                        Close();
                    }
                }
                else
                {
                    // 不在 UI 線程，使用 Invoke
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            DialogResult = success;
                            Close();
                        }
                        catch (InvalidOperationException ex)
                        {
                            LogService.Instance.Warning("無法設置 DialogResult: {0}", ex.Message);
                            Close();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "關閉 Overlay 時發生錯誤");

                // 確保視窗關閉
                try
                {
                    if (Dispatcher.CheckAccess())
                    {
                        Close();
                    }
                    else
                    {
                        Dispatcher.Invoke(() => Close());
                    }
                }
                catch { }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 確保鉤子已解除
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }

                // 確保定時器已停止
                if (_updateTimer != null)
                {
                    _updateTimer.Stop();
                    _updateTimer.Tick -= UpdateTimer_Tick;
                    _updateTimer = null;
                }

                LogService.Instance.Info("位置覆蓋層已關閉");
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "OnClosed 發生錯誤");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
