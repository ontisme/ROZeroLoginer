using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ROZeroLoginer.Models;
using ROZeroLoginer.Services;
using ROZeroLoginer.Utils;

namespace ROZeroLoginer.Windows
{
    public partial class AccountWindow : Window
    {
        private readonly OtpService _otpService;
        private DispatcherTimer _previewTimer;
        private Account _account;

        public Account Account => _account;

        public AccountWindow(Account account = null)
        {
            InitializeComponent();
            
            _otpService = new OtpService();
            _account = account ?? new Account();
            
            InitializePreviewTimer();
            LoadGroups();
            LoadAccountData();
        }

        private void InitializePreviewTimer()
        {
            _previewTimer = new DispatcherTimer();
            _previewTimer.Interval = TimeSpan.FromSeconds(1);
            _previewTimer.Tick += PreviewTimer_Tick;
            _previewTimer.Start();
        }

        private void LoadGroups()
        {
            try
            {
                var dataService = new DataService();
                var allAccounts = dataService.GetAccounts();
                var existingGroups = allAccounts.Select(a => a.Group).Distinct().OrderBy(g => g).ToList();

                GroupComboBox.Items.Clear();
                
                // 添加預設分組
                if (!existingGroups.Contains("預設"))
                {
                    GroupComboBox.Items.Add("預設");
                }
                
                // 添加現有分組
                foreach (var group in existingGroups)
                {
                    if (!string.IsNullOrEmpty(group))
                    {
                        GroupComboBox.Items.Add(group);
                    }
                }
                
                // 設定預設值
                GroupComboBox.Text = "預設";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加載分組時發生錯誤: {ex.Message}");
                GroupComboBox.Items.Add("預設");
                GroupComboBox.Text = "預設";
            }
        }

        private void LoadAccountData()
        {
            if (_account != null)
            {
                NameTextBox.Text = _account.Name ?? "";
                UsernameTextBox.Text = _account.Username ?? "";
                PasswordBox.Password = _account.Password ?? "";
                OtpSecretTextBox.Text = _account.OtpSecret ?? "";
                GroupComboBox.Text = _account.Group ?? "預設";
                // 設定伺服器選項：0=遊戲預設位置, 1-4=第一到四個伺服器
                switch (_account.Server)
                {
                    case 0: ServerComboBox.SelectedIndex = 4; break; // 遊戲預設位置
                    case 1: ServerComboBox.SelectedIndex = 0; break; // 第一個
                    case 2: ServerComboBox.SelectedIndex = 1; break; // 第二個
                    case 3: ServerComboBox.SelectedIndex = 2; break; // 第三個
                    case 4: ServerComboBox.SelectedIndex = 3; break; // 第四個
                    default: ServerComboBox.SelectedIndex = 4; break; // 預設為遊戲預設位置
                }

                // 設定角色選項：0=遊戲預設位置, 1-15=角色1到15
                if (_account.Character == 0)
                {
                    CharacterComboBox.SelectedIndex = 15; // 遊戲預設位置
                }
                else if (_account.Character >= 1 && _account.Character <= 15)
                {
                    CharacterComboBox.SelectedIndex = _account.Character - 1; // 角色1-15
                }
                else
                {
                    CharacterComboBox.SelectedIndex = 15; // 預設為遊戲預設位置
                }
                
                AutoSelectServerCheckBox.IsChecked = _account.AutoSelectServer;
                AutoSelectCharacterCheckBox.IsChecked = _account.AutoSelectCharacter;
            }
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            UpdateTotpPreview();
        }

        private void UpdateTotpPreview()
        {
            if (!string.IsNullOrEmpty(OtpSecretTextBox.Text))
            {
                try
                {
                    var totp = _otpService.GenerateTotp(OtpSecretTextBox.Text);
                    var remaining = _otpService.GetTimeRemaining();
                    
                    PreviewTotpTextBox.Text = totp;
                    PreviewCountdownTextBlock.Text = $"({remaining}s)";
                }
                catch (Exception)
                {
                    PreviewTotpTextBox.Text = "錯誤";
                    PreviewCountdownTextBlock.Text = "";
                }
            }
            else
            {
                PreviewTotpTextBox.Text = "";
                PreviewCountdownTextBlock.Text = "";
            }
        }

        private void OtpSecretTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateTotpPreview();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                _account.Name = NameTextBox.Text.Trim();
                _account.Username = UsernameTextBox.Text.Trim();
                _account.Password = PasswordBox.Password;
                _account.OtpSecret = OtpSecretTextBox.Text.Trim();
                _account.Group = string.IsNullOrWhiteSpace(GroupComboBox.Text) ? "預設" : GroupComboBox.Text.Trim();
                // 保存伺服器選項：0=遊戲預設位置, 1-4=第一到四個伺服器
                switch (ServerComboBox.SelectedIndex)
                {
                    case 0: _account.Server = 1; break; // 第一個
                    case 1: _account.Server = 2; break; // 第二個
                    case 2: _account.Server = 3; break; // 第三個
                    case 3: _account.Server = 4; break; // 第四個
                    case 4: _account.Server = 0; break; // 遊戲預設位置
                    default: _account.Server = 0; break; // 預設為遊戲預設位置
                }

                // 保存角色選項：0=遊戲預設位置, 1-15=角色1到15
                if (CharacterComboBox.SelectedIndex == 15)
                {
                    _account.Character = 0; // 遊戲預設位置
                }
                else if (CharacterComboBox.SelectedIndex >= 0 && CharacterComboBox.SelectedIndex <= 14)
                {
                    _account.Character = CharacterComboBox.SelectedIndex + 1; // 角色1-15
                }
                else
                {
                    _account.Character = 0; // 預設為遊戲預設位置
                }
                
                _account.AutoSelectServer = AutoSelectServerCheckBox.IsChecked ?? false;
                _account.AutoSelectCharacter = AutoSelectCharacterCheckBox.IsChecked ?? false;

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
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("請輸入帳號名稱", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("請輸入帳號", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("請輸入密碼", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(OtpSecretTextBox.Text))
            {
                MessageBox.Show("請輸入OTP Secret Key", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                OtpSecretTextBox.Focus();
                return false;
            }

            // 驗證 OTP Secret Key 是否有效
            try
            {
                _otpService.GenerateTotp(OtpSecretTextBox.Text.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"OTP Secret Key 格式錯誤: {ex.Message}", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                OtpSecretTextBox.Focus();
                return false;
            }

            return true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _previewTimer?.Stop();
            base.OnClosed(e);
        }
    }
}