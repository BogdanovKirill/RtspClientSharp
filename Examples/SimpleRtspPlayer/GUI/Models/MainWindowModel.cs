using System;
using RtspClientSharp;
using SimpleRtspPlayer.RawFramesReceiving;

namespace SimpleRtspPlayer.GUI.Models
{
    class MainWindowModel : IMainWindowModel
    {
        private readonly RealtimeVideoSource _realtimeVideoSource = new RealtimeVideoSource();
        private readonly RealtimeAudioSource _realtimeAudioSource = new RealtimeAudioSource();

        private IRawFramesSource _rawFramesSource;

        public event EventHandler<string> StatusChanged;

        public IVideoSource VideoSource => _realtimeVideoSource;

        public void Start(ConnectionParameters connectionParameters)
        {
            if (_rawFramesSource != null)
                return;

            _rawFramesSource = new RawFramesSource(connectionParameters);
            _rawFramesSource.ConnectionStatusChanged += ConnectionStatusChanged;

            _realtimeVideoSource.SetRawFramesSource(_rawFramesSource);
            _realtimeAudioSource.SetRawFramesSource(_rawFramesSource);

            _rawFramesSource.Start();
        }

        public void Stop()
        {
            if (_rawFramesSource == null)
                return;

            _rawFramesSource.Stop();
            _realtimeVideoSource.SetRawFramesSource(null);
            _rawFramesSource = null;
        }

        private void ConnectionStatusChanged(object sender, string s)
        {
            StatusChanged?.Invoke(this, s);
        }
    }
}