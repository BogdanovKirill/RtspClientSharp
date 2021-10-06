using SimpleRtspPlayer.GUI.Views;
using System;
using System.Windows;

namespace SimpleRtspPlayer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            LoggerWindow.Instance.Show();
        }
    }
}