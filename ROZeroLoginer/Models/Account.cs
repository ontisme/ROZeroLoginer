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
        private int _server = 1;
        private int _character = 1;
        private int _lastCharacter = 1;
        private bool _autoSelectServer = false;
        private bool _autoSelectCharacter = false;
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

        public int Server
        {
            get => _server;
            set
            {
                _server = value;
                OnPropertyChanged();
            }
        }

        public int Character
        {
            get => _character;
            set
            {
                _character = value;
                OnPropertyChanged();
            }
        }

        public int LastCharacter
        {
            get => _lastCharacter;
            set
            {
                _lastCharacter = value;
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

        public bool AutoSelectServer
        {
            get => _autoSelectServer;
            set
            {
                _autoSelectServer = value;
                OnPropertyChanged();
            }
        }

        public bool AutoSelectCharacter
        {
            get => _autoSelectCharacter;
            set
            {
                _autoSelectCharacter = value;
                OnPropertyChanged();
            }
        }

        public Account()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.Now;
            LastUsed = DateTime.MinValue;
        }

        public Account(string name, string username, string password, string otpSecret, int server = 1, int character = 1, string group = "預設", bool autoSelectServer = false, bool autoSelectCharacter = false) : this()
        {
            Name = name;
            Username = username;
            Password = password;
            OtpSecret = otpSecret;
            Server = server;
            Character = character;
            Group = group;
            AutoSelectServer = autoSelectServer;
            AutoSelectCharacter = autoSelectCharacter;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}