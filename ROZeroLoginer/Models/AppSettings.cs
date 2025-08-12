using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace ROZeroLoginer.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private Keys _hotkey = Keys.Home;
        private bool _hotkeyEnabled = true;
        private bool _startWithWindows = false;
        private bool _showNotifications = true;
        private int _otpValiditySeconds = 30;
        private int _otpInputDelayMs = 2000;
        private bool _privacyModeEnabled = true;
        private bool _hideNames = false;
        private bool _hideUsernames = false;
        private bool _hidePasswords = true;
        private bool _hideSecretKeys = true;
        private string _roGamePath = @"C:\Gravity\RagnarokZero\Ragexe.exe";
        private List<string> _gameTitles;

        public Keys Hotkey
        {
            get => _hotkey;
            set
            {
                _hotkey = value;
                OnPropertyChanged();
            }
        }

        public bool HotkeyEnabled
        {
            get => _hotkeyEnabled;
            set
            {
                _hotkeyEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                _startWithWindows = value;
                OnPropertyChanged();
            }
        }

        public bool ShowNotifications
        {
            get => _showNotifications;
            set
            {
                _showNotifications = value;
                OnPropertyChanged();
            }
        }

        public int OtpValiditySeconds
        {
            get => _otpValiditySeconds;
            set
            {
                _otpValiditySeconds = value;
                OnPropertyChanged();
            }
        }

        public int OtpInputDelayMs
        {
            get => _otpInputDelayMs;
            set
            {
                _otpInputDelayMs = value;
                OnPropertyChanged();
            }
        }

        public bool PrivacyModeEnabled
        {
            get => _privacyModeEnabled;
            set
            {
                _privacyModeEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool HideNames
        {
            get => _hideNames;
            set
            {
                _hideNames = value;
                OnPropertyChanged();
            }
        }

        public bool HideUsernames
        {
            get => _hideUsernames;
            set
            {
                _hideUsernames = value;
                OnPropertyChanged();
            }
        }

        public bool HidePasswords
        {
            get => _hidePasswords;
            set
            {
                _hidePasswords = value;
                OnPropertyChanged();
            }
        }

        public bool HideSecretKeys
        {
            get => _hideSecretKeys;
            set
            {
                _hideSecretKeys = value;
                OnPropertyChanged();
            }
        }

        public string RoGamePath
        {
            get => _roGamePath;
            set
            {
                _roGamePath = value;
                OnPropertyChanged();
            }
        }

        public List<string> GameTitles
        {
            get => _gameTitles ?? (_gameTitles = new List<string>());
            set
            {
                _gameTitles = value ?? new List<string>();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 獲取有效的遊戲標題列表，如果為空則返回預設標題
        /// </summary>
        public List<string> GetEffectiveGameTitles()
        {
            var titles = GameTitles;
            return titles.Count > 0 ? titles : new List<string> { "Ragnarok", "Ragnarok : Zero" };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}