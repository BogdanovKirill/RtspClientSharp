using System;
using RtspClientSharp;
using SimpleRtspPlayer.RawFramesReceiving;

namespace SimpleRtspPlayer.GUI.Models
{
    class MainWindowModel : IMainWindowModel
    {
        private readonly RealtimeVideoSource _realtimeVideoSource = new RealtimeVideoSource();
        private RawFramesSource _rawFramesSource;

        public event EventHandler<string> StatusChanged;

        public IVideoSource VideoSource => _realtimeVideoSource;

        public void Start(ConnectionParameters connectionParameters)
        {
            _rawFramesSource = new RawFramesSource(connectionParameters);
            _rawFramesSource.ConnectionStatusChanged += ConnectionStatusChanged;

            _realtimeVideoSource.SetRawFramesSource(_rawFramesSource);

            _rawFramesSource.Start();
        }

        public void Stop()
        {
            _rawFramesSource.Stop();
            _realtimeVideoSource.SetRawFramesSource(null);
        }

        private void ConnectionStatusChanged(object sender, string s)
        {
            StatusChanged?.Invoke(this, s);
        }
    }
}