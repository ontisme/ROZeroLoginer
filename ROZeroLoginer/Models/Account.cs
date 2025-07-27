using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ROZeroLoginer.Models
{
    public class Account : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private string _username;
        private string _password;
        private string _otpSecret;
        private string _group;
        private DateTime _createdAt;
        private DateTime _lastUsed;

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
            }
        }

        public string OtpSecret
        {
            get => _otpSecret;
            set
            {
                _otpSecret = value;
                OnPropertyChanged();
            }
        }

        public string Group
        {
            get => _group ?? "預設";
            set
            {
                _group = value;
                OnPropertyChanged();
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                _createdAt = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastUsed
        {
            get => _lastUsed;
            set
            {
                _lastUsed = value;
                OnPropertyChanged();
            }
        }

        public Account()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.Now;
            LastUsed = DateTime.MinValue;
        }

        public Account(string name, string username, string password, string otpSecret, string group = "預設") : this()
        {
            Name = name;
            Username = username;
            Password = password;
            OtpSecret = otpSecret;
            Group = group;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}