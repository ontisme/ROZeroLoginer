using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using ROZeroLoginer.Models;
using ROZeroLoginer.Services;
using ROZeroLoginer.Utils;

namespace ROZeroLoginer.Windows
{
    public partial class BatchEditWindow : Window
    {
        private readonly DataService _dataService;
        private readonly TotpGenerator _totpGenerator;
        private readonly ObservableCollection<SelectableAccount> _accounts = new ObservableCollection<SelectableAccount>();

        public BatchEditWindow(DataService dataService)
        {
            InitializeComponent();
            _dataService = dataService;
            _totpGenerator = new TotpGenerator();
            
            LoadAccounts();
            LoadGroups();
            
            AccountListView.ItemsSource = _accounts;
        }

        private void LoadAccounts()
        {
            _accounts.Clear();
            var accounts = _dataService.GetAccounts();
            
            foreach (var account in accounts)
            {
                _accounts.Add(new SelectableAccount { Account = account, IsSelected = false });
            }
        }

        private void LoadGroups()
        {
            var accounts = _dataService.GetAccounts();
            var groups = accounts.Select(a => a.Group).Distinct().OrderBy(g => g).ToList();
            
            GroupComboBox.ItemsSource = groups;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var account in _accounts)
            {
                account.IsSelected = true;
            }
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var account in _accounts)
            {
                account.IsSelected = false;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedAccounts = _accounts.Where(a => a.IsSelected).ToList();
                
                if (selectedAccounts.Count == 0)
                {
                    MessageBox.Show("請至少選擇一個帳號", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool hasChanges = EditGroupCheckBox.IsChecked == true || 
                                 EditPasswordCheckBox.IsChecked == true || 
                                 EditOtpCheckBox.IsChecked == true || 
                                 EditNameCheckBox.IsChecked == true;

                if (!hasChanges)
                {
                    MessageBox.Show("請至少選擇一個要修改的欄位", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 驗證輸入
                if (!ValidateInputs())
                {
                    return;
                }

                // 確認操作
                var result = MessageBox.Show(
                    $"確定要批次修改 {selectedAccounts.Count} 個帳號嗎？", 
                    "確認批次修改", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                // 執行批次修改
                int successCount = 0;
                var errorMessages = new List<string>();

                foreach (var selectableAccount in selectedAccounts)
                {
                    try
                    {
                        var account = selectableAccount.Account;

                        if (EditGroupCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(GroupComboBox.Text))
                        {
                            account.Group = GroupComboBox.Text.Trim();
                        }

                        if (EditPasswordCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(PasswordBox.Password))
                        {
                            account.Password = PasswordBox.Password;
                        }

                        if (EditOtpCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(OtpSecretTextBox.Text))
                        {
                            account.OtpSecret = OtpSecretTextBox.Text.Trim();
                        }

                        if (EditNameCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(NameTextBox.Text))
                        {
                            account.Name = NameTextBox.Text.Trim();
                        }

                        _dataService.SaveAccount(account);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorMessages.Add($"{selectableAccount.Account.Name}: {ex.Message}");
                    }
                }

                // 顯示結果
                string message = $"批次修改完成！\n成功修改 {successCount} 個帳號";
                if (errorMessages.Count > 0)
                {
                    message += $"\n失敗 {errorMessages.Count} 個帳號：\n" + string.Join("\n", errorMessages.Take(5));
                    if (errorMessages.Count > 5)
                    {
                        message += $"\n... 還有 {errorMessages.Count - 5} 個錯誤";
                    }
                }

                MessageBox.Show(message, "批次修改結果", MessageBoxButton.OK, 
                    errorMessages.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);

                if (successCount > 0)
                {
                    DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"批次修改時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInputs()
        {
            // 驗證OTP密鑰
            if (EditOtpCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(OtpSecretTextBox.Text))
            {
                try
                {
                    _totpGenerator.GenerateTotp(OtpSecretTextBox.Text.Trim());
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"OTP Secret Key 格式錯誤: {ex.Message}", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    OtpSecretTextBox.Focus();
                    return false;
                }
            }

            // 驗證名稱
            if (EditNameCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("名稱不能為空", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameTextBox.Focus();
                return false;
            }

            // 驗證分組
            if (EditGroupCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(GroupComboBox.Text))
            {
                MessageBox.Show("分組不能為空", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                GroupComboBox.Focus();
                return false;
            }

            // 驗證密碼
            if (EditPasswordCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("密碼不能為空", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordBox.Focus();
                return false;
            }

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }

    // 用於顯示可選帳號的輔助類
    public class SelectableAccount : INotifyPropertyChanged
    {
        private bool _isSelected;

        public Account Account { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}