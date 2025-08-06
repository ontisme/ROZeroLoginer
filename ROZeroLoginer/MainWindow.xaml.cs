using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ROZeroLoginer.Models;
using ROZeroLoginer.Services;
using ROZeroLoginer.Utils;
using ROZeroLoginer.Windows;

namespace ROZeroLoginer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private DataService _dataService;
        private readonly TotpGenerator _totpGenerator;
        private readonly LowLevelKeyboardHookService _hotkeyService;
        private readonly WindowValidationService _windowValidationService;
        private DispatcherTimer _totpTimer;
        private ObservableCollection<Account> _accounts;
        private Account _selectedAccount;
        private bool _isSelectionWindowOpen = false;
        private AppSettings _currentSettings;
        private ObservableCollection<AccountDisplayItem> _displayAccounts;
        private string _currentGroupFilter = "所有分組";

        public AppSettings CurrentSettings
        {
            get => _currentSettings;
            set
            {
                _currentSettings = value;
                OnPropertyChanged();
                UpdateDisplayAccounts();
            }
        }

        public ObservableCollection<AccountDisplayItem> DisplayAccounts
        {
            get => _displayAccounts;
            set
            {
                _displayAccounts = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainWindow()
        {
            InitializeComponent();

            _dataService = new DataService();
            _totpGenerator = new TotpGenerator();
            _hotkeyService = new LowLevelKeyboardHookService();
            _windowValidationService = new WindowValidationService();

            CurrentSettings = _dataService.GetSettings();
            DisplayAccounts = new ObservableCollection<AccountDisplayItem>();
            this.DataContext = this;

            // 設定視窗標題包含版本號
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            this.Title = $"Ragnarok Online Zero 帳號管理工具 v{version}";

            InitializeTimer();
            LoadAccounts();
            LoadGroupTabs();
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
                AccountsDataGrid.ItemsSource = DisplayAccounts;
            }
            else
            {
                _accounts.Clear();
                foreach (var account in accounts)
                {
                    _accounts.Add(account);
                }
            }

            UpdateDisplayAccounts();
            AccountCountTextBlock.Text = _accounts.Count.ToString();
        }

        private void LoadGroupTabs()
        {
            try
            {
                var allGroups = _accounts?.Select(a => a.Group).Distinct().OrderBy(g => g).ToList() ?? new List<string>();

                GroupTabControl.Items.Clear();

                // 添加 "所有分組" TAB
                var allTab = new TabItem
                {
                    Header = "所有分組",
                    Tag = "所有分組"
                };
                GroupTabControl.Items.Add(allTab);

                // 添加各個分組TAB
                foreach (var group in allGroups)
                {
                    if (!string.IsNullOrEmpty(group))
                    {
                        var tabItem = new TabItem
                        {
                            Header = group,
                            Tag = group
                        };
                        GroupTabControl.Items.Add(tabItem);
                    }
                }

                // 默認選中第一個TAB
                if (GroupTabControl.Items.Count > 0)
                {
                    GroupTabControl.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加載分組TAB時發生錯誤: {ex.Message}");
            }
        }

        private void UpdateDisplayAccounts()
        {
            if (_accounts == null || DisplayAccounts == null) return;

            DisplayAccounts.Clear();

            // 根據當前分組篩選帳號
            var filteredAccounts = _currentGroupFilter == "所有分組"
                ? _accounts
                : _accounts.Where(a => a.Group == _currentGroupFilter);

            foreach (var account in filteredAccounts)
            {
                var displayItem = new AccountDisplayItem(account, CurrentSettings);
                displayItem.PropertyChanged += DisplayItem_PropertyChanged;
                DisplayAccounts.Add(displayItem);
            }
        }

        private void DisplayItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AccountDisplayItem.IsSelected))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateLaunchSelectedButtonState();
                }), DispatcherPriority.Background);
            }
        }

        private void UpdateLaunchSelectedButtonState()
        {
            var hasSelected = DisplayAccounts?.Any(item => item.IsSelected) == true;
            LaunchSelectedButton.IsEnabled = hasSelected;
            DeleteSelectedButton.IsEnabled = hasSelected;
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // 立即更新按鈕狀態
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateLaunchSelectedButtonState();
            }), DispatcherPriority.Background);
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

            if (_accounts == null || _accounts.Count == 0)
            {
                MessageBox.Show("沒有可用的帳號", "ROZero Loginer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isSelectionWindowOpen = true;

            var selectionWindow = new AccountSelectionWindow(_accounts.ToList(), null);

            // 當視窗關閉時重置標記
            selectionWindow.Closed += (s, e) => _isSelectionWindowOpen = false;

            // 強制視窗置於最前方
            selectionWindow.Topmost = true;

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
                var totp = _totpGenerator.GenerateTotpWithTiming(account.OtpSecret);
                var settings = _dataService.GetSettings();

                inputService.SendLogin(account.Username, account.Password, totp, settings.OtpInputDelayMs);

                _dataService.UpdateAccountLastUsed(account.Id);

                // 在 UI 執行緒中更新帳號列表
                Dispatcher.Invoke(() =>
                {
                    LoadAccounts();
                    StatusTextBlock.Text = $"已使用帳號: {account.Name}";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LoadAccounts(); // 確保即使出錯也重新載入帳號列表
                    MessageBox.Show($"使用帳號時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                });
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
            var selectedDisplayItem = AccountsDataGrid.SelectedItem as AccountDisplayItem;
            _selectedAccount = selectedDisplayItem?.Account;

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
                LoadGroupTabs();
                StatusTextBlock.Text = "新增帳號成功";
            }
        }

        private void BatchAddButton_Click(object sender, RoutedEventArgs e)
        {
            var batchAddWindow = new BatchAddWindow();
            if (batchAddWindow.ShowDialog() == true)
            {
                var importedAccounts = batchAddWindow.ImportedAccounts;
                var successCount = 0;
                var errorCount = 0;

                foreach (var account in importedAccounts)
                {
                    try
                    {
                        _dataService.SaveAccount(account);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to save account {account.Name}: {ex.Message}");
                        errorCount++;
                    }
                }

                LoadAccounts();
                LoadGroupTabs();

                if (errorCount == 0)
                {
                    StatusTextBlock.Text = $"批次新增成功：{successCount} 個帳號";
                }
                else
                {
                    StatusTextBlock.Text = $"批次新增完成：{successCount} 個成功，{errorCount} 個失敗";
                    MessageBox.Show($"部分帳號新增失敗\n成功：{successCount} 個\n失敗：{errorCount} 個",
                                  "批次新增結果", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
                LoadGroupTabs();
                StatusTextBlock.Text = "編輯帳號成功";
            }
        }

        private void BatchEditButton_Click(object sender, RoutedEventArgs e)
        {
            var batchEditWindow = new BatchEditWindow(_dataService);
            if (batchEditWindow.ShowDialog() == true)
            {
                LoadAccounts();
                LoadGroupTabs();
                StatusTextBlock.Text = "批次編輯完成";
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
                LoadGroupTabs();
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

                // 更新當前設定以觸發UI更新
                CurrentSettings = settings;

                // 重新設定熱鍵
                _hotkeyService.UnregisterAllHotkeys();
                SetupHotkey();

                StatusTextBlock.Text = "設定已更新";

                // 檢查是否有資料還原，如果有則立即重新載入
                if (settingsWindow.DataRestored)
                {
                    try
                    {
                        ReloadAllData();
                        StatusTextBlock.Text = "資料還原完成，已重新載入所有資料";
                        MessageBox.Show($"資料還原成功！已載入 {_accounts?.Count ?? 0} 個帳號。", "還原完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"資料還原後重新載入失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void TestTotpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAccount == null) return;

            try
            {
                var totp = _totpGenerator.GenerateTotpWithTiming(_selectedAccount.OtpSecret);
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
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            var aboutMessage = $"ROZero Loginer v{version}\n\n" +
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


        private void GroupTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupTabControl.SelectedItem is TabItem selectedTab)
            {
                _currentGroupFilter = selectedTab.Tag?.ToString() ?? "所有分組";
                UpdateDisplayAccounts();
                UpdateLaunchSelectedButtonState();
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayAccounts != null)
            {
                foreach (var item in DisplayAccounts)
                {
                    item.IsSelected = true;
                }
                UpdateLaunchSelectedButtonState();
            }
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayAccounts != null)
            {
                foreach (var item in DisplayAccounts)
                {
                    item.IsSelected = false;
                }
                UpdateLaunchSelectedButtonState();
            }
        }

        private void LaunchSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayAccounts == null) return;

            var selectedAccounts = DisplayAccounts
                .Where(item => item.IsSelected)
                .Select(item => item.Account)
                .ToList();

            if (selectedAccounts.Count == 0)
            {
                MessageBox.Show("請選擇要啟動的帳號", "批次啟動", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"確定要啟動選中的 {selectedAccounts.Count} 個帳號嗎？",
                "批次啟動",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Task.Run(() => BatchLaunchGames(selectedAccounts));
            }
        }

        private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayAccounts == null) return;

            var selectedAccounts = DisplayAccounts
                .Where(item => item.IsSelected)
                .Select(item => item.Account)
                .ToList();

            if (selectedAccounts.Count == 0)
            {
                MessageBox.Show("請選擇要刪除的帳號", "批次刪除", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"確定要刪除選中的 {selectedAccounts.Count} 個帳號嗎？\n\n" +
                "此操作無法復原！", 
                "批次刪除確認", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var successCount = 0;
                var errorCount = 0;

                foreach (var account in selectedAccounts)
                {
                    try
                    {
                        _dataService.DeleteAccount(account.Id);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete account {account.Name}: {ex.Message}");
                        errorCount++;
                    }
                }

                LoadAccounts();
                LoadGroupTabs();
                
                if (errorCount == 0)
                {
                    StatusTextBlock.Text = $"批次刪除成功：{successCount} 個帳號";
                }
                else
                {
                    StatusTextBlock.Text = $"批次刪除完成：{successCount} 個成功，{errorCount} 個失敗";
                    MessageBox.Show($"部分帳號刪除失敗\n成功：{successCount} 個\n失敗：{errorCount} 個", 
                                  "批次刪除結果", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void LaunchGameForAccount(Account account)
        {
            try
            {
                var settings = _dataService.GetSettings();

                if (string.IsNullOrEmpty(settings.RoGamePath) || !File.Exists(settings.RoGamePath))
                {
                    MessageBox.Show("RO 主程式路徑無效，請到設定中正確設定遊戲路徑。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusTextBlock.Text = $"正在啟動遊戲 - {account.Name}...";

                // 啟動遊戲 - 設定工作目錄為遊戲目錄
                var gameDirectory = Path.GetDirectoryName(settings.RoGamePath);
                var gameExecutable = Path.GetFileName(settings.RoGamePath);

                var processInfo = new ProcessStartInfo
                {
                    FileName = gameExecutable,
                    Arguments = "1rag1",
                    WorkingDirectory = gameDirectory,
                    UseShellExecute = true
                };

                var gameProcess = Process.Start(processInfo);

                if (gameProcess == null)
                {
                    throw new Exception("無法啟動遊戲進程");
                }

                // 等待一下遊戲啟動
                await Task.Delay(3000);

                // 執行自動輸入帳號密碼
                UseAccount(account);

                StatusTextBlock.Text = $"遊戲已啟動並自動登入 - {account.Name}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"啟動遊戲時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "遊戲啟動失敗";
            }
        }

        private async void BatchLaunchGames(List<Account> selectedAccounts)
        {
            var successCount = 0;
            var failCount = 0;

            foreach (var account in selectedAccounts)
            {
                try
                {
                    Dispatcher.Invoke(() => StatusTextBlock.Text = $"正在啟動遊戲 - {account.Name}...");

                    // 在背景執行緒中執行啟動遊戲邏輯
                    await Task.Run(() => LaunchGameForAccountInternal(account));

                    successCount++;

                    // 每個帳號之間等待一段時間
                    await Task.Delay(3000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to launch game for {account.Name}: {ex.Message}");
                    Dispatcher.Invoke(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"批次啟動失敗 - {account.Name}: {ex.Message}");
                    });
                    failCount++;
                }
            }

            Dispatcher.Invoke(() =>
            {
                if (failCount == 0)
                {
                    StatusTextBlock.Text = $"批次啟動完成：{successCount} 個成功";
                }
                else
                {
                    StatusTextBlock.Text = $"批次啟動完成：{successCount} 個成功，{failCount} 個失敗";
                    MessageBox.Show($"批次啟動完成\n成功：{successCount} 個\n失敗：{failCount} 個",
                                  "批次啟動結果", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
        }

        private void LaunchGameForAccountInternal(Account account)
        {
            var settings = _dataService.GetSettings();

            if (string.IsNullOrEmpty(settings.RoGamePath) || !File.Exists(settings.RoGamePath))
            {
                throw new Exception("RO 主程式路徑無效");
            }

            // 啟動遊戲 - 設定工作目錄為遊戲目錄
            var gameDirectory = Path.GetDirectoryName(settings.RoGamePath);
            var gameExecutable = Path.GetFileName(settings.RoGamePath);

            var processInfo = new ProcessStartInfo
            {
                FileName = gameExecutable,
                Arguments = "1rag1",
                WorkingDirectory = gameDirectory,
                UseShellExecute = true
            };

            var gameProcess = Process.Start(processInfo);

            if (gameProcess == null)
            {
                throw new Exception("無法啟動遊戲進程");
            }

            // 等待一下遊戲啟動
            System.Threading.Thread.Sleep(3000);

            // 執行自動輸入帳號密碼（在背景執行緒中，但需要處理 UI 更新）
            var inputService = new InputService();
            var totp = _totpGenerator.GenerateTotpWithTiming(account.OtpSecret);

            inputService.SendLogin(account.Username, account.Password, totp, settings.OtpInputDelayMs);

            _dataService.UpdateAccountLastUsed(account.Id);
        }




        private void ReloadAllData()
        {
            try
            {
                // 重新建立 DataService 以確保載入新的金鑰和資料
                _dataService = new DataService();
                
                // 強制當前 DataService 重新載入（雙重保險）
                _dataService.ForceReload();

                // 重新載入設定並更新 CurrentSettings
                var newSettings = _dataService.GetSettings();
                CurrentSettings = newSettings;

                // 重新載入帳號資料
                LoadAccounts();
                LoadGroupTabs();

                // 重新設定熱鍵（因為設定可能已更新）
                _hotkeyService.UnregisterAllHotkeys();
                SetupHotkey();

                // 清除當前選中的帳號
                _selectedAccount = null;
                AccountsDataGrid.SelectedItem = null;

                // 清空詳細資料顯示
                NameTextBox.Text = "";
                UsernameTextBox.Text = "";
                TotpTextBox.Text = "";
                TotpCountdownTextBlock.Text = "";

                // 更新按鈕狀態
                EditAccountButton.IsEnabled = false;
                DeleteAccountButton.IsEnabled = false;
                TestTotpButton.IsEnabled = false;
                CopyTotpButton.IsEnabled = false;
                UpdateLaunchSelectedButtonState();

                // 更新視窗標題（如果版本有變化）
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                this.Title = $"Ragnarok Online Zero 帳號管理工具 v{version}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新載入資料時發生錯誤: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _totpTimer?.Stop();
            _hotkeyService?.UnregisterAllHotkeys();
        }
    }
}