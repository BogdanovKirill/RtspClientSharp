using RtspClientSharp;
using RtspClientSharp.RawFrames.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleRtspOnvifMetadataPlayer
{
    class Program
    {
        static void Main(string[] args)
        {
            Start();
            Console.ReadLine();
        }

        static async void Start()
        {
            var serverUri = new Uri("rtsp://root:pass@192.168.40.1/onvif-media/media.amp?profile=profile_1_h264&sessiontimeout=60&streamtype=unicast");

            var connectionParameters = new ConnectionParameters(serverUri)
            {
                RtpTransport = RtpTransportProtocol.TCP,
                RequiredTracks = RequiredTracks.Data,
                ReceiveTimeout = TimeSpan.FromMilliseconds(int.MaxValue)
            };

            using (var rtspClient = new RtspClient(connectionParameters))
            {
                rtspClient.FrameReceived += (sender, frame) =>
                {
                    if (frame is OnvifMetadataFrame onvifFrame)
                    {
                        System.Diagnostics.Debug.WriteLine(onvifFrame.XmlSegment);
                        Console.WriteLine(onvifFrame.XmlSegment);
                        Console.WriteLine();
                    }
                };

                try
                {
                    await rtspClient.ConnectAsync(default);
                    await rtspClient.ReceiveAsync(default);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error : " + ex.Message);
                }
            }
        }
    }
}