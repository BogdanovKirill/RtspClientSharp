using System.Windows;

namespace SimpleRtspPlayer.GUI.Views
{
    /// <summary>
    /// Interaction logic for LoggerWindow.xaml
    /// </summary>
    public partial class LoggerWindow
    {
        public static LoggerWindow Instance { get; private set; }

        static LoggerWindow()
        {
            Instance = new LoggerWindow();
        }

        private LoggerWindow()
        {
            InitializeComponent();
        }

    }
}
