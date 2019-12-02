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
            //var serverUri = new Uri("rtsp://admin:prysm-123@192.168.50.6:558/LiveChannel/1/media.smp/session=1228432");
            //var serverUri = new Uri("rtsp://service:Ccrlyon69!@192.168.40.25/rtsp_tunnel?p=0&h26x=4&vcd=2");
            var serverUri = new Uri("rtsp://service:Ccrlyon69!@192.168.40.23/rtsp_tunnel?p=0&h26x=4&vcd=2");

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