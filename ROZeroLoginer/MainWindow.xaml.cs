using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ROZeroLoginer.Models;
using ROZeroLoginer.Services;
using ROZeroLoginer.Utils;
using ROZeroLoginer.Windows;

namespace ROZeroLoginer
{
    public partial class MainWindow : Window
    {
        private readonly DataService _dataService;
        private readonly TotpGenerator _totpGenerator;
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly WindowValidationService _windowValidationService;
        private DispatcherTimer _totpTimer;
        private ObservableCollection<Account> _accounts;
        private Account _selectedAccount;
        private bool _isSelectionWindowOpen = false;

        public MainWindow()
        {
            InitializeComponent();
            
            _dataService = new DataService();
            _totpGenerator = new TotpGenerator();
            _hotkeyService = new GlobalHotkeyService();
            _windowValidationService = new WindowValidationService();
            
            InitializeTimer();
            LoadAccounts();
            SetupHotkey();
            
            this.Closing += MainWindow_Closing;
        }

        private void InitializeTimer()
        {
            _totpTimer = new DispatcherTimer();
            _totpTimer.Interval = TimeSpan.FromSeconds(1);
            _totpTimer.Tick += TotpTimer_Tick;
            _totpTimer.Start();
        }

        private void LoadAccounts()
        {
            var accounts = _dataService.GetAccounts();
            
            if (_accounts == null)
            {
                _accounts = new ObservableCollection<Account>(accounts);
                AccountsDataGrid.ItemsSource = _accounts;
            }
            else
            {
                _accounts.Clear();
                foreach (var account in accounts)
                {
                    _accounts.Add(account);
                }
            }
            
            AccountCountTextBlock.Text = _accounts.Count.ToString();
        }

        private void SetupHotkey()
        {
            var settings = _dataService.GetSettings();
            HotkeyTextBlock.Text = settings.Hotkey.ToString();
            
            if (settings.HotkeyEnabled)
            {
                _hotkeyService.RegisterHotkey(settings.Hotkey, () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ShowAccountSelectionWindow();
                    });
                });
            }
        }

        private void ShowAccountSelectionWindow()
        {
            // 檢查是否已有選擇視窗開啟
            if (_isSelectionWindowOpen)
            {
                StatusTextBlock.Text = "帳號選擇視窗已開啟";
                return;
            }

            // 檢查當前視窗是否為 RO 遊戲視窗
            if (!_windowValidationService.IsRagnarokWindow())
            {
                StatusTextBlock.Text = "當前視窗不是 Ragnarok 遊戲視窗";
                return;
            }

            if (_accounts == null || _accounts.Count == 0)
            {
                MessageBox.Show("沒有可用的帳號", "ROZero Loginer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isSelectionWindowOpen = true;
            var selectionWindow = new AccountSelectionWindow(_accounts.ToList());
            
            // 當視窗關閉時重置標記
            selectionWindow.Closed += (s, e) => _isSelectionWindowOpen = false;
            
            if (selectionWindow.ShowDialog() == true)
            {
                var selectedAccount = selectionWindow.SelectedAccount;
                if (selectedAccount != null)
                {
                    UseAccount(selectedAccount);
                }
            }
            
            _isSelectionWindowOpen = false;
        }

        private void UseAccount(Account account)
        {
            try
            {
                var inputService = new InputService();
                var totp = _totpGenerator.GenerateTotp(account.OtpSecret);
                
                inputService.SendLogin(account.Username, account.Password, totp);
                
                _dataService.UpdateAccountLastUsed(account.Id);
                LoadAccounts();
                
                StatusTextBlock.Text = $"已使用帳號: {account.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"使用帳號時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TotpTimer_Tick(object sender, EventArgs e)
        {
            if (_selectedAccount != null && !string.IsNullOrEmpty(_selectedAccount.OtpSecret))
            {
                try
                {
                    var totp = _totpGenerator.GenerateTotp(_selectedAccount.OtpSecret);
                    var remaining = _totpGenerator.GetTimeRemaining();
                    
                    TotpTextBox.Text = totp;
                    TotpCountdownTextBlock.Text = $"({remaining}s)";
                    CopyTotpButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    TotpTextBox.Text = "錯誤";
                    TotpCountdownTextBlock.Text = "";
                    CopyTotpButton.IsEnabled = false;
                }
            }
            else
            {
                TotpTextBox.Text = "";
                TotpCountdownTextBlock.Text = "";
                CopyTotpButton.IsEnabled = false;
            }
        }

        private void AccountsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedAccount = AccountsDataGrid.SelectedItem as Account;
            
            if (_selectedAccount != null)
            {
                NameTextBox.Text = _selectedAccount.Name;
                UsernameTextBox.Text = _selectedAccount.Username;
                
                EditAccountButton.IsEnabled = true;
                DeleteAccountButton.IsEnabled = true;
                TestTotpButton.IsEnabled = true;
            }
            else
            {
                NameTextBox.Text = "";
                UsernameTextBox.Text = "";
                
                EditAccountButton.IsEnabled = false;
                DeleteAccountButton.IsEnabled = false;
                TestTotpButton.IsEnabled = false;
            }
        }

        private void AddAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var accountWindow = new AccountWindow();
            if (accountWindow.ShowDialog() == true)
            {
                var newAccount = accountWindow.Account;
                _dataService.SaveAccount(newAccount);
                LoadAccounts();
                StatusTextBlock.Text = "新增帳號成功";
            }
        }

        private void EditAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAccount == null) return;

            var accountWindow = new AccountWindow(_selectedAccount);
            if (accountWindow.ShowDialog() == true)
            {
                var updatedAccount = accountWindow.Account;
                _dataService.SaveAccount(updatedAccount);
                LoadAccounts();
                StatusTextBlock.Text = "編輯帳號成功";
            }
        }

        private void DeleteAccountButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAccount == null) return;

            var result = MessageBox.Show($"確定要刪除帳號 '{_selectedAccount.Name}' 嗎？", 
                "確認刪除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _dataService.DeleteAccount(_selectedAccount.Id);
                LoadAccounts();
                StatusTextBlock.Text = "刪除帳號成功";
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_dataService.GetSettings());
            if (settingsWindow.ShowDialog() == true)
            {
                var settings = settingsWindow.Settings;
                _dataService.SaveSettings(settings);
                
                // 重新設定熱鍵
                _hotkeyService.UnregisterAllHotkeys();
                SetupHotkey();
                
                StatusTextBlock.Text = "設定已更新";
            }
        }

        private void TestTotpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAccount == null) return;

            try
            {
                var totp = _totpGenerator.GenerateTotp(_selectedAccount.OtpSecret);
                var remaining = _totpGenerator.GetTimeRemaining();
                
                MessageBox.Show($"TOTP: {totp}\n剩餘時間: {remaining} 秒", "TOTP 測試", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成 TOTP 時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyTotpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TotpTextBox.Text))
            {
                Clipboard.SetText(TotpTextBox.Text);
                StatusTextBlock.Text = "TOTP 已複製到剪貼簿";
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutMessage = "ROZero Loginer v1.0.0\n\n" +
                              "Ragnarok Online Zero 帳號管理工具\n" +
                              "支援 TOTP 驗證與自動登入功能\n\n" +
                              "作者: ontisme\n" +
                              "GitHub: https://github.com/ontisme\n\n" +
                              "特色功能:\n" +
                              "• 安全的帳號密碼管理\n" +
                              "• TOTP 兩步驟驗證\n" +
                              "• 全域熱鍵快速登入\n" +
                              "• 遊戲視窗自動偵測\n" +
                              "• shadcn/ui 風格介面\n\n" +
                              "Copyright © ontisme 2025";

            var result = MessageBox.Show(aboutMessage + "\n\n點擊「是」開啟 GitHub 頁面", 
                                       "關於 ROZero Loginer", 
                                       MessageBoxButton.YesNo, 
                                       MessageBoxImage.Information);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/ontisme",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"無法開啟網頁: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _totpTimer?.Stop();
            _hotkeyService?.UnregisterAllHotkeys();
        }
    }
}