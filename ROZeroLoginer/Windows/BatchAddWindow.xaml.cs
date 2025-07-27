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
        private readonly TotpGenerator _totpGenerator;

        public List<Account> ImportedAccounts => _parsedAccounts;

        public BatchAddWindow()
        {
            InitializeComponent();
            _totpGenerator = new TotpGenerator();
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
                    if (parts.Length != 4 && parts.Length != 5)
                    {
                        errorCount++;
                        resultBuilder.AppendLine($"❌ 格式錯誤: {trimmedLine}");
                        resultBuilder.AppendLine($"   (需要4或5個欄位，實際找到 {parts.Length} 個)");
                        continue;
                    }

                    var name = parts[0].Trim();
                    var username = parts[1].Trim();
                    var password = parts[2].Trim();
                    var otpSecret = parts[3].Trim();
                    var group = parts.Length == 5 ? parts[4].Trim() : DefaultGroupComboBox.Text?.Trim();

                    // 如果分組為空，使用預設分組
                    if (string.IsNullOrEmpty(group))
                    {
                        group = "預設";
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
                        _totpGenerator.GenerateTotp(otpSecret);
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
                        CreatedAt = DateTime.Now,
                        LastUsed = DateTime.MinValue
                    };

                    _parsedAccounts.Add(account);
                    successCount++;
                    resultBuilder.AppendLine($"✅ {name} ({username}) - {group}");
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