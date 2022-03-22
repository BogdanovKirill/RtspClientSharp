using Logger;
using System.Windows;

namespace SimpleRtspPlayer.GUI.Views
{
    /// <summary>
    /// Interaction logic for Logger.xaml
    /// </summary>
    public partial class LoggerView : Window
    {
        public LoggerView()
        {
            InitializeComponent();
            PlayerLogger.fLogMethod = (s) => logTextBox.AppendText(s);
        }
    }
}
