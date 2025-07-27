using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ROZeroLoginer.Models
{
    public class AccountDisplayItem : INotifyPropertyChanged
    {
        private Account _account;
        private AppSettings _settings;
        private bool _isSelected;

        public Account Account
        {
            get => _account;
            set
            {
                _account = value;
                OnPropertyChanged();
                UpdateDisplayProperties();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public AppSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
                UpdateDisplayProperties();
            }
        }

        public string DisplayName => GetDisplayValue(_account?.Name, _settings?.HideNames);
        public string DisplayUsername => GetDisplayValue(_account?.Username, _settings?.HideUsernames);
        public string DisplayPassword => GetPasswordDisplay(_account?.Password, _settings?.HidePasswords);
        public string DisplaySecretKey => GetDisplayValue(_account?.OtpSecret, _settings?.HideSecretKeys);

        public AccountDisplayItem(Account account, AppSettings settings)
        {
            _account = account;
            _settings = settings;
        }

        private void UpdateDisplayProperties()
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(DisplayUsername));
            OnPropertyChanged(nameof(DisplayPassword));
            OnPropertyChanged(nameof(DisplaySecretKey));
        }

        private string GetDisplayValue(string original, bool? shouldHide)
        {
            if (string.IsNullOrEmpty(original) || _settings?.PrivacyModeEnabled != true || shouldHide != true)
                return original ?? "";

            if (original.Length <= 2)
                return new string('*', original.Length);

            return original[0] + new string('*', original.Length - 2) + original[original.Length - 1];
        }

        private string GetPasswordDisplay(string original, bool? shouldHide)
        {
            if (string.IsNullOrEmpty(original) || _settings?.PrivacyModeEnabled != true || shouldHide != true)
                return original ?? "";

            return new string('*', System.Math.Max(8, original.Length));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}