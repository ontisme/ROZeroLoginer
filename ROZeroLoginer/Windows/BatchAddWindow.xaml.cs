using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using ROZeroLoginer.Models;
using ROZeroLoginer.Services;
using ROZeroLoginer.Utils;

namespace ROZeroLoginer.Windows
{
    public partial class BatchAddWindow : Window
    {
        private List<Account> _parsedAccounts = new List<Account>();
        private readonly OtpService _otpService;

        public List<Account> ImportedAccounts => _parsedAccounts;

        public BatchAddWindow()
        {
            InitializeComponent();
            _otpService = new OtpService();
            LoadExistingGroups();
        }

        private void LoadExistingGroups()
        {
            try
            {
                var dataService = new DataService();
                var allAccounts = dataService.GetAccounts();
                var existingGroups = allAccounts.Select(a => a.Group).Distinct().OrderBy(g => g).ToList();

                DefaultGroupComboBox.Items.Clear();

                // 添加預設分組
                if (!existingGroups.Contains("預設"))
                {
                    DefaultGroupComboBox.Items.Add("預設");
                }

                // 添加現有分組
                foreach (var group in existingGroups)
                {
                    if (!string.IsNullOrEmpty(group))
                    {
                        DefaultGroupComboBox.Items.Add(group);
                    }
                }

                // 設定預設值
                DefaultGroupComboBox.Text = "預設";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加載分組時發生錯誤: {ex.Message}");
                DefaultGroupComboBox.Items.Add("預設");
                DefaultGroupComboBox.Text = "預設";
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _parsedAccounts.Clear();
                var inputText = AccountDataTextBox.Text?.Trim();

                if (string.IsNullOrEmpty(inputText))
                {
                    PreviewTextBlock.Text = "請輸入帳號資料";
                    ImportButton.IsEnabled = false;
                    return;
                }

                var lines = inputText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var resultBuilder = new StringBuilder();
                var successCount = 0;
                var errorCount = 0;

                resultBuilder.AppendLine($"解析結果 (共 {lines.Length} 行):");
                resultBuilder.AppendLine();

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine))
                        continue;

                    var parts = trimmedLine.Split('|');
                    if (parts.Length < 4 || parts.Length > 9)
                    {
                        errorCount++;
                        resultBuilder.AppendLine($"❌ 格式錯誤: {trimmedLine}");
                        resultBuilder.AppendLine($"   (需要4-9個欄位，實際找到 {parts.Length} 個)");
                        continue;
                    }

                    var name = parts[0].Trim();
                    var username = parts[1].Trim();
                    var password = parts[2].Trim();
                    var otpSecret = parts[3].Trim();
                    var group = parts.Length >= 5 ? parts[4].Trim() : DefaultGroupComboBox.Text?.Trim();
                    var server = 1;
                    var character = 1;
                    var autoSelectServer = false;
                    var autoSelectCharacter = false;

                    // 如果分組為空，使用預設分組
                    if (string.IsNullOrEmpty(group))
                    {
                        group = "預設";
                    }

                    // 解析伺服器
                    if (parts.Length >= 6 && !string.IsNullOrEmpty(parts[5].Trim()))
                    {
                        if (!int.TryParse(parts[5].Trim(), out server) || server < 0 || server > 4)
                        {
                            errorCount++;
                            resultBuilder.AppendLine($"❌ 無效的伺服器值: {name} (伺服器必須為0-4)");
                            continue;
                        }
                    }

                    // 解析角色
                    if (parts.Length >= 7 && !string.IsNullOrEmpty(parts[6].Trim()))
                    {
                        if (!int.TryParse(parts[6].Trim(), out character) || character < 0 || character > 15)
                        {
                            errorCount++;
                            resultBuilder.AppendLine($"❌ 無效的角色值: {name} (角色必須為0-15)");
                            continue;
                        }
                    }

                    // 解析自動選擇伺服器
                    if (parts.Length >= 8 && !string.IsNullOrEmpty(parts[7].Trim()))
                    {
                        if (!bool.TryParse(parts[7].Trim(), out autoSelectServer))
                        {
                            errorCount++;
                            resultBuilder.AppendLine($"❌ 無效的自動選擇伺服器值: {name} (必須為true或false)");
                            continue;
                        }
                    }

                    // 解析自動選擇角色
                    if (parts.Length >= 9 && !string.IsNullOrEmpty(parts[8].Trim()))
                    {
                        if (!bool.TryParse(parts[8].Trim(), out autoSelectCharacter))
                        {
                            errorCount++;
                            resultBuilder.AppendLine($"❌ 無效的自動選擇角色值: {name} (必須為true或false)");
                            continue;
                        }
                    }

                    // 驗證欄位
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(username) ||
                        string.IsNullOrEmpty(password) || string.IsNullOrEmpty(otpSecret))
                    {
                        errorCount++;
                        resultBuilder.AppendLine($"❌ 欄位不能為空: {trimmedLine}");
                        continue;
                    }

                    // 驗證 TOTP 密鑰
                    try
                    {
                        _otpService.GenerateTotp(otpSecret);
                    }
                    catch
                    {
                        errorCount++;
                        resultBuilder.AppendLine($"❌ 無效的TOTP密鑰: {name} ({otpSecret})");
                        continue;
                    }

                    // 檢查重複名稱
                    if (_parsedAccounts.Any(a => a.Name == name))
                    {
                        errorCount++;
                        resultBuilder.AppendLine($"❌ 重複的帳號名稱: {name}");
                        continue;
                    }

                    // 創建帳號
                    var account = new Account
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = name,
                        Username = username,
                        Password = password,
                        OtpSecret = otpSecret,
                        Group = group,
                        Server = server,
                        Character = character,
                        LastCharacter = character,
                        AutoSelectServer = autoSelectServer,
                        AutoSelectCharacter = autoSelectCharacter,
                        CreatedAt = DateTime.Now,
                        LastUsed = DateTime.MinValue
                    };

                    _parsedAccounts.Add(account);
                    successCount++;
                    string serverName;
                    switch (server)
                    {
                        case 0:
                            serverName = "遊戲預設位置";
                            break;
                        case 1:
                            serverName = "1";
                            break;
                        case 2:
                            serverName = "2";
                            break;
                        case 3:
                            serverName = "3";
                            break;
                        case 4:
                            serverName = "4";
                            break;
                        default:
                            serverName = "未知";
                            break;
                    }
                    
                    string characterName;
                    if (character == 0)
                    {
                        characterName = "遊戲預設位置";
                    }
                    else
                    {
                        characterName = character.ToString();
                    }
                    var autoSelectInfo = "";
                    if (autoSelectServer || autoSelectCharacter)
                    {
                        var autoSelectParts = new List<string>();
                        if (autoSelectServer) autoSelectParts.Add("自動選伺服器");
                        if (autoSelectCharacter) autoSelectParts.Add("自動選角色");
                        autoSelectInfo = $" ({string.Join(", ", autoSelectParts)})";
                    }
                    resultBuilder.AppendLine($"✅ {name} ({username}) - {group} - 伺服器{serverName} 角色{characterName}{autoSelectInfo}");
                }

                resultBuilder.AppendLine();
                resultBuilder.AppendLine($"統計: {successCount} 個成功, {errorCount} 個錯誤");

                PreviewTextBlock.Text = resultBuilder.ToString();
                ImportButton.IsEnabled = successCount > 0;
            }
            catch (Exception ex)
            {
                PreviewTextBlock.Text = $"解析時發生錯誤: {ex.Message}";
                ImportButton.IsEnabled = false;
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parsedAccounts.Count == 0)
            {
                MessageBox.Show("沒有可新增的帳號", "批次新增", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"確定要新增 {_parsedAccounts.Count} 個帳號嗎？",
                "確認批次新增",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}