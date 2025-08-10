using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using ROZeroLoginer.Models;

namespace ROZeroLoginer.Services
{
    public class DataService
    {
        private readonly string _dataFilePath;
        private readonly string _settingsFilePath;
        private string _encryptionKey;
        private List<Account> _accounts;
        private AppSettings _settings;

        public DataService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ROZeroLoginer");
            Directory.CreateDirectory(appDataPath);

            _dataFilePath = Path.Combine(appDataPath, "accounts.dat");
            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
            _encryptionKey = GenerateOrGetEncryptionKey();
            
            LoadData();
            LoadSettings();
        }

        public List<Account> GetAccounts()
        {
            return _accounts ?? new List<Account>();
        }

        public AppSettings GetSettings()
        {
            return _settings ?? new AppSettings();
        }

        public void SaveAccount(Account account)
        {
            if (_accounts == null) _accounts = new List<Account>();

            var existingAccount = _accounts.FirstOrDefault(a => a.Id == account.Id);
            if (existingAccount != null)
            {
                existingAccount.Name = account.Name;
                existingAccount.Username = account.Username;
                existingAccount.Password = account.Password;
                existingAccount.OtpSecret = account.OtpSecret;
                existingAccount.Server = account.Server;
                existingAccount.Character = account.Character;
                existingAccount.LastCharacter = account.LastCharacter;
                existingAccount.AutoAssistBattle = account.AutoAssistBattle;
                existingAccount.LastUsed = account.LastUsed;
            }
            else
            {
                _accounts.Add(account);
            }

            SaveData();
        }

        public void DeleteAccount(string accountId)
        {
            if (_accounts == null) return;

            var account = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                _accounts.Remove(account);
                SaveData();
            }
        }

        public void UpdateAccountLastUsed(string accountId)
        {
            if (_accounts == null) return;

            var account = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.LastUsed = DateTime.Now;
                SaveData();
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            _settings = settings;
            SaveSettingsToFile();
        }

        public void ForceReload()
        {
            _encryptionKey = GenerateOrGetEncryptionKey();
            LoadData();
            LoadSettings();
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var encryptedData = File.ReadAllBytes(_dataFilePath);
                    var decryptedData = DecryptData(encryptedData);
                    _accounts = JsonConvert.DeserializeObject<List<Account>>(decryptedData) ?? new List<Account>();
                }
                else
                {
                    _accounts = new List<Account>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
                _accounts = new List<Account>();
            }
        }

        private void SaveData()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
                var encryptedData = EncryptData(json);
                File.WriteAllBytes(_dataFilePath, encryptedData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                _settings = new AppSettings();
            }
        }

        private void SaveSettingsToFile()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private string GenerateOrGetEncryptionKey()
        {
            var keyFilePath = Path.Combine(Path.GetDirectoryName(_dataFilePath), "key.dat");
            
            if (File.Exists(keyFilePath))
            {
                return File.ReadAllText(keyFilePath);
            }
            else
            {
                var key = GenerateRandomKey();
                File.WriteAllText(keyFilePath, key);
                return key;
            }
        }

        private string GenerateRandomKey()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var keyBytes = new byte[32];
                rng.GetBytes(keyBytes);
                return Convert.ToBase64String(keyBytes);
            }
        }

        private byte[] EncryptData(string data)
        {
            var keyBytes = Convert.FromBase64String(_encryptionKey);
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                {
                    var dataBytes = Encoding.UTF8.GetBytes(data);
                    var encryptedData = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
                    
                    var result = new byte[aes.IV.Length + encryptedData.Length];
                    Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
                    Array.Copy(encryptedData, 0, result, aes.IV.Length, encryptedData.Length);
                    
                    return result;
                }
            }
        }

        private string DecryptData(byte[] encryptedData)
        {
            var keyBytes = Convert.FromBase64String(_encryptionKey);
            using (var aes = Aes.Create())
            {
                aes.Key = keyBytes;
                
                var iv = new byte[16];
                Array.Copy(encryptedData, 0, iv, 0, 16);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                {
                    var dataBytes = new byte[encryptedData.Length - 16];
                    Array.Copy(encryptedData, 16, dataBytes, 0, dataBytes.Length);
                    
                    var decryptedData = decryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
                    return Encoding.UTF8.GetString(decryptedData);
                }
            }
        }
    }
}