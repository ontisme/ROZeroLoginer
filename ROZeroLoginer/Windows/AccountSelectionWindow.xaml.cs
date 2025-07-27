using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ROZeroLoginer.Models;
using ROZeroLoginer.Utils;

namespace ROZeroLoginer.Windows
{
    public partial class AccountSelectionWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private List<Account> _accounts;
        private Account _preselectedAccount;

        public Account SelectedAccount { get; private set; }

        public AccountSelectionWindow(List<Account> accounts, Account preselectedAccount = null)
        {
            InitializeComponent();
            
            _accounts = accounts;
            _preselectedAccount = preselectedAccount;
            LoadAccounts();
            
            // 確保視窗獲得焦點以接收鍵盤事件
            this.Loaded += (s, e) => 
            {
                ForceFocusWindow();
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

        private void LoadAccounts()
        {
            // 按帳號順序排序
            AccountsDataGrid.ItemsSource = _accounts;
            
            // 自動選擇預選帳號或第一個帳號
            if (_accounts.Count > 0)
            {
                int selectedIndex = 0;
                
                // 如果有預選帳號，尋找其索引
                if (_preselectedAccount != null)
                {
                    for (int i = 0; i < _accounts.Count; i++)
                    {
                        if (_accounts[i].Id == _preselectedAccount.Id)
                        {
                            selectedIndex = i;
                            System.Diagnostics.Debug.WriteLine($"自動選中對應帳號: {_preselectedAccount.Name}");
                            break;
                        }
                    }
                }
                
                AccountsDataGrid.SelectedIndex = selectedIndex;
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

        private void ForceFocusWindow()
        {
            try
            {
                var windowHandle = new WindowInteropHelper(this).Handle;
                
                // 多重方法確保視窗獲得焦點
                ShowWindow(windowHandle, SW_RESTORE);
                ShowWindow(windowHandle, SW_SHOW);
                SetForegroundWindow(windowHandle);
                BringWindowToTop(windowHandle);
                
                // 確保 WPF 視窗也獲得焦點
                this.Activate();
                this.Focus();
                
                // 設定 DataGrid 焦點（延遲執行確保視窗完全載入）
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    AccountsDataGrid.Focus();
                    if (AccountsDataGrid.SelectedIndex >= 0)
                    {
                        var selectedItem = AccountsDataGrid.ItemContainerGenerator.ContainerFromIndex(AccountsDataGrid.SelectedIndex);
                        if (selectedItem is System.Windows.Controls.DataGridRow row)
                        {
                            row.Focus();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Force focus failed: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}