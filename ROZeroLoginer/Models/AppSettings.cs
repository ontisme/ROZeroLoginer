using System;
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}