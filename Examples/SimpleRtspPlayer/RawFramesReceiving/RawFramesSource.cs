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
                var (IsSetTimestampInClock, InitialTimestamp) = await GetRtspUriDateTimeAsync(_connectionParameters.ConnectionUri.AbsoluteUri);
        
                using (var rtspClient = new RtspClient(_connectionParameters))
                {
                    rtspClient.FrameReceived += RtspClientOnFrameReceived;

                    while (true)
                    {
                        OnStatusChanged("Connecting...");

                        try
                        {
                            await rtspClient.ConnectAsync(new RtspRequestParams
                            {
                                InitialTimestamp = InitialTimestamp,
                                IsSetTimestampInClock = IsSetTimestampInClock,
                                Token = token
                            })
                                .ConfigureAwait(false);
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

        private async Task<(bool IsSetTimestampInClock, DateTime? InitialTimestamp)> GetRtspUriDateTimeAsync(string rtspUri)
        {

            return await Task.Run<(bool IsSetTimestampInClock, DateTime? InitialTimestamp)>(() =>
                 {
                     DateTime? dateTime = null;
                     if (TryParseMotorolaDatetime(rtspUri, out dateTime))
                         return (false, dateTime);

                     if (TryParseIntelbrasAndHikvisionIsapi(rtspUri, out dateTime))
                         return (true, dateTime);

                     throw new ArgumentException("DateTime invalid");
                 });
        }

        private bool TryParseIntelbrasAndHikvisionIsapi(string rtspUri, out DateTime? dateTime)
        {
            dateTime = null;

            if (!rtspUri.Contains("starttime="))
                return false;

            Regex HikvisionIsapiDateTimeFormat = new Regex(@"\d{8}\w*(t)*\w\d{6}\w*(z)*\w");

            Regex IntelbrasDateTimeFormat = new Regex(@"\d{4}\w*(_)*\w\d{2}\w*(_)*\w\d{2}\w*(_)*\w\d{2}\w*(_)*\w\d{2}\w*(_)*\w\d{2}");
            string[] stringSeparator = new string[] { "starttime=" };

            var startDate = rtspUri.Split(stringSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (String.IsNullOrEmpty(startDate[1]))
                throw new ArgumentNullException();

            DateTime initialDate = DateTime.MinValue;

            if (HikvisionIsapiDateTimeFormat.IsMatch(startDate[1]))
                initialDate = ParseDateToTime(startDate[1].Replace("t", String.Empty).Replace("z", String.Empty));
            else if (IntelbrasDateTimeFormat.IsMatch(startDate[1]))
                initialDate = ParseDateToTime(startDate[1].Replace("_", String.Empty));
            dateTime = initialDate;
            return true;
        }

        private bool TryParseMotorolaDatetime(string rtspUri, out DateTime? dateTime)
        {
            dateTime = null;

            Regex MotorolaDate = new Regex(@"\d{4}\W*(-)*\W\d{2}\W*(-)*\W\d{2}");
            Regex MotorolaTime = new Regex(@"\d{2}\W*(:)*\W\d{2}\W*(:)*\W\d{2}");

            //rtsp://corseg0162.ddns.net:3002/chID=1&date=2022-05-06&time=19:43:50&timelen=20&action=playback
            var dateSeparator = "date=";
            var timeSeparator = "&time=";
            var validUri = rtspUri.Contains(dateSeparator) && rtspUri.Contains("time=");

            if (!validUri)
                return false;

            var dateSplited = rtspUri.Split(new string[] { dateSeparator, timeSeparator, "&timelen" }, StringSplitOptions.RemoveEmptyEntries);
            var isValidRegex = MotorolaDate.IsMatch(dateSplited[1]) && MotorolaTime.IsMatch(dateSplited[2]);

            if (!isValidRegex)
                return false;

            if (!DateTime.TryParseExact(
                dateSplited[1] + dateSplited[2], "yyyy-MM-ddHH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal,
                out var dateTimeConverted))
                return false;

            dateTime = dateTimeConverted;
            return true;
        }

        private DateTime ParseDateToTime(string dateTime)
        {
            DateTime convertedDateTime = DateTime.MinValue;

            DateTime.TryParseExact(dateTime, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out convertedDateTime);

            return convertedDateTime;
        }
    }
}