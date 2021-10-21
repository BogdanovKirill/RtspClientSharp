using System;
using System.Globalization;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp;
using RtspClientSharp.RawFrames;
using RtspClientSharp.Rtsp;

namespace SimpleRtspPlayer.RawFramesReceiving
{
    class RawFramesSource : IRawFramesSource
    {
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
        private readonly ConnectionParameters _connectionParameters;
        private Task _workTask = Task.CompletedTask;
        private CancellationTokenSource _cancellationTokenSource;

        public EventHandler<RawFrame> FrameReceived { get; set; }
        public EventHandler<string> ConnectionStatusChanged { get; set; }

        public RawFramesSource(ConnectionParameters connectionParameters)
        {
            _connectionParameters =
                connectionParameters ?? throw new ArgumentNullException(nameof(connectionParameters));
        }

        public void Start()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            CancellationToken token = _cancellationTokenSource.Token;

            _workTask = _workTask.ContinueWith(async p =>
            {
                await ReceiveAsync(token);
            }, token);
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        private async Task ReceiveAsync(CancellationToken token)
        {
            try
            {
                DateTime initialTimestamp = await GetRtspUriDateTimeAsync(_connectionParameters.ConnectionUri.AbsoluteUri);

                using (var rtspClient = new RtspClient(_connectionParameters))
                {
                    rtspClient.FrameReceived += RtspClientOnFrameReceived;

                    while (true)
                    {
                        OnStatusChanged("Connecting...");

                        try
                        {
                            await rtspClient.ConnectAsync(initialTimestamp, token);
                        }
                        catch (InvalidCredentialException)
                        {
                            OnStatusChanged("Invalid login and/or password");
                            await Task.Delay(RetryDelay, token);
                            continue;
                        }
                        catch (RtspClientException e)
                        {
                            OnStatusChanged(e.ToString());
                            await Task.Delay(RetryDelay, token);
                            continue;
                        }

                        OnStatusChanged("Receiving frames...");

                        try
                        {
                            await rtspClient.ReceiveAsync(token);
                        }
                        catch (RtspClientException e)
                        {
                            OnStatusChanged(e.ToString());
                            await Task.Delay(RetryDelay, token);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void RtspClientOnFrameReceived(object sender, RawFrame rawFrame)
        {
            FrameReceived?.Invoke(this, rawFrame);
            OnStatusChanged(rawFrame.Timestamp.ToString());
        }

        private void OnStatusChanged(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }

        private async Task<DateTime> GetRtspUriDateTimeAsync(string rtspUri)
        {
            Regex HikvisionIsapiDateTimeFormat = new Regex(@"\d{8}\w*(t)*\w\d{6}\w*(z)*\w");
            Regex IntelbrasDateTimeFormat = new Regex(@"\d{4}\w*(_)*\w\d{2}\w*(_)*\w\d{2}\w*(_)*\w\d{2}\w*(_)*\w\d{2}\w*(_)*\w\d{2}");
            string[] stringSeparator = new string[] { "starttime=" };

            var startDate = rtspUri.Split(stringSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (String.IsNullOrEmpty(startDate[1]))
                throw new ArgumentNullException();

            DateTime initialDate = DateTime.MinValue;

            if (HikvisionIsapiDateTimeFormat.IsMatch(startDate[1]))
                initialDate = await ParseDateToTimeAsync(startDate[1].Replace("t", String.Empty).Replace("z", String.Empty));
            else if (IntelbrasDateTimeFormat.IsMatch(startDate[1]))
                initialDate = await ParseDateToTimeAsync(startDate[1].Replace("_", String.Empty));

            await Task.Delay(500);
            return initialDate;
        }

        private async Task<DateTime> ParseDateToTimeAsync(string dateTime)
        {
            DateTime convertedDateTime = DateTime.MinValue;

            await Task.Delay(500);
            DateTime.TryParseExact(dateTime, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out convertedDateTime);

            return convertedDateTime;
        }
    }
}