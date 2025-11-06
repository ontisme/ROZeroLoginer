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
            UpdateLogStatistics();
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
            UpdateLogStatistics();
        }

        private void CleanupOldLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var stats = LogService.Instance.GetLogStatistics();
                var message = $"即將清理超過 3 天的舊日誌文件\n\n" +
                             $"當前日誌統計:\n" +
                             $"• 總文件數: {stats.TotalFiles}\n" +
                             $"• 總大小: {FormatBytes(stats.TotalSizeBytes)}\n" +
                             $"• 最舊日誌: {(stats.OldestDate.HasValue ? stats.OldestDate.Value.ToString("yyyy-MM-dd") : "無")}\n" +
                             $"• 最新日誌: {(stats.NewestDate.HasValue ? stats.NewestDate.Value.ToString("yyyy-MM-dd") : "無")}\n\n" +
                             $"確定要清理嗎？";

                var result = MessageBox.Show(message,
                    "確認清理舊日誌", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    int deletedCount = LogService.Instance.CleanupOldLogs(3);

                    MessageBox.Show($"清理完成！\n已刪除 {deletedCount} 個舊日誌文件。",
                        "清理成功", MessageBoxButton.OK, MessageBoxImage.Information);

                    UpdateLogStatistics();
                    StatusTextBlock.Text = $"已清理 {deletedCount} 個舊日誌文件";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清理舊日誌失敗: {ex.Message}", "錯誤",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateLogStatistics()
        {
            try
            {
                var stats = LogService.Instance.GetLogStatistics();
                LogStatisticsTextBlock.Text = $"日誌統計: {stats.TotalFiles} 個文件 • " +
                                             $"總大小 {FormatBytes(stats.TotalSizeBytes)} • " +
                                             $"最舊 {(stats.OldestDate.HasValue ? stats.OldestDate.Value.ToString("yyyy-MM-dd") : "無")} • " +
                                             $"最新 {(stats.NewestDate.HasValue ? stats.NewestDate.Value.ToString("yyyy-MM-dd") : "無")}";
            }
            catch
            {
                LogStatisticsTextBlock.Text = "統計信息加載失敗";
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
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