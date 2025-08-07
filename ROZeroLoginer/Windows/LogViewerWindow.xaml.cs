using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ROZeroLoginer.Services;

namespace ROZeroLoginer.Windows
{
    public partial class LogViewerWindow : Window
    {
        private DispatcherTimer _refreshTimer;
        private int _maxLines = 1000;

        public LogViewerWindow()
        {
            InitializeComponent();
            InitializeTimer();
            LoadLogContent();
            UpdateLogFilePath();
        }

        private void InitializeTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (AutoRefreshCheckBox.IsChecked == true)
            {
                LoadLogContent();
            }
        }

        private void LoadLogContent()
        {
            try
            {
                var logContent = _maxLines > 0 
                    ? LogService.Instance.GetLogContent(_maxLines)
                    : LogService.Instance.GetLogContent(int.MaxValue);
                
                LogTextBox.Text = logContent;
                
                // 自動捲動到底部
                LogTextBox.ScrollToEnd();
                
                // 更新狀態
                var lineCount = logContent.Split('\n').Length;
                StatusTextBlock.Text = $"已載入 {lineCount} 行日誌 - 最後更新: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                LogTextBox.Text = $"載入日誌失敗: {ex.Message}";
                StatusTextBlock.Text = "載入日誌失敗";
            }
        }

        private void UpdateLogFilePath()
        {
            LogFilePathTextBlock.Text = LogService.Instance.CurrentLogFile;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadLogContent();
        }

        private void OpenLogDirButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LogService.Instance.LogDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法開啟日誌目錄: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("確定要清空當前日誌檔案嗎？此操作無法復原。", 
                    "確認清空日誌", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // 使用 LogService 的清空方法
                    LogService.Instance.ClearCurrentLog();
                    LoadLogContent();
                    StatusTextBlock.Text = "日誌已清空";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清空日誌失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LineCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LineCountComboBox.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag.ToString(), out int maxLines))
                {
                    _maxLines = maxLines;
                    LoadLogContent();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _refreshTimer?.Stop();
            base.OnClosed(e);
        }
    }
}