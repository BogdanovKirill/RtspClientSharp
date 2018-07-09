using System;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using RtspClientSharp;
using SimpleRtspPlayer.GUI.Commands;
using SimpleRtspPlayer.GUI.Models;

namespace SimpleRtspPlayer.GUI.ViewModels
{
    class MainWindowViewModel : INotifyPropertyChanged
    {
        private const string RtspPrefix = "rtsp://";
        private const string HttpPrefix = "http://";

        private string _status = string.Empty;
        private readonly IMainWindowModel _mainWindowModel;

        public string DeviceAddress { get; set; } = "rtsp://192.168.1.77:554/ucast/11";

        public string Login { get; set; } = "admin";
        public string Password { get; set; } = "123456";

        public IVideoSource VideoSource => _mainWindowModel.VideoSource;

        private readonly CommandHandler _startClickCommand;
        public ICommand StartClickCommand => _startClickCommand;

        private readonly CommandHandler _stopClickCommand;
        public ICommand StopClickCommand => _stopClickCommand;

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel(IMainWindowModel mainWindowModel)
        {
            _mainWindowModel = mainWindowModel ?? throw new ArgumentNullException(nameof(mainWindowModel));

            _startClickCommand = new CommandHandler(OnStartButtonClick, true);
            _stopClickCommand = new CommandHandler(OnStopButtonClick, false);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnStartButtonClick()
        {
            string address = DeviceAddress;

            if (!address.StartsWith(RtspPrefix) && !address.StartsWith(HttpPrefix))
                address = RtspPrefix + address;

            if (!Uri.TryCreate(address, UriKind.Absolute, out Uri deviceUri))
            {
                MessageBox.Show("Invalid device address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var credential = new NetworkCredential(Login, Password);
            var connectionParameters = new ConnectionParameters(deviceUri, credential);

            _mainWindowModel.Start(connectionParameters);
            _mainWindowModel.StatusChanged += MainWindowModelOnStatusChanged;

            _startClickCommand.SetCanExecute(false);
            _stopClickCommand.SetCanExecute(true);
        }

        private void OnStopButtonClick()
        {
            _mainWindowModel.Stop();
            _mainWindowModel.StatusChanged -= MainWindowModelOnStatusChanged;

            _stopClickCommand.SetCanExecute(false);
            _startClickCommand.SetCanExecute(true);
            Status = string.Empty;
        }

        private void MainWindowModelOnStatusChanged(object sender, string s)
        {
            Application.Current.Dispatcher.Invoke(() => Status = s);
        }
    }
}