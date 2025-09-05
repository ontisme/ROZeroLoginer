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
        private int _characterSelectionDelayMs = 50;
        private int _serverSelectionDelayMs = 50;
        private int _keyboardInputDelayMs = 100;
        private int _mouseClickDelayMs = 200;
        private int _generalOperationDelayMs = 500;
        private bool _minimizeToTray = false;
        private double _windowWidth = 800;
        private double _windowHeight = 600;
        private double _windowLeft = -1;
        private double _windowTop = -1;
        private bool _windowMaximized = false;
        private bool _useVerticalTabLayout = false;

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

        public int CharacterSelectionDelayMs
        {
            get => _characterSelectionDelayMs;
            set
            {
                _characterSelectionDelayMs = value;
                OnPropertyChanged();
            }
        }

        public int ServerSelectionDelayMs
        {
            get => _serverSelectionDelayMs;
            set
            {
                _serverSelectionDelayMs = value;
                OnPropertyChanged();
            }
        }

        public int KeyboardInputDelayMs
        {
            get => _keyboardInputDelayMs;
            set
            {
                _keyboardInputDelayMs = value;
                OnPropertyChanged();
            }
        }

        public int MouseClickDelayMs
        {
            get => _mouseClickDelayMs;
            set
            {
                _mouseClickDelayMs = value;
                OnPropertyChanged();
            }
        }

        public int GeneralOperationDelayMs
        {
            get => _generalOperationDelayMs;
            set
            {
                _generalOperationDelayMs = value;
                OnPropertyChanged();
            }
        }

        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                _minimizeToTray = value;
                OnPropertyChanged();
            }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set
            {
                _windowWidth = value;
                OnPropertyChanged();
            }
        }

        public double WindowHeight
        {
            get => _windowHeight;
            set
            {
                _windowHeight = value;
                OnPropertyChanged();
            }
        }

        public double WindowLeft
        {
            get => _windowLeft;
            set
            {
                _windowLeft = value;
                OnPropertyChanged();
            }
        }

        public double WindowTop
        {
            get => _windowTop;
            set
            {
                _windowTop = value;
                OnPropertyChanged();
            }
        }

        public bool WindowMaximized
        {
            get => _windowMaximized;
            set
            {
                _windowMaximized = value;
                OnPropertyChanged();
            }
        }

        public bool UseVerticalTabLayout
        {
            get => _useVerticalTabLayout;
            set
            {
                _useVerticalTabLayout = value;
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