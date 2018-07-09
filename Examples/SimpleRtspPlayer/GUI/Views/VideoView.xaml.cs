using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SimpleRtspPlayer.RawFramesDecoding.DecodedFrames;

namespace SimpleRtspPlayer.GUI.Views
{
    /// <summary>
    /// Interaction logic for VideoView.xaml
    /// </summary>
    public partial class VideoView
    {
        private static readonly Color DefaultFillColor = Colors.Black;
        private static readonly TimeSpan ResizeHandleTimeout = TimeSpan.FromMilliseconds(500);

        private Color _fillColor = DefaultFillColor;
        private WriteableBitmap _writeableBitmap;

        private int _width;
        private int _height;
        private Int32Rect _dirtyRect;
        private readonly Action<IDecodedVideoFrame> _invalidateAction;

        private Task _handleSizeChangedTask = Task.CompletedTask;
        private CancellationTokenSource _resizeCancellationTokenSource = new CancellationTokenSource();

        public static readonly DependencyProperty VideoSourceProperty = DependencyProperty.Register(nameof(VideoSource),
            typeof(IVideoSource),
            typeof(VideoView),
            new FrameworkPropertyMetadata(OnVideoSourceChanged));

        public static readonly DependencyProperty FillColorProperty = DependencyProperty.Register(nameof(FillColor),
            typeof(Color),
            typeof(VideoView),
            new FrameworkPropertyMetadata(DefaultFillColor, OnFillColorPropertyChanged));

        public IVideoSource VideoSource
        {
            get => (IVideoSource) GetValue(VideoSourceProperty);
            set => SetValue(VideoSourceProperty, value);
        }

        public Color FillColor
        {
            get => (Color) GetValue(FillColorProperty);
            set => SetValue(FillColorProperty, value);
        }

        public VideoView()
        {
            InitializeComponent();
            _invalidateAction = Invalidate;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            int newWidth = (int) constraint.Width;
            int newHeight = (int) constraint.Height;

            if (_width != newWidth || _height != newHeight)
            {
                _resizeCancellationTokenSource.Cancel();
                _resizeCancellationTokenSource = new CancellationTokenSource();

                _handleSizeChangedTask = _handleSizeChangedTask.ContinueWith(prev =>
                    HandleSizeChangedAsync(newWidth, newHeight, _resizeCancellationTokenSource.Token));
            }

            return base.MeasureOverride(constraint);
        }

        private async Task HandleSizeChangedAsync(int width, int height, CancellationToken token)
        {
            try
            {
                await Task.Delay(ResizeHandleTimeout, token).ConfigureAwait(false);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ReinitializeBitmap(width, height);

                    IVideoSource source = VideoSource;
                    source?.SetVideoSize(width, height);
                }, DispatcherPriority.Send, token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void ReinitializeBitmap(int width, int height)
        {
            _width = width;
            _height = height;
            _dirtyRect = new Int32Rect(0, 0, width, height);

            _writeableBitmap = new WriteableBitmap(
                width,
                height,
                ScreenInfo.DpiX,
                ScreenInfo.DpiY,
                PixelFormats.Bgr24,
                null);

            RenderOptions.SetBitmapScalingMode(_writeableBitmap, BitmapScalingMode.NearestNeighbor);

            _writeableBitmap.Lock();

            try
            {
                UpdateBackgroundColor(_writeableBitmap.BackBuffer, _writeableBitmap.BackBufferStride);
                _writeableBitmap.AddDirtyRect(_dirtyRect);
            }
            finally
            {
                _writeableBitmap.Unlock();
            }

            VideoImage.Source = _writeableBitmap;
        }

        private static void OnVideoSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (VideoView) d;

            if (e.OldValue is IVideoSource oldVideoSource)
                oldVideoSource.FrameReceived -= view.OnFrameReceived;

            if (e.NewValue is IVideoSource newVideoSource)
            {
                newVideoSource.SetVideoSize(view._width, view._height);
                newVideoSource.FrameReceived += view.OnFrameReceived;
            }
        }

        private void OnFrameReceived(object sender, IDecodedVideoFrame decodedFrame)
        {
            Application.Current.Dispatcher.Invoke(_invalidateAction, DispatcherPriority.Send, decodedFrame);
        }

        private void Invalidate(IDecodedVideoFrame decodedVideoFrame)
        {
            if (decodedVideoFrame.Width != _width || decodedVideoFrame.Height != _height)
                return;

            _writeableBitmap.Lock();

            try
            {
                if (decodedVideoFrame.Stride == _writeableBitmap.BackBufferStride)
                {
                    Debug.Assert(decodedVideoFrame.DecodedBytes.Array != null,
                        "decodedVideoFrame.DecodedBytes.Array != null");

                    Marshal.Copy(decodedVideoFrame.DecodedBytes.Array, decodedVideoFrame.DecodedBytes.Offset,
                        _writeableBitmap.BackBuffer, decodedVideoFrame.DecodedBytes.Count);
                }
                else
                {
                    IntPtr backBufferPtr = _writeableBitmap.BackBuffer;
                    int srcOffset = decodedVideoFrame.DecodedBytes.Offset;

                    for (int i = 0; i < decodedVideoFrame.Height; i++)
                    {
                        Debug.Assert(decodedVideoFrame.DecodedBytes.Array != null,
                            "decodedVideoFrame.DecodedBytes.Array != null");

                        Marshal.Copy(decodedVideoFrame.DecodedBytes.Array, srcOffset, backBufferPtr,
                            decodedVideoFrame.Stride);

                        srcOffset += decodedVideoFrame.Stride;
                        backBufferPtr += _writeableBitmap.BackBufferStride;
                    }
                }

                _writeableBitmap.AddDirtyRect(_dirtyRect);
            }
            finally
            {
                _writeableBitmap.Unlock();
            }
        }

        private static void OnFillColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (VideoView) d;
            view._fillColor = (Color) e.NewValue;
        }

        private unsafe void UpdateBackgroundColor(IntPtr backBufferPtr, int backBufferStride)
        {
            byte* pixels = (byte*) backBufferPtr;

            Debug.Assert(pixels != null, nameof(pixels) + " != null");

            for (int i = 0; i < _height; i++)
            for (int j = 0; j < _width; j++)
            {
                int offset = i * backBufferStride + j;
                pixels[offset++] = _fillColor.B;
                pixels[offset++] = _fillColor.G;
                pixels[offset] = _fillColor.R;
            }
        }
    }
}