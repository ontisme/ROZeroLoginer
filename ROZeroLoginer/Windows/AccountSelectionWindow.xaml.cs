using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ROZeroLoginer.Models;
using ROZeroLoginer.Utils;

namespace ROZeroLoginer.Windows
{
    public partial class AccountSelectionWindow : Window
    {
        private readonly TotpGenerator _totpGenerator;
        private DispatcherTimer _totpTimer;
        private List<Account> _accounts;

        public Account SelectedAccount { get; private set; }

        public AccountSelectionWindow(List<Account> accounts)
        {
            InitializeComponent();
            
            _totpGenerator = new TotpGenerator();
            _accounts = accounts;
            
            InitializeTimer();
            LoadAccounts();
            
            // 確保視窗獲得焦點以接收鍵盤事件
            this.Loaded += (s, e) => 
            {
                this.Focus();
                AccountsDataGrid.Focus();
            };
            
            // 設定鍵盤快捷鍵 - 使用PreviewKeyDown確保優先處理
            this.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    DialogResult = false;
                    Close();
                }
                else if (e.Key == Key.Enter && AccountsDataGrid.SelectedItem != null)
                {
                    e.Handled = true;
                    SelectAccount();
                }
            };
            
            // 也為DataGrid添加鍵盤事件處理
            AccountsDataGrid.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter && AccountsDataGrid.SelectedItem != null)
                {
                    e.Handled = true;
                    SelectAccount();
                }
            };
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
            // 按最後使用時間排序，最近使用的在前面
            var sortedAccounts = _accounts.OrderByDescending(a => a.LastUsed).ToList();
            AccountsDataGrid.ItemsSource = sortedAccounts;
            
            // 自動選擇第一個帳號
            if (sortedAccounts.Count > 0)
            {
                AccountsDataGrid.SelectedIndex = 0;
            }
        }

        private void TotpTimer_Tick(object sender, EventArgs e)
        {
            UpdateTotpDisplay();
        }

        private void UpdateTotpDisplay()
        {
            var accounts = AccountsDataGrid.ItemsSource as List<Account>;
            if (accounts == null) return;

            // 更新每個帳號的TOTP顯示
            for (int i = 0; i < accounts.Count; i++)
            {
                var account = accounts[i];
                var row = AccountsDataGrid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
                
                if (row != null)
                {
                    var totpColumn = AccountsDataGrid.Columns[3];
                    var cell = totpColumn.GetCellContent(row) as StackPanel;
                    
                    if (cell != null)
                    {
                        var totpText = cell.Children[0] as TextBlock;
                        var countdownText = cell.Children[1] as TextBlock;
                        
                        if (totpText != null && countdownText != null)
                        {
                            try
                            {
                                var totp = _totpGenerator.GenerateTotp(account.OtpSecret);
                                var remaining = _totpGenerator.GetTimeRemaining();
                                
                                totpText.Text = totp;
                                countdownText.Text = $"({remaining}s)";
                            }
                            catch (Exception)
                            {
                                totpText.Text = "錯誤";
                                countdownText.Text = "";
                            }
                        }
                    }
                }
            }
        }

        private void AccountsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectButton.IsEnabled = AccountsDataGrid.SelectedItem != null;
        }

        private void AccountsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AccountsDataGrid.SelectedItem != null)
            {
                SelectAccount();
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectAccount();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectAccount()
        {
            SelectedAccount = AccountsDataGrid.SelectedItem as Account;
            DialogResult = true;
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _totpTimer?.Stop();
            base.OnClosed(e);
        }
    }
}