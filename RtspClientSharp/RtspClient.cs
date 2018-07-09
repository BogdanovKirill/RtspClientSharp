using System;
using System.Net;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using RtspClientSharp.RawFrames;
using RtspClientSharp.Rtsp;
using RtspClientSharp.Utils;

namespace RtspClientSharp
{
    public sealed class RtspClient : IRtspClient
    {
        private readonly Func<IRtspTransportClient> _transportClientProvider;
        private IRtspTransportClient _rtspTransportClient;
        private bool _anyTpktPacketReceived;
        private int _disposed;
        private readonly RtspClientInternal _rtspClientInternal;

        public ConnectionParameters ConnectionParameters { get; }

        public event EventHandler<RawFrame> FrameReceived;

        public RtspClient(ConnectionParameters connectionParameters)
        {
            ConnectionParameters = connectionParameters ??
                                   throw new ArgumentNullException(nameof(connectionParameters));

            _transportClientProvider = CreateTransportClient;
            _rtspClientInternal = CreateRtspClientInternal(connectionParameters);
        }

        internal RtspClient(ConnectionParameters connectionParameters,
            Func<IRtspTransportClient> transportClientProvider)
        {
            ConnectionParameters = connectionParameters ??
                                   throw new ArgumentNullException(nameof(connectionParameters));

            _transportClientProvider = transportClientProvider ??
                                       throw new ArgumentNullException(nameof(transportClientProvider));

            _rtspClientInternal = CreateRtspClientInternal(connectionParameters);
        }

        ~RtspClient()
        {
            Dispose();
        }

        /// <summary>
        /// Connect to endpoint and start RTSP session
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="InvalidCredentialException"></exception>
        /// <exception cref="RtspClientException"></exception>
        public async Task ConnectAsync(CancellationToken token)
        {
            await Task.Run(async () =>
            {
                _rtspTransportClient = _transportClientProvider();

                try
                {
                    Task connectionTask = ConnectInternalAsync(token);

                    if (connectionTask.IsCompleted)
                    {
                        await connectionTask;
                        return;
                    }

                    var delayTaskCancelTokenSource = new CancellationTokenSource();
                    using (var linkedTokenSource =
                        CancellationTokenSource.CreateLinkedTokenSource(delayTaskCancelTokenSource.Token, token))
                    {
                        CancellationToken delayTaskToken = linkedTokenSource.Token;

                        Task delayTask = Task.Delay(ConnectionParameters.ConnectTimeout, delayTaskToken);

                        if (connectionTask != await Task.WhenAny(connectionTask, delayTask))
                        {
                            connectionTask.IgnoreExceptions();

                            if (delayTask.Status == TaskStatus.Canceled)
                                throw new OperationCanceledException();

                            throw new TimeoutException();
                        }

                        delayTaskCancelTokenSource.Cancel();
                        await connectionTask;
                    }
                }
                catch (Exception e)
                {
                    _rtspTransportClient.Dispose();
                    _rtspTransportClient = null;

                    if (e is TimeoutException)
                        throw new RtspClientException("Connection timeout", e);

                    if (e is OperationCanceledException)
                        throw;

                    if (e is RtspBadResponseCodeException rtspBadResponseCodeException &&
                        rtspBadResponseCodeException.Code == RtspStatusCode.Unauthorized ||
                        e is HttpBadResponseCodeException httpBadResponseCodeException &&
                        httpBadResponseCodeException.Code == HttpStatusCode.Unauthorized)
                        throw new InvalidCredentialException("Invalid login and/or password");

                    if (!(e is RtspClientException))
                        throw new RtspClientException("Connection error", e);

                    throw;
                }
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Receive frames. 
        /// Should be called after successful connection to endpoint or InvalidOperationException will be thrown
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="RtspClientException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task ReceiveAsync(CancellationToken token)
        {
            if (_rtspTransportClient == null)
                throw new InvalidOperationException("Client should be connected first");

            try
            {
                Task receiveInternalTask = _rtspClientInternal.ReceiveAsync(_rtspTransportClient, token);

                if (receiveInternalTask.IsCompleted)
                {
                    await receiveInternalTask;
                    return;
                }

                var delayTaskCancelTokenSource = new CancellationTokenSource();
                using (var linkedTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(delayTaskCancelTokenSource.Token, token))
                {
                    CancellationToken delayTaskToken = linkedTokenSource.Token;

                    while (true)
                    {
                        _anyTpktPacketReceived = false;

                        Task result = await Task.WhenAny(receiveInternalTask,
                            Task.Delay(ConnectionParameters.ReceiveTimeout, delayTaskToken));

                        if (result == receiveInternalTask)
                        {
                            delayTaskCancelTokenSource.Cancel();
                            await receiveInternalTask;
                            break;
                        }

                        if (result.IsCanceled)
                        {
                            await Task.WhenAny(receiveInternalTask,
                                Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None));

                            receiveInternalTask.IgnoreExceptions();
                            throw new OperationCanceledException();
                        }

                        if (!Volatile.Read(ref _anyTpktPacketReceived))
                        {
                            receiveInternalTask.IgnoreExceptions();
                            throw new RtspClientException("Receive timeout", new TimeoutException());
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (RtspClientException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new RtspClientException("Receive error", e);
            }
            finally
            {
                _rtspTransportClient.Dispose();
                _rtspTransportClient = null;
            }
        }

        /// <summary>
        /// Clean up unmanaged resources
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                return;

            IRtspTransportClient client = _rtspTransportClient;

            if (client != null)
                client.Dispose();

            GC.SuppressFinalize(this);
        }

        private IRtspTransportClient CreateTransportClient()
        {
            if (ConnectionParameters.ConnectionUri.Scheme.Equals(Uri.UriSchemeHttp,
                StringComparison.InvariantCultureIgnoreCase))
                return new RtspHttpTransportClient(ConnectionParameters);

            return new RtspTcpTransportClient(ConnectionParameters);
        }

        private async Task ConnectInternalAsync(CancellationToken token)
        {
            await _rtspTransportClient.ConnectAsync(token);

            await _rtspClientInternal.ConnectAsync(_rtspTransportClient, token);
        }

        private RtspClientInternal CreateRtspClientInternal(ConnectionParameters connectionParameters)
        {
            return new RtspClientInternal(connectionParameters)
            {
                ReadingContinues = () => Volatile.Write(ref _anyTpktPacketReceived, true),
                FrameReceived = frame => FrameReceived?.Invoke(this, frame)
            };
        }
    }
}