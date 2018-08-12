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
        private bool _anyFrameReceived;
        private RtspClientInternal _rtspClientInternal;
        private int _disposed;

        public ConnectionParameters ConnectionParameters { get; }

        public event EventHandler<RawFrame> FrameReceived;

        public RtspClient(ConnectionParameters connectionParameters)
        {
            ConnectionParameters = connectionParameters ??
                                   throw new ArgumentNullException(nameof(connectionParameters));
        }

        internal RtspClient(ConnectionParameters connectionParameters,
            Func<IRtspTransportClient> transportClientProvider)
        {
            ConnectionParameters = connectionParameters ??
                                   throw new ArgumentNullException(nameof(connectionParameters));
            _transportClientProvider = transportClientProvider ??
                                       throw new ArgumentNullException(nameof(transportClientProvider));
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
                _rtspClientInternal = CreateRtspClientInternal(ConnectionParameters, _transportClientProvider);

                try
                {
                    Task connectionTask = _rtspClientInternal.ConnectAsync(token);

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

                            if (delayTask.IsCanceled)
                                throw new OperationCanceledException();

                            throw new TimeoutException();
                        }

                        delayTaskCancelTokenSource.Cancel();
                        await connectionTask;
                    }
                }
                catch (Exception e)
                {
                    _rtspClientInternal.Dispose();
                    Volatile.Write(ref _rtspClientInternal, null);

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
            if (_rtspClientInternal == null)
                throw new InvalidOperationException("Client should be connected first");

            try
            {
                Task receiveInternalTask = _rtspClientInternal.ReceiveAsync(token);

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
                        _anyFrameReceived = false;

                        Task result = await Task.WhenAny(receiveInternalTask,
                            Task.Delay(ConnectionParameters.ReceiveTimeout, delayTaskToken)).ConfigureAwait(false);

                        if (result == receiveInternalTask)
                        {
                            delayTaskCancelTokenSource.Cancel();
                            await receiveInternalTask;
                            break;
                        }

                        if (result.IsCanceled)
                        {
                            if (ConnectionParameters.CancelTimeout == TimeSpan.Zero ||
                                await Task.WhenAny(receiveInternalTask,
                                    Task.Delay(ConnectionParameters.CancelTimeout, CancellationToken.None)) !=
                                receiveInternalTask)
                                _rtspClientInternal.Dispose();

                            await Task.WhenAny(receiveInternalTask);
                            throw new OperationCanceledException();
                        }

                        if (!Volatile.Read(ref _anyFrameReceived))
                        {
                            receiveInternalTask.IgnoreExceptions();
                            throw new RtspClientException("Receive timeout", new TimeoutException());
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
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
                _rtspClientInternal.Dispose();
                Volatile.Write(ref _rtspClientInternal, null);
            }
        }

        /// <summary>
        /// Clean up unmanaged resources
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            RtspClientInternal rtspClientInternal = Volatile.Read(ref _rtspClientInternal);

            if (rtspClientInternal != null)
                rtspClientInternal.Dispose();

            GC.SuppressFinalize(this);
        }

        private RtspClientInternal CreateRtspClientInternal(ConnectionParameters connectionParameters,
            Func<IRtspTransportClient> transportClientProvider)
        {
            return new RtspClientInternal(connectionParameters, transportClientProvider)
            {
                FrameReceived = frame =>
                {
                    Volatile.Write(ref _anyFrameReceived, true);
                    FrameReceived?.Invoke(this, frame);
                }
            };
        }
    }
}