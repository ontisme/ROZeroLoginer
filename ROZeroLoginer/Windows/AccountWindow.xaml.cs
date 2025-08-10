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
                ServerComboBox.SelectedIndex = _account.Server == 2 ? 1 : 0;

                CharacterComboBox.SelectedIndex =
                    _account.Character >= 1 && _account.Character <= 5
                        ? _account.Character - 1
                        : 0;
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
                _account.Server = ServerComboBox.SelectedIndex == 1 ? 2 : 1;

                _account.Character = CharacterComboBox.SelectedIndex >= 0
                    ? CharacterComboBox.SelectedIndex + 1
                    : 1;
                
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