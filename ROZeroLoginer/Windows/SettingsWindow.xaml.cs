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
        public bool DataRestored { get; private set; } = false;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            
            _settings = new AppSettings
            {
                Hotkey = settings.Hotkey,
                HotkeyEnabled = settings.HotkeyEnabled,
                StartWithWindows = settings.StartWithWindows,
                ShowNotifications = settings.ShowNotifications,
                OtpValiditySeconds = settings.OtpValiditySeconds,
                OtpInputDelayMs = settings.OtpInputDelayMs,
                PrivacyModeEnabled = settings.PrivacyModeEnabled,
                HideNames = settings.HideNames,
                HideUsernames = settings.HideUsernames,
                HidePasswords = settings.HidePasswords,
                HideSecretKeys = settings.HideSecretKeys,
                RoGamePath = settings.RoGamePath,
                GameTitles = new System.Collections.Generic.List<string>(settings.GameTitles),
                CharacterSelectionDelayMs = settings.CharacterSelectionDelayMs,
                ServerSelectionDelayMs = settings.ServerSelectionDelayMs,
                KeyboardInputDelayMs = settings.KeyboardInputDelayMs,
                MouseClickDelayMs = settings.MouseClickDelayMs,
                GeneralOperationDelayMs = settings.GeneralOperationDelayMs,
                MinimizeToTray = settings.MinimizeToTray,
                WindowWidth = settings.WindowWidth,
                WindowHeight = settings.WindowHeight,
                WindowLeft = settings.WindowLeft,
                WindowTop = settings.WindowTop,
                WindowMaximized = settings.WindowMaximized
            };
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            HotkeyEnabledCheckBox.IsChecked = _settings.HotkeyEnabled;
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            ShowNotificationsCheckBox.IsChecked = _settings.ShowNotifications;
            MinimizeToTrayCheckBox.IsChecked = _settings.MinimizeToTray;
            OtpValidityTextBox.Text = _settings.OtpValiditySeconds.ToString();
            OtpDelayTextBox.Text = _settings.OtpInputDelayMs.ToString();
            
            // 隱私模式設定
            PrivacyModeEnabledCheckBox.IsChecked = _settings.PrivacyModeEnabled;
            HideNamesCheckBox.IsChecked = _settings.HideNames;
            HideUsernamesCheckBox.IsChecked = _settings.HideUsernames;
            HidePasswordsCheckBox.IsChecked = _settings.HidePasswords;
            HideSecretKeysCheckBox.IsChecked = _settings.HideSecretKeys;
            RoGamePathTextBox.Text = _settings.RoGamePath;
            
            // 延遲設定
            CharacterSelectionDelayTextBox.Text = _settings.CharacterSelectionDelayMs.ToString();
            ServerSelectionDelayTextBox.Text = _settings.ServerSelectionDelayMs.ToString();
            KeyboardInputDelayTextBox.Text = _settings.KeyboardInputDelayMs.ToString();
            MouseClickDelayTextBox.Text = _settings.MouseClickDelayMs.ToString();
            GeneralOperationDelayTextBox.Text = _settings.GeneralOperationDelayMs.ToString();
            
            LoadGameTitles();
            
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

            if (!int.TryParse(OtpDelayTextBox.Text, out int otpDelay) || otpDelay < 100 || otpDelay > 10000)
            {
                MessageBox.Show("OTP 輸入延遲必須介於 100 到 10000 毫秒之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                OtpDelayTextBox.Focus();
                return false;
            }

            // 延遲設定驗證
            if (!int.TryParse(CharacterSelectionDelayTextBox.Text, out int characterDelay) || characterDelay < 10 || characterDelay > 2000)
            {
                MessageBox.Show("角色選擇延遲必須介於 10 到 2000 毫秒之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                CharacterSelectionDelayTextBox.Focus();
                return false;
            }

            if (!int.TryParse(ServerSelectionDelayTextBox.Text, out int serverDelay) || serverDelay < 10 || serverDelay > 1000)
            {
                MessageBox.Show("伺服器選擇延遲必須介於 10 到 1000 毫秒之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                ServerSelectionDelayTextBox.Focus();
                return false;
            }

            if (!int.TryParse(KeyboardInputDelayTextBox.Text, out int keyboardDelay) || keyboardDelay < 50 || keyboardDelay > 1000)
            {
                MessageBox.Show("鍵盤輸入延遲必須介於 50 到 1000 毫秒之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                KeyboardInputDelayTextBox.Focus();
                return false;
            }

            if (!int.TryParse(MouseClickDelayTextBox.Text, out int mouseDelay) || mouseDelay < 50 || mouseDelay > 2000)
            {
                MessageBox.Show("滑鼠點擊延遲必須介於 50 到 2000 毫秒之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                MouseClickDelayTextBox.Focus();
                return false;
            }

            if (!int.TryParse(GeneralOperationDelayTextBox.Text, out int generalDelay) || generalDelay < 100 || generalDelay > 5000)
            {
                MessageBox.Show("一般操作延遲必須介於 100 到 5000 毫秒之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                GeneralOperationDelayTextBox.Focus();
                return false;
            }

            return true;
        }

        private void SaveSettings()
        {
            _settings.HotkeyEnabled = HotkeyEnabledCheckBox.IsChecked == true;
            _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
            _settings.ShowNotifications = ShowNotificationsCheckBox.IsChecked == true;
            _settings.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
            _settings.OtpValiditySeconds = int.Parse(OtpValidityTextBox.Text);
            _settings.OtpInputDelayMs = int.Parse(OtpDelayTextBox.Text);
            
            // 隱私模式設定
            _settings.PrivacyModeEnabled = PrivacyModeEnabledCheckBox.IsChecked == true;
            _settings.HideNames = HideNamesCheckBox.IsChecked == true;
            _settings.HideUsernames = HideUsernamesCheckBox.IsChecked == true;
            _settings.HidePasswords = HidePasswordsCheckBox.IsChecked == true;
            _settings.HideSecretKeys = HideSecretKeysCheckBox.IsChecked == true;
            _settings.RoGamePath = RoGamePathTextBox.Text;
            
            // 延遲設定
            _settings.CharacterSelectionDelayMs = int.Parse(CharacterSelectionDelayTextBox.Text);
            _settings.ServerSelectionDelayMs = int.Parse(ServerSelectionDelayTextBox.Text);
            _settings.KeyboardInputDelayMs = int.Parse(KeyboardInputDelayTextBox.Text);
            _settings.MouseClickDelayMs = int.Parse(MouseClickDelayTextBox.Text);
            _settings.GeneralOperationDelayMs = int.Parse(GeneralOperationDelayTextBox.Text);
            
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
                    var keyFile = Path.Combine(appDataPath, "key.dat");
                    var settingsFile = Path.Combine(appDataPath, "settings.json");
                    
                    var backupData = new
                    {
                        AccountsData = File.Exists(accountsFile) ? Convert.ToBase64String(File.ReadAllBytes(accountsFile)) : "",
                        KeyData = File.Exists(keyFile) ? File.ReadAllText(keyFile) : "",
                        SettingsData = File.Exists(settingsFile) ? File.ReadAllText(settingsFile) : "",
                        BackupDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        Version = "1.3.0"
                    };
                    
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(backupData, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    
                    MessageBox.Show("備份成功！包含帳號資料、加密金鑰和設定檔。", "備份", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    var result = MessageBox.Show("還原資料會覆蓋現有的帳號資料和設定，確定要繼續嗎？", 
                        "確認還原", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        var json = File.ReadAllText(openFileDialog.FileName);
                        var backupData = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                        
                        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ROZeroLoginer");
                        Directory.CreateDirectory(appDataPath);
                        
                        var accountsFile = Path.Combine(appDataPath, "accounts.dat");
                        var keyFile = Path.Combine(appDataPath, "key.dat");
                        var settingsFile = Path.Combine(appDataPath, "settings.json");
                        
                        // 備份現有檔案
                        var backupSuffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        if (File.Exists(accountsFile))
                            File.Copy(accountsFile, accountsFile + "." + backupSuffix + ".old", true);
                        if (File.Exists(keyFile))
                            File.Copy(keyFile, keyFile + "." + backupSuffix + ".old", true);
                        if (File.Exists(settingsFile))
                            File.Copy(settingsFile, settingsFile + "." + backupSuffix + ".old", true);
                        
                        // 還原帳號資料
                        if (!string.IsNullOrEmpty((string)backupData.AccountsData))
                        {
                            var accountsBytes = Convert.FromBase64String((string)backupData.AccountsData);
                            File.WriteAllBytes(accountsFile, accountsBytes);
                        }
                        
                        // 還原加密金鑰（關鍵修復！）
                        if (!string.IsNullOrEmpty((string)backupData.KeyData))
                        {
                            File.WriteAllText(keyFile, (string)backupData.KeyData);
                        }
                        
                        // 還原設定檔
                        if (!string.IsNullOrEmpty((string)backupData.SettingsData))
                        {
                            File.WriteAllText(settingsFile, (string)backupData.SettingsData);
                        }
                        
                        DataRestored = true;
                        MessageBox.Show("還原成功！包含帳號資料、加密金鑰和設定檔。\n點擊確定後將立即重新載入資料。", "還原", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"還原失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseGamePathButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "執行檔 (*.exe)|*.exe|所有檔案 (*.*)|*.*",
                Title = "選擇 RO 主程式",
                FileName = "Ragexe.exe"
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                RoGamePathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void LoadGameTitles()
        {
            GameTitlesListBox.Items.Clear();
            foreach (var title in _settings.GameTitles)
            {
                GameTitlesListBox.Items.Add(title);
            }
        }

        private void AddGameTitleButton_Click(object sender, RoutedEventArgs e)
        {
            var newTitle = NewGameTitleTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(newTitle))
            {
                MessageBox.Show("請輸入遊戲標題", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                NewGameTitleTextBox.Focus();
                return;
            }

            if (_settings.GameTitles.Any(title => string.Equals(title, newTitle, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show("此遊戲標題已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                NewGameTitleTextBox.Focus();
                return;
            }

            _settings.GameTitles.Add(newTitle);
            GameTitlesListBox.Items.Add(newTitle);
            NewGameTitleTextBox.Text = "";
            NewGameTitleTextBox.Focus();
        }

        private void RemoveGameTitleButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedTitle = GameTitlesListBox.SelectedItem as string;
            if (selectedTitle == null)
            {
                MessageBox.Show("請選擇要移除的遊戲標題", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 允許移除所有標題，系統會在需要時自動生成預設標題

            _settings.GameTitles.Remove(selectedTitle);
            GameTitlesListBox.Items.Remove(selectedTitle);
        }
    }
}