using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using ROZeroLoginer.Models;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace ROZeroLoginer.Windows
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            
            _settings = new AppSettings
            {
                Hotkey = settings.Hotkey,
                HotkeyEnabled = settings.HotkeyEnabled,
                StartWithWindows = settings.StartWithWindows,
                ShowNotifications = settings.ShowNotifications,
                OtpValiditySeconds = settings.OtpValiditySeconds
            };
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            HotkeyEnabledCheckBox.IsChecked = _settings.HotkeyEnabled;
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            ShowNotificationsCheckBox.IsChecked = _settings.ShowNotifications;
            OtpValidityTextBox.Text = _settings.OtpValiditySeconds.ToString();
            
            // 設定熱鍵下拉選單
            var hotkeyName = _settings.Hotkey.ToString();
            var selectedItem = HotkeyComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == hotkeyName);
            
            if (selectedItem != null)
            {
                HotkeyComboBox.SelectedItem = selectedItem;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                SaveSettings();
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            if (!int.TryParse(OtpValidityTextBox.Text, out int otpValidity) || otpValidity < 15 || otpValidity > 120)
            {
                MessageBox.Show("TOTP 有效期必須介於 15 到 120 秒之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                OtpValidityTextBox.Focus();
                return false;
            }

            return true;
        }

        private void SaveSettings()
        {
            _settings.HotkeyEnabled = HotkeyEnabledCheckBox.IsChecked == true;
            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            _settings.ShowNotifications = ShowNotificationsCheckBox.IsChecked == true;
            _settings.OtpValiditySeconds = int.Parse(OtpValidityTextBox.Text);
            
            // 設定熱鍵
            var selectedItem = HotkeyComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                var hotkeyName = selectedItem.Tag.ToString();
                if (Enum.TryParse<Keys>(hotkeyName, out Keys hotkey))
                {
                    _settings.Hotkey = hotkey;
                }
            }
            
            // 設定開機啟動
            SetStartupRegistry(_settings.StartWithWindows);
        }

        private void SetStartupRegistry(bool enabled)
        {
            try
            {
                var registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                var appName = "ROZeroLoginer";
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                
                if (enabled)
                {
                    registryKey?.SetValue(appName, exePath);
                }
                else
                {
                    registryKey?.DeleteValue(appName, false);
                }
                
                registryKey?.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定開機啟動時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BackupButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "備份檔案 (*.backup)|*.backup",
                    FileName = $"ROZeroLoginer_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.backup"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ROZeroLoginer");
                    var accountsFile = Path.Combine(appDataPath, "accounts.dat");
                    var settingsFile = Path.Combine(appDataPath, "settings.json");
                    
                    var backupData = new
                    {
                        AccountsData = File.Exists(accountsFile) ? Convert.ToBase64String(File.ReadAllBytes(accountsFile)) : "",
                        SettingsData = File.Exists(settingsFile) ? File.ReadAllText(settingsFile) : "",
                        BackupDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                    
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(backupData, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    
                    MessageBox.Show("備份成功！", "備份", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"備份失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "備份檔案 (*.backup)|*.backup"
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    var result = MessageBox.Show("還原資料會覆蓋現有的帳號資料，確定要繼續嗎？", 
                        "確認還原", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        var json = File.ReadAllText(openFileDialog.FileName);
                        var backupData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                        
                        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ROZeroLoginer");
                        Directory.CreateDirectory(appDataPath);
                        
                        var accountsFile = Path.Combine(appDataPath, "accounts.dat");
                        var settingsFile = Path.Combine(appDataPath, "settings.json");
                        
                        if (!string.IsNullOrEmpty((string)backupData.AccountsData))
                        {
                            var accountsBytes = Convert.FromBase64String((string)backupData.AccountsData);
                            File.WriteAllBytes(accountsFile, accountsBytes);
                        }
                        
                        if (!string.IsNullOrEmpty((string)backupData.SettingsData))
                        {
                            File.WriteAllText(settingsFile, (string)backupData.SettingsData);
                        }
                        
                        MessageBox.Show("還原成功！請重新啟動應用程式。", "還原", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"還原失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}