using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using RtspClientSharp;
using RtspClientSharp.RawFrames.Video;

namespace MjpegSnapshotsMaker
{
    class Program
    {
        public class Options
        {
            [Option('u', "uri", Required = true, HelpText = "RTSP URI")]
            public Uri Uri { get; set; }

            [Option('p', "path", Required = true, HelpText = "Path where snapshots should be saved")]
            public string Path { get; set; }

            [Option('i', "interval", Required = false, HelpText = "Snapshots saving interval in seconds")]
            public int Interval { get; set; } = 5;
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(options =>
                {
                    var cancellationTokenSource = new CancellationTokenSource();

                    Task makeSnapshotsTask = MakeSnapshotsAsync(options, cancellationTokenSource.Token);

                    Console.ReadKey();

                    cancellationTokenSource.Cancel();
                    makeSnapshotsTask.Wait();
                })
                .WithNotParsed(options =>
                {
                    Console.WriteLine("Usage example: MjpegSnapshotsMaker.exe " +
                                      "-u rtsp://admin:123456@192.168.1.77:554/ucast/11 " +
                                      "-p S:\\Temp");
                });
        }

        private static async Task MakeSnapshotsAsync(Options options, CancellationToken token)
        {
            try
            {
                if (!Directory.Exists(options.Path))
                    Directory.CreateDirectory(options.Path);

                int intervalMs = options.Interval * 1000;
                int lastTimeSnapshotSaved = Environment.TickCount - intervalMs;

                var connectionParameters = new ConnectionParameters(options.Uri);
                using (var rtspClient = new RtspClient(connectionParameters))
                {
                    rtspClient.FrameReceived += (sender, frame) =>
                    {
                        if (!(frame is RawJpegFrame))
                            return;

                        int ticksNow = Environment.TickCount;

                        if (Math.Abs(ticksNow - lastTimeSnapshotSaved) < intervalMs)
                            return;

                        lastTimeSnapshotSaved = ticksNow;

                        string snapshotName = frame.Timestamp.ToString("O").Replace(":", "_") + ".jpg";
                        string path = Path.Combine(options.Path, snapshotName);

                        ArraySegment<byte> frameSegment = frame.FrameSegment;

                        using (var stream = File.OpenWrite(path))
                            stream.Write(frameSegment.Array, frameSegment.Offset, frameSegment.Count);

                        Console.WriteLine($"[{DateTime.UtcNow}] Snapshot is saved to {snapshotName}");
                    };

                    Console.WriteLine("Connecting...");
                    await rtspClient.ConnectAsync(token);
                    Console.WriteLine("Receiving...");
                    await rtspClient.ReceiveAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}