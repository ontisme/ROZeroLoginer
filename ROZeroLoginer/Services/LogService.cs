using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ROZeroLoginer.Services
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public class LogService
    {
        private static readonly Lazy<LogService> _instance = new Lazy<LogService>(() => new LogService());
        public static LogService Instance => _instance.Value;

        private readonly string _logDirectory;
        private readonly string _currentLogFile;
        private readonly object _lockObject = new object();

        // 事件通知日誌更新
        public event EventHandler LogUpdated;

        private LogService()
        {
            // 將日誌存放在配置目錄下
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ROZeroLoginer");
            _logDirectory = Path.Combine(appDataPath, "Logs");
            
            // 確保日誌目錄存在
            Directory.CreateDirectory(_logDirectory);
            
            // 當前日誌文件名 - 使用時間戳確保每次啟動都是新文件
            _currentLogFile = Path.Combine(_logDirectory, $"ROZeroLoginer_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            
            // 創建新的日誌文件（如果文件已存在會被覆蓋）
            InitializeNewLogFile();
            
            // 清理舊的日誌文件（保留最近30天）
            CleanupOldLogs();
        }

        public string LogDirectory => _logDirectory;
        public string CurrentLogFile => _currentLogFile;

        private void InitializeNewLogFile()
        {
            try
            {
                // 創建新日誌文件，如果存在則覆蓋
                var header = $"=== ROZero Loginer 日誌文件 - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===";
                lock (_lockObject)
                {
                    File.WriteAllText(_currentLogFile, header + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化日誌文件失敗: {ex.Message}");
            }
        }

        public void Debug(string message, params object[] args)
        {
            WriteLog(LogLevel.Debug, message, args);
        }

        public void Info(string message, params object[] args)
        {
            WriteLog(LogLevel.Info, message, args);
        }

        public void Warning(string message, params object[] args)
        {
            WriteLog(LogLevel.Warning, message, args);
        }

        public void Error(string message, params object[] args)
        {
            WriteLog(LogLevel.Error, message, args);
        }

        public void Error(Exception ex, string message = null, params object[] args)
        {
            var logMessage = string.IsNullOrEmpty(message) 
                ? $"異常: {ex.Message}\n堆疊追蹤: {ex.StackTrace}" 
                : $"{string.Format(message, args)}\n異常: {ex.Message}\n堆疊追蹤: {ex.StackTrace}";
            WriteLog(LogLevel.Error, logMessage);
        }

        private void WriteLog(LogLevel level, string message, params object[] args)
        {
            try
            {
                var formattedMessage = args?.Length > 0 ? string.Format(message, args) : message;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var threadId = Thread.CurrentThread.ManagedThreadId;
                var logEntry = $"[{timestamp}] [{level}] [T{threadId:D2}] {formattedMessage}";

                lock (_lockObject)
                {
                    File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);
                }

                // 同時輸出到 Debug 控制台
                System.Diagnostics.Debug.WriteLine(logEntry);

                // 觸發日誌更新事件
                LogUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                // 如果日誌系統本身出錯，只輸出到 Debug 控制台
                System.Diagnostics.Debug.WriteLine($"日誌系統錯誤: {ex.Message}");
            }
        }

        private void CleanupOldLogs()
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-7); // 保留最近7天的日誌
                var logFiles = Directory.GetFiles(_logDirectory, "ROZeroLoginer_*.log");
                
                foreach (var file in logFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // 新格式: ROZeroLoginer_yyyyMMdd_HHmmss
                    // 舊格式: ROZeroLoginer_yyyyMMdd
                    if (fileName.StartsWith("ROZeroLoginer_"))
                    {
                        var datePart = fileName.Substring("ROZeroLoginer_".Length);
                        DateTime fileDate = DateTime.MinValue;
                        bool validDate = false;
                        
                        // 嘗試新格式 (yyyyMMdd_HHmmss)
                        if (datePart.Length >= 8)
                        {
                            var dateString = datePart.Substring(0, 8);
                            validDate = DateTime.TryParseExact(dateString, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out fileDate);
                        }
                        
                        if (validDate && fileDate < cutoffDate)
                        {
                            File.Delete(file);
                            System.Diagnostics.Debug.WriteLine($"刪除舊日誌: {Path.GetFileName(file)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理舊日誌失敗: {ex.Message}");
            }
        }

        public void ClearCurrentLog()
        {
            try
            {
                lock (_lockObject)
                {
                    // 重新初始化日誌文件，清除所有內容
                    var header = $"=== ROZero Loginer 日誌文件 - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===";
                    File.WriteAllText(_currentLogFile, header + Environment.NewLine);
                }
                
                // 記錄清空操作
                Info("日誌已清空");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清空日誌失敗: {ex.Message}");
            }
        }

        public string GetLogContent(int maxLines = 1000)
        {
            try
            {
                if (!File.Exists(_currentLogFile))
                    return "日誌文件不存在";

                var lines = File.ReadAllLines(_currentLogFile);
                var startIndex = Math.Max(0, lines.Length - maxLines);
                var relevantLines = new string[lines.Length - startIndex];
                Array.Copy(lines, startIndex, relevantLines, 0, relevantLines.Length);
                
                return string.Join(Environment.NewLine, relevantLines);
            }
            catch (Exception ex)
            {
                return $"讀取日誌失敗: {ex.Message}";
            }
        }

        public List<string> GetRecentLogs(int maxLines = 50)
        {
            try
            {
                if (!File.Exists(_currentLogFile))
                    return new List<string> { "日誌文件不存在" };

                var lines = File.ReadAllLines(_currentLogFile);
                var startIndex = Math.Max(0, lines.Length - maxLines);
                var recentLines = new List<string>();
                for (int i = startIndex; i < lines.Length; i++)
                {
                    recentLines.Add(lines[i]);
                }
                return recentLines;
            }
            catch (Exception ex)
            {
                return new List<string> { $"讀取日誌失敗: {ex.Message}" };
            }
        }
    }
}