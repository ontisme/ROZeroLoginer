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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Drawing;
using WinForms = System.Windows.Forms;
using ROZeroLoginer.Models;
using ROZeroLoginer.Services;
using ROZeroLoginer.Utils;
using ROZeroLoginer.Windows;

namespace ROZeroLoginer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private DataService _dataService;
        private readonly OtpService _otpService;
        private readonly LowLevelKeyboardHookService _hotkeyService;
        private readonly WindowValidationService _windowValidationService;
        private DispatcherTimer _totpTimer;
        private ObservableCollection<Account> _accounts;
        private Account _selectedAccount;
        private bool _isSelectionWindowOpen = false;
        private AppSettings _currentSettings;
        private ObservableCollection<AccountDisplayItem> _displayAccounts;
        private string _currentGroupFilter = "所有分組";
        private bool _hasNewVersion = false;
        private WinForms.NotifyIcon _trayIcon;
        private bool _isVerticalTabMode = false;

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

            // 記錄程序啟動
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            LogService.Instance.Info("=== ROZero Loginer v{0} 啟動 ===", version);
            LogService.Instance.Info("作業系統: {0}", Environment.OSVersion);
            LogService.Instance.Info("工作目錄: {0}", Environment.CurrentDirectory);

            _dataService = new DataService();
            _otpService = new OtpService();
            CurrentSettings = _dataService.GetSettings();
            
            _hotkeyService = new LowLevelKeyboardHookService(_currentSettings);
            _windowValidationService = new WindowValidationService(_currentSettings);
            DisplayAccounts = new ObservableCollection<AccountDisplayItem>();
            this.DataContext = this;

            // 設定視窗標題包含版本號
            this.Title = $"Ragnarok Online Zero 帳號管理工具 v{version}";

            InitializeTimer();
            LoadAccounts();
            LoadGroupTabs();
            
            // 初始化垂直標籤模式
            _isVerticalTabMode = CurrentSettings.UseVerticalTabLayout;
            UpdateTabControlLayout();
            
            SetupHotkey();

            this.Closing += MainWindow_Closing;
            
            // 初始化系統匣圖示
            InitializeTrayIcon();
            
            // 還原視窗位置和大小
            RestoreWindowState();

            // 啟動時自動檢查更新
            CheckForUpdatesOnStartup();

            LogService.Instance.Info("主視窗初始化完成");
        }

        private void InitializeTimer()
        {
            _totpTimer = new DispatcherTimer();
            _totpTimer.Interval = TimeSpan.FromSeconds(1);
            _totpTimer.Tick += TotpTimer_Tick;
            _totpTimer.Start();
        }

        private void InitializeTrayIcon()
        {
            try
            {
                // 創建系統匣圖示
                _trayIcon = new WinForms.NotifyIcon();
                
                // 設定圖示（使用應用程式內建圖示）
                var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/ROZeroLoginer;component/favicon.ico"));
                if (iconStream != null)
                {
                    _trayIcon.Icon = new Icon(iconStream.Stream);
                }
                else
                {
                    // 如果沒有找到圖示檔案，使用系統預設圖示
                    _trayIcon.Icon = SystemIcons.Application;
                }

                // 設定提示文字
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                _trayIcon.Text = $"ROZero Loginer v{version}";

                // 設定雙擊事件
                _trayIcon.DoubleClick += TrayIcon_DoubleClick;

                // 創建右鍵選單
                var contextMenu = new WinForms.ContextMenuStrip();
                contextMenu.Items.Add("顯示主視窗", null, (s, e) => ShowWindow());
                contextMenu.Items.Add("-"); // 分隔線
                contextMenu.Items.Add("退出", null, (s, e) => System.Windows.Application.Current.Shutdown());
                _trayIcon.ContextMenuStrip = contextMenu;

                // 處理視窗狀態變化
                this.StateChanged += MainWindow_StateChanged;

                LogService.Instance.Info("系統匣圖示初始化完成");
            }
            catch (Exception ex)
            {
                LogService.Instance.Warning("系統匣圖示初始化失敗: {0}", ex.Message);
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            // 如果啟用了最小化到系統匣，且視窗最小化了，則隱藏視窗
            if (_currentSettings?.MinimizeToTray == true && this.WindowState == WindowState.Minimized)
            {
                this.Hide();
                _trayIcon.Visible = true;
                
                // 第一次最小化時顯示通知
                if (_currentSettings?.ShowNotifications == true)
                {
                    _trayIcon.ShowBalloonTip(2000, "ROZero Loginer", "已最小化到系統匣", WinForms.ToolTipIcon.Info);
                }
            }
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowWindow();
        }

        private void ShowWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            _trayIcon.Visible = false;
        }

        private void RestoreWindowState()
        {
            try
            {
                // 設定默認大小（如果沒有保存的設定）
                if (_currentSettings.WindowWidth <= 0 || _currentSettings.WindowHeight <= 0)
                {
                    this.Width = 913;
                    this.Height = 600;
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
                else
                {
                    this.Width = _currentSettings.WindowWidth;
                    this.Height = _currentSettings.WindowHeight;
                }

                if (_currentSettings.WindowLeft >= 0 && _currentSettings.WindowTop >= 0)
                {
                    // 確保視窗在可見螢幕範圍內
                    var workingArea = SystemParameters.WorkArea;
                    if (_currentSettings.WindowLeft < workingArea.Width - 100 && 
                        _currentSettings.WindowTop < workingArea.Height - 50)
                    {
                        this.Left = _currentSettings.WindowLeft;
                        this.Top = _currentSettings.WindowTop;
                        this.WindowStartupLocation = WindowStartupLocation.Manual;
                    }
                    else
                    {
                        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                }
                else
                {
                    this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                if (_currentSettings.WindowMaximized)
                {
                    this.WindowState = WindowState.Maximized;
                }

                LogService.Instance.Info("視窗狀態已還原: {0}x{1} at ({2},{3}), 最大化: {4}", 
                    this.Width, this.Height, this.Left, this.Top, this.WindowState == WindowState.Maximized);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warning("還原視窗狀態時發生錯誤: {0}", ex.Message);
                // 發生錯誤時使用默認設定
                this.Width = 913;
                this.Height = 600;
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void SaveWindowState()
        {
            try
            {
                if (this.WindowState == WindowState.Maximized)
                {
                    _currentSettings.WindowMaximized = true;
                }
                else
                {
                    _currentSettings.WindowMaximized = false;
                    _currentSettings.WindowWidth = this.ActualWidth;
                    _currentSettings.WindowHeight = this.ActualHeight;
                    _currentSettings.WindowLeft = this.Left;
                    _currentSettings.WindowTop = this.Top;
                }

                _dataService.SaveSettings(_currentSettings);
                LogService.Instance.Debug("視窗狀態已保存");
            }
            catch (Exception ex)
            {
                LogService.Instance.Warning("保存視窗狀態時發生錯誤: {0}", ex.Message);
            }
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
                var allGroups = _accounts?.Select(a => a.Group).Distinct().Where(g => !string.IsNullOrEmpty(g)).ToList() ?? new List<string>();
                
                // 保存目前選中的分組
                var currentSelectedGroup = _currentGroupFilter;

                // 獲取現有的分組列表（除了"所有分組"）
                var existingGroups = new List<string>();
                for (int i = 1; i < GroupTabControl.Items.Count; i++) // 跳過第一個 "所有分組"
                {
                    if (GroupTabControl.Items[i] is TabItem tabItem && tabItem.Tag != null)
                    {
                        existingGroups.Add(tabItem.Tag.ToString());
                    }
                }

                // 檢查分組是否有變化
                var newGroups = allGroups.Except(existingGroups).ToList();
                var removedGroups = existingGroups.Except(allGroups).ToList();
                
                // 只有當分組真的有變化時才重新創建
                bool hasChanges = newGroups.Any() || removedGroups.Any() || GroupTabControl.Items.Count == 0;

                if (hasChanges)
                {
                    GroupTabControl.Items.Clear();

                    // 添加 "所有分組" TAB
                    var allTab = new TabItem
                    {
                        Header = "所有分組",
                        Tag = "所有分組"
                    };
                    GroupTabControl.Items.Add(allTab);

                    // 保持原有順序：先添加仍存在的現有分組
                    foreach (var existingGroup in existingGroups)
                    {
                        if (allGroups.Contains(existingGroup))
                        {
                            var tabItem = new TabItem
                            {
                                Header = existingGroup,
                                Tag = existingGroup
                            };
                            GroupTabControl.Items.Add(tabItem);
                        }
                    }

                    // 然後在最後添加新分組
                    foreach (var newGroup in newGroups)
                    {
                        var tabItem = new TabItem
                        {
                            Header = newGroup,
                            Tag = newGroup
                        };
                        GroupTabControl.Items.Add(tabItem);
                    }

                    // 嘗試恢復之前選中的分組
                    int indexToSelect = 0;
                    if (!string.IsNullOrEmpty(currentSelectedGroup))
                    {
                        for (int i = 0; i < GroupTabControl.Items.Count; i++)
                        {
                            if (GroupTabControl.Items[i] is TabItem tabItem && 
                                tabItem.Tag?.ToString() == currentSelectedGroup)
                            {
                                indexToSelect = i;
                                break;
                            }
                        }
                    }

                    // 設定選中的TAB
                    if (GroupTabControl.Items.Count > 0)
                    {
                        GroupTabControl.SelectedIndex = indexToSelect;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加載分組TAB時發生錯誤: {ex.Message}");
            }
            
            // 確保新創建的標籤使用正確的樣式
            UpdateTabControlLayout();
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
                DisplayAccounts.Add(displayItem);
            }
        }

        private void UpdateTabControlLayout()
        {
            if (GroupTabControl != null && VerticalTabPanel != null && VerticalTabScrollViewer != null && AccountsDataGrid != null)
            {
                // 尋找VerticalTabBorder
                var verticalTabBorder = this.FindName("VerticalTabBorder") as System.Windows.Controls.Border;
                
                if (_isVerticalTabMode)
                {
                    // 垂直模式：隱藏TabControl，顯示垂直按鈕面板
                    GroupTabControl.Visibility = System.Windows.Visibility.Collapsed;
                    if (verticalTabBorder != null)
                        verticalTabBorder.Visibility = System.Windows.Visibility.Visible;
                    
                    // 調整DataGrid佈局
                    System.Windows.Controls.Grid.SetColumn(AccountsDataGrid, 1);
                    System.Windows.Controls.Grid.SetRow(AccountsDataGrid, 1);
                    System.Windows.Controls.Grid.SetRowSpan(AccountsDataGrid, 1);
                    System.Windows.Controls.Grid.SetColumnSpan(AccountsDataGrid, 1);
                    
                    // 填充垂直按鈕
                    PopulateVerticalTabs();
                }
                else
                {
                    // 水平模式：顯示TabControl，隱藏垂直按鈕面板
                    GroupTabControl.Visibility = System.Windows.Visibility.Visible;
                    if (verticalTabBorder != null)
                        verticalTabBorder.Visibility = System.Windows.Visibility.Collapsed;
                    
                    // 調整DataGrid佈局
                    System.Windows.Controls.Grid.SetColumn(AccountsDataGrid, 0);
                    System.Windows.Controls.Grid.SetRow(AccountsDataGrid, 1);
                    System.Windows.Controls.Grid.SetRowSpan(AccountsDataGrid, 1);
                    System.Windows.Controls.Grid.SetColumnSpan(AccountsDataGrid, 2);
                    
                    // 應用水平樣式
                    var style = (System.Windows.Style)GroupTabControl.Resources["HorizontalTabStyle"];
                    foreach (System.Windows.Controls.TabItem tab in GroupTabControl.Items)
                    {
                        tab.Style = style;
                    }
                }
                    
                // 更新按鈕文字
                if (ToggleVerticalTabButton != null)
                {
                    ToggleVerticalTabButton.Content = _isVerticalTabMode ? "水平標籤" : "垂直標籤";
                }
            }
        }
        
        private void PopulateVerticalTabs()
        {
            if (VerticalTabPanel != null && GroupTabControl != null)
            {
                VerticalTabPanel.Children.Clear();
                
                // 為每個TabItem創建垂直按鈕
                foreach (System.Windows.Controls.TabItem tabItem in GroupTabControl.Items)
                {
                    var button = new System.Windows.Controls.Button
                    {
                        Content = tabItem.Header?.ToString(),
                        Tag = tabItem,
                        Margin = new System.Windows.Thickness(0, 0, 0, 2),
                        Padding = new System.Windows.Thickness(8, 10, 6, 10),
                        MinWidth = 100,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
                        Background = System.Windows.Media.Brushes.Transparent,
                        BorderThickness = new System.Windows.Thickness(2, 0, 0, 0),
                        BorderBrush = System.Windows.Media.Brushes.Transparent,
                        FontSize = 11,
                        FontWeight = System.Windows.FontWeights.Medium
                    };
                    
                    // 設定按鈕樣式
                    UpdateVerticalButtonStyle(button, tabItem == GroupTabControl.SelectedItem);
                    
                    // 點擊事件
                    button.Click += (s, e) =>
                    {
                        var clickedButton = s as System.Windows.Controls.Button;
                        var associatedTab = clickedButton?.Tag as System.Windows.Controls.TabItem;
                        if (associatedTab != null)
                        {
                            GroupTabControl.SelectedItem = associatedTab;
                            UpdateAllVerticalButtonStyles();
                        }
                    };
                    
                    VerticalTabPanel.Children.Add(button);
                }
            }
        }
        
        private void UpdateVerticalButtonStyle(System.Windows.Controls.Button button, bool isSelected)
        {
            try
            {
                if (isSelected)
                {
                    button.Background = System.Windows.Media.Brushes.White;
                    button.BorderBrush = (System.Windows.Media.SolidColorBrush)FindResource("PrimaryBrush");
                    button.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("PrimaryBrush");
                    button.FontWeight = System.Windows.FontWeights.SemiBold;
                }
                else
                {
                    button.Background = System.Windows.Media.Brushes.Transparent;
                    button.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    button.Foreground = (System.Windows.Media.SolidColorBrush)FindResource("MutedForegroundBrush");
                    button.FontWeight = System.Windows.FontWeights.Medium;
                }
            }
            catch
            {
                // Fallback colors if resources not found
                if (isSelected)
                {
                    button.Background = System.Windows.Media.Brushes.LightBlue;
                    button.BorderBrush = System.Windows.Media.Brushes.Blue;
                    button.Foreground = System.Windows.Media.Brushes.Blue;
                }
                else
                {
                    button.Background = System.Windows.Media.Brushes.Transparent;
                    button.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    button.Foreground = System.Windows.Media.Brushes.Gray;
                }
            }
        }
        
        private void UpdateAllVerticalButtonStyles()
        {
            if (VerticalTabPanel != null && GroupTabControl != null)
            {
                foreach (System.Windows.Controls.Button button in VerticalTabPanel.Children)
                {
                    var associatedTab = button.Tag as System.Windows.Controls.TabItem;
                    UpdateVerticalButtonStyle(button, associatedTab == GroupTabControl.SelectedItem);
                }
            }
        }


        private void UpdateLaunchSelectedButtonState()
        {
            var hasSelected = AccountsDataGrid?.SelectedItems?.Count > 0;
            LaunchSelectedButton.IsEnabled = hasSelected;
            DeleteSelectedButton.IsEnabled = hasSelected;
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
            // 檢查當前前台視窗是否為 RO 視窗（熱鍵模式限制）
            var inputService = new InputService(_currentSettings);
            if (!inputService.IsCurrentWindowRagnarok())
            {
                // 當前視窗不是 RO 視窗時，不執行操作
                LogService.Instance.Info("[ShowAccountSelectionWindow] 熱鍵觸發時前台視窗不是 RO 視窗，忽略操作");
                return;
            }

            // 檢查是否已有選擇視窗開啟
            if (_isSelectionWindowOpen)
            {
                StatusTextBlock.Text = "帳號選擇視窗已開啟";
                return;
            }

            if (_accounts == null || _accounts.Count == 0)
            {
                System.Windows.MessageBox.Show("沒有可用的帳號", "ROZero Loginer", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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
                    UseAccount(selectedAccount, true);
                }
            }

            _isSelectionWindowOpen = false;
        }

        private void UseAccount(Account account, bool skipAgreeButton = false)
        {
            try
            {
                var inputService = new InputService(_currentSettings);
                var settings = _dataService.GetSettings();

                var serverToUse = account.AutoSelectServer ? account.Server : 0;
                var characterToUse = account.AutoSelectCharacter ? account.Character : 0;
                var lastCharacterToUse = account.AutoSelectCharacter ? account.LastCharacter : 0;
                
                // 如果伺服器設為0（遊戲預設位置），則不執行自動選擇
                var shouldAutoSelectServer = account.AutoSelectServer && account.Server > 0;
                // 如果角色設為0（遊戲預設位置），則不執行自動選擇
                var shouldAutoSelectCharacter = account.AutoSelectCharacter && account.Character > 0;
                
                inputService.SendLogin(account.Username, account.Password, account.OtpSecret, settings.OtpInputDelayMs, settings, skipAgreeButton, 0, serverToUse, characterToUse, lastCharacterToUse, shouldAutoSelectServer, shouldAutoSelectCharacter);

                account.LastUsed = DateTime.Now;
                if (account.AutoSelectCharacter)
                {
                    account.LastCharacter = account.Character;
                }
                _dataService.SaveAccount(account);

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
                    System.Windows.MessageBox.Show($"使用帳號時發生錯誤: {ex.Message}", "錯誤", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
            }
        }

        private void TotpTimer_Tick(object sender, EventArgs e)
        {
            if (_selectedAccount != null && !string.IsNullOrEmpty(_selectedAccount.OtpSecret))
            {
                try
                {
                    var totp = _otpService.GenerateTotp(_selectedAccount.OtpSecret);
                    var remaining = _otpService.GetTimeRemaining();

                    TotpTextBox.Text = totp;
                    TotpCountdownTextBlock.Text = $"({remaining}s)";
                    CopyTotpButton.IsEnabled = true;
                }
                catch (Exception)
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
            // 處理詳細資料顯示 (使用最後選擇的項目)
            var selectedDisplayItem = AccountsDataGrid.SelectedItem as AccountDisplayItem;
            _selectedAccount = selectedDisplayItem?.Account;

            if (_selectedAccount != null)
            {
                NameTextBox.Text = _selectedAccount.Name;
                UsernameTextBox.Text = _selectedAccount.Username;

                EditAccountButton.IsEnabled = true;
            }
            else
            {
                NameTextBox.Text = "";
                UsernameTextBox.Text = "";

                EditAccountButton.IsEnabled = false;
            }

            // 更新批次操作按鈕狀態
            UpdateLaunchSelectedButtonState();
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
                        Debug.WriteLine($"Failed to save account {account.Name}: {ex.Message}");
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
                    System.Windows.MessageBox.Show($"部分帳號新增失敗\n成功：{successCount} 個\n失敗：{errorCount} 個",
                                  "批次新增結果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                        System.Windows.MessageBox.Show($"資料還原成功！已載入 {_accounts?.Count ?? 0} 個帳號。", "還原完成", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"資料還原後重新載入失敗: {ex.Message}", "錯誤", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
            }
        }



        private void CopyTotpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TotpTextBox.Text))
            {
                System.Windows.Clipboard.SetText(TotpTextBox.Text);
                StatusTextBlock.Text = "TOTP 已複製到剪貼簿";
            }
        }

        private void ViewLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logViewer = new LogViewerWindow();
                logViewer.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"開啟日誌檢視器時發生錯誤: {ex.Message}", "錯誤", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async void CheckForUpdatesOnStartup()
        {
            try
            {
                LogService.Instance.Info("開始自動檢查更新");
                var updateService = new Services.UpdateService();
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo != null && updateInfo.IsNewVersion)
                {
                    _hasNewVersion = true;
                    UpdateCheckUpdateButtonAppearance();
                    LogService.Instance.Info("發現新版本: {0}", updateInfo.Version);
                }
                else
                {
                    LogService.Instance.Info("已是最新版本");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warning("自動檢查更新失敗: {0}", ex.Message);
            }
        }

        private void UpdateCheckUpdateButtonAppearance()
        {
            if (_hasNewVersion)
            {
                CheckUpdateButton.Content = "🔴 有新版本";
                CheckUpdateButton.ToolTip = "發現新版本，點擊查看詳情";
            }
            else
            {
                CheckUpdateButton.Content = "檢查更新";
                CheckUpdateButton.ToolTip = null;
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusTextBlock.Text = "正在檢查更新...";
                CheckUpdateButton.IsEnabled = false;

                var updateService = new Services.UpdateService();
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (updateInfo == null)
                {
                    StatusTextBlock.Text = "檢查更新失敗";
                    System.Windows.MessageBox.Show("無法檢查更新，請檢查網路連線或稍後再試。", "檢查更新",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                if (updateInfo.IsNewVersion)
                {
                    _hasNewVersion = true;
                    UpdateCheckUpdateButtonAppearance();
                    StatusTextBlock.Text = $"發現新版本: {updateInfo.Version}";

                    var result = System.Windows.MessageBox.Show(
                        $"發現新版本！\n\n" +
                        $"目前版本: v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}\n" +
                        $"最新版本: {updateInfo.Version}\n" +
                        $"發布日期: {updateInfo.PublishDate:yyyy-MM-dd}\n\n" +
                        $"更新說明:\n{updateInfo.ReleaseNotes}\n\n" +
                        $"是否要前往下載頁面？",
                        "發現新版本",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        updateService.OpenDownloadPage(updateInfo.DownloadUrl);
                    }
                }
                else
                {
                    _hasNewVersion = false;
                    UpdateCheckUpdateButtonAppearance();
                    StatusTextBlock.Text = "已是最新版本";
                    System.Windows.MessageBox.Show($"目前已是最新版本 ({updateInfo.Version})", "檢查更新",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "檢查更新出錯";
                LogService.Instance.Error(ex, "檢查更新時發生錯誤");
                System.Windows.MessageBox.Show($"檢查更新時發生錯誤: {ex.Message}", "錯誤",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
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

            var result = System.Windows.MessageBox.Show(aboutMessage + "\n\n點擊「是」開啟 GitHub 頁面",
                                       "關於 ROZero Loginer",
                                       System.Windows.MessageBoxButton.YesNo,
                                       System.Windows.MessageBoxImage.Information);

            if (result == System.Windows.MessageBoxResult.Yes)
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
                    System.Windows.MessageBox.Show($"無法開啟網頁: {ex.Message}", "錯誤", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                
                // 如果是垂直模式，更新按鈕樣式
                if (_isVerticalTabMode)
                {
                    UpdateAllVerticalButtonStyles();
                }
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            AccountsDataGrid.SelectAll();
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            AccountsDataGrid.UnselectAll();
        }

        private void ToggleVerticalTabButton_Click(object sender, RoutedEventArgs e)
        {
            _isVerticalTabMode = !_isVerticalTabMode;
            CurrentSettings.UseVerticalTabLayout = _isVerticalTabMode;
            _dataService.SaveSettings(CurrentSettings);
            UpdateTabControlLayout();
        }

        private void LaunchSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsDataGrid.SelectedItems.Count == 0)
            {
                System.Windows.MessageBox.Show("請選擇要啟動的帳號", "批次啟動", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var selectedAccounts = AccountsDataGrid.SelectedItems
                .Cast<AccountDisplayItem>()
                .Select(item => item.Account)
                .ToList();

            var result = System.Windows.MessageBox.Show(
                $"確定要啟動選中的 {selectedAccounts.Count} 個帳號嗎？",
                "批次啟動",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                // 縮小視窗到工作列
                this.WindowState = WindowState.Minimized;

                Task.Run(() => BatchLaunchGames(selectedAccounts));
            }
        }

        private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (AccountsDataGrid.SelectedItems.Count == 0)
            {
                System.Windows.MessageBox.Show("請選擇要刪除的帳號", "批次刪除", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var selectedAccounts = AccountsDataGrid.SelectedItems
                .Cast<AccountDisplayItem>()
                .Select(item => item.Account)
                .ToList();

            var result = System.Windows.MessageBox.Show(
                $"確定要刪除選中的 {selectedAccounts.Count} 個帳號嗎？\n\n" +
                "此操作無法復原！",
                "批次刪除確認",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
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
                    System.Windows.MessageBox.Show($"部分帳號刪除失敗\n成功：{successCount} 個\n失敗：{errorCount} 個",
                                  "批次刪除結果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                    System.Windows.MessageBox.Show("RO 主程式路徑無效，請到設定中正確設定遊戲路徑。", "錯誤", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                System.Windows.MessageBox.Show($"啟動遊戲時發生錯誤: {ex.Message}", "錯誤", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                StatusTextBlock.Text = "遊戲啟動失敗";
            }
        }

        private async void BatchLaunchGames(List<Account> selectedAccounts)
        {
            // 清除已登入視窗記錄，確保批次啟動時有乾淨的狀態
            InputService.ClearLoggedInWindows();
            LogService.Instance.Info("[BatchLaunch] 已清除已登入視窗記錄，開始批次啟動 {0} 個帳號", selectedAccounts.Count);

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
                    System.Windows.MessageBox.Show($"批次啟動完成\n成功：{successCount} 個\n失敗：{failCount} 個",
                                  "批次啟動結果", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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

            LogService.Instance.Info("[BatchLaunch] 遊戲啟動成功 - PID: {0}, 帳號: {1}", gameProcess.Id, account.Username);

            // 主動等待遊戲視窗出現，最多等待 30 秒
            LogService.Instance.Info("[BatchLaunch] 開始等待 PID {0} 的遊戲視窗出現 - {1}", gameProcess.Id, account.Username);
            var gameWindow = InputService.WaitForRoWindowByPid(gameProcess.Id, _currentSettings, 30000, 500);

            if (gameWindow == IntPtr.Zero)
            {
                throw new Exception($"等待遊戲視窗出現超時 (PID: {gameProcess.Id})");
            }

            LogService.Instance.Info("[BatchLaunch] 遊戲視窗已出現，準備執行登入操作 - PID: {0}, 視窗: {1}, 帳號: {2}",
                gameProcess.Id, gameWindow.ToInt64(), account.Username);

            // 執行自動輸入帳號密碼（必須在主 UI 線程中執行以獲得輸入權限）
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    LogService.Instance.Info("[BatchLaunch] 在主線程中開始執行輸入操作 - {0}", account.Username);
                    var inputService = new InputService(_currentSettings);
                    
                    var serverToUse = account.AutoSelectServer ? account.Server : 0;
                    var characterToUse = account.AutoSelectCharacter ? account.Character : 0;
                    var lastCharacterToUse = account.AutoSelectCharacter ? account.LastCharacter : 0;
                    
                    // 如果伺服器設為0（遊戲預設位置），則不執行自動選擇
                    var shouldAutoSelectServer = account.AutoSelectServer && account.Server > 0;
                    // 如果角色設為0（遊戲預設位置），則不執行自動選擇
                    var shouldAutoSelectCharacter = account.AutoSelectCharacter && account.Character > 0;
                    
                    inputService.SendLogin(account.Username, account.Password, account.OtpSecret, settings.OtpInputDelayMs, settings, false, gameProcess.Id, serverToUse, characterToUse, lastCharacterToUse, shouldAutoSelectServer, shouldAutoSelectCharacter);
                    LogService.Instance.Info("[BatchLaunch] 輸入操作完成 - {0}", account.Username);
                }
                catch (Exception ex)
                {
                    LogService.Instance.Error("[BatchLaunch] 批次啟動輸入失敗 - {0}: {1}", account.Username, ex.Message);
                    throw; // 重新拋出異常以便上層處理
                }
            });

            account.LastUsed = DateTime.Now;
            if (account.AutoSelectCharacter)
            {
                account.LastCharacter = account.Character;
            }
            _dataService.SaveAccount(account);
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
                CopyTotpButton.IsEnabled = false;
                UpdateLaunchSelectedButtonState();

                // 更新視窗標題（如果版本有變化）
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
                this.Title = $"Ragnarok Online Zero 帳號管理工具 v{version}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"重新載入資料時發生錯誤: {ex.Message}", "錯誤", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }


        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LogService.Instance.Info("程序準備關閉");
            
            // 保存視窗狀態
            SaveWindowState();
            
            _totpTimer?.Stop();
            _hotkeyService?.UnregisterAllHotkeys();
            
            // 清理系統匣圖示
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
            
            LogService.Instance.Info("=== ROZero Loginer 已關閉 ===");
        }
    }
}