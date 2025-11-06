using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using ROZeroLoginer.Services;

namespace ROZeroLoginer.Windows
{
    public partial class AgreeButtonPositionWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private int _capturedX = 0;
        private int _capturedY = 0;
        private string _capturedWindowTitle = "";
        private IntPtr _capturedWindowHandle = IntPtr.Zero;
        private Models.AppSettings _settings;
        private Window _mainWindow;
        private Window _settingsWindow;
        private WindowState _originalMainWindowState;
        private WindowState _originalSettingsWindowState;

        public int CapturedX => _capturedX;
        public int CapturedY => _capturedY;
        public bool PositionCaptured { get; private set; } = false;

        public AgreeButtonPositionWindow(Models.AppSettings settings, Window mainWindow = null, Window settingsWindow = null)
        {
            InitializeComponent();
            _settings = settings;
            _mainWindow = mainWindow;
            _settingsWindow = settingsWindow;

            LogService.Instance.Info("同意按鈕位置設定視窗已開啟");

            // 視窗載入時最小化其他視窗
            this.Loaded += AgreeButtonPositionWindow_Loaded;
        }

        private void AgreeButtonPositionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存並最小化主視窗
                if (_mainWindow != null)
                {
                    _originalMainWindowState = _mainWindow.WindowState;
                    _mainWindow.WindowState = WindowState.Minimized;
                    LogService.Instance.Debug("[AgreeButtonPosition] 主視窗已最小化");
                }

                // 保存並最小化設定視窗
                if (_settingsWindow != null)
                {
                    _originalSettingsWindowState = _settingsWindow.WindowState;
                    _settingsWindow.WindowState = WindowState.Minimized;
                    LogService.Instance.Debug("[AgreeButtonPosition] 設定視窗已最小化");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warning("[AgreeButtonPosition] 最小化視窗時發生錯誤: {0}", ex.Message);
            }
        }

        private void StartCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            PositionOverlayWindow overlayWindow = null;
            try
            {
                // 隱藏當前視窗
                this.Hide();

                // 稍微延遲以確保視窗完全隱藏
                Thread.Sleep(100);

                // 顯示 Overlay
                overlayWindow = new PositionOverlayWindow(_settings);
                bool? result = overlayWindow.ShowDialog();

                // 顯示回當前視窗
                this.Show();
                this.Activate();

                if (result == true && overlayWindow.PositionCaptured)
                {
                    _capturedX = overlayWindow.CapturedX;
                    _capturedY = overlayWindow.CapturedY;
                    _capturedWindowTitle = overlayWindow.CapturedWindowTitle;
                    PositionCaptured = true;

                    // 嘗試獲取視窗句柄
                    _capturedWindowHandle = FindWindow(null, _capturedWindowTitle);

                    StatusTextBlock.Text = "位置捕獲成功！";
                    PositionTextBlock.Text = $"位置: X={_capturedX}, Y={_capturedY}";
                    WindowTitleTextBlock.Text = $"視窗: {_capturedWindowTitle}";
                    OkButton.IsEnabled = true;
                    TestPositionButton.IsEnabled = true;

                    LogService.Instance.Info("成功從 Overlay 捕獲位置: ({0}, {1})", _capturedX, _capturedY);
                }
                else
                {
                    StatusTextBlock.Text = "捕獲已取消";
                    LogService.Instance.Info("用戶取消了位置捕獲");
                }
            }
            catch (Exception ex)
            {
                // 確保視窗顯示回來
                try
                {
                    this.Show();
                    this.Activate();
                }
                catch { }

                StatusTextBlock.Text = "捕獲時發生錯誤";
                LogService.Instance.Error(ex, "開始捕獲時發生錯誤");
                MessageBox.Show($"捕獲位置時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 確保 Overlay 被正確釋放
                if (overlayWindow != null)
                {
                    try
                    {
                        if (overlayWindow.IsLoaded)
                        {
                            overlayWindow.Close();
                        }
                    }
                    catch { }
                }
            }
        }

        private void TestPositionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!PositionCaptured)
            {
                MessageBox.Show("尚未捕獲位置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                PerformTestClick();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"測試點擊失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                LogService.Instance.Error(ex, "測試點擊失敗");
            }
        }

        private void PerformTestClick()
        {
            // 查找目標視窗
            IntPtr targetWindow = IntPtr.Zero;

            // 嘗試使用保存的句柄
            if (_capturedWindowHandle != IntPtr.Zero)
            {
                targetWindow = _capturedWindowHandle;
            }
            else
            {
                // 查找視窗
                targetWindow = FindWindow(null, _capturedWindowTitle);
            }

            if (targetWindow == IntPtr.Zero)
            {
                throw new Exception($"找不到視窗: {_capturedWindowTitle}");
            }

            LogService.Instance.Info("[TestClick] 找到目標視窗: {0}", targetWindow.ToInt64());

            // 將視窗置於前台
            SetForegroundWindow(targetWindow);
            Thread.Sleep(200);

            // 使用 InputService 執行點擊
            var inputService = new InputService(_settings);
            inputService.ClickUsingMethod1(targetWindow, _capturedX, _capturedY);

            LogService.Instance.Info("[TestClick] 已執行測試點擊: ({0}, {1})", _capturedX, _capturedY);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!PositionCaptured)
                {
                    MessageBox.Show("尚未捕獲位置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                LogService.Instance.Info("用戶確認位置: ({0}, {1})", _capturedX, _capturedY);

                try
                {
                    DialogResult = true;
                    Close();
                }
                catch (InvalidOperationException)
                {
                    // 無法設置 DialogResult，直接關閉
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "確認位置時發生錯誤");
                MessageBox.Show($"發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogService.Instance.Info("用戶點擊取消按鈕");

                try
                {
                    DialogResult = false;
                    Close();
                }
                catch (InvalidOperationException)
                {
                    // 無法設置 DialogResult，直接關閉
                    Close();
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(ex, "關閉視窗時發生錯誤");
                // 即使發生錯誤也要嘗試關閉
                try
                {
                    Close();
                }
                catch
                {
                    // 忽略二次錯誤
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 恢復主視窗狀態
                if (_mainWindow != null)
                {
                    _mainWindow.WindowState = _originalMainWindowState;
                    _mainWindow.Activate();
                    LogService.Instance.Debug("[AgreeButtonPosition] 主視窗已恢復");
                }

                // 恢復設定視窗狀態
                if (_settingsWindow != null)
                {
                    _settingsWindow.WindowState = _originalSettingsWindowState;
                    _settingsWindow.Activate();
                    LogService.Instance.Debug("[AgreeButtonPosition] 設定視窗已恢復");
                }

                LogService.Instance.Info("同意按鈕位置設定視窗已關閉");
            }
            catch (Exception ex)
            {
                // 記錄錯誤但不拋出
                LogService.Instance.Warning("[AgreeButtonPosition] OnClosed 發生錯誤: {0}", ex.Message);
                System.Diagnostics.Debug.WriteLine($"OnClosed error: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
