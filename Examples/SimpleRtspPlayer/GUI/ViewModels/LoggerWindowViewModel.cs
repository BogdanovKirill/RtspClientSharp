using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Logger;
using SimpleRtspPlayer.RawFramesDecoding.FFmpeg;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using LoggerWindow = SimpleRtspPlayer.GUI.Views.LoggerWindow;

namespace SimpleRtspPlayer.GUI.ViewModels
{
    public unsafe class LoggerWindowViewModel : ViewModelBase
    {
        private bool _canExecute = true;

        public RelayCommand SaveLogToFileCommand { get; }
        public RelayCommand ClearLogCommand { get; }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogDelegate(byte* data);

        public static LogDelegate logDelegate;

        public LoggerWindowViewModel()
        {
            PlayerLogger.fLogMethod = (s) => OnLogUpdated(s);
            logDelegate = new LogDelegate(PlayerLogger.LogDllOutput);
            
            FFmpegVideoPInvoke.SetLogMethod(Marshal.GetFunctionPointerForDelegate(logDelegate));

            SaveLogToFileCommand = new RelayCommand(OnSaveLogToFile, () => _canExecute);
            ClearLogCommand = new RelayCommand(OnClearLog, () => _canExecute);
        }

        private void OnSaveLogToFile()
        {
            var path = $@"C:\Users\Otavio\Desktop\PlayerLogs\Log.txt";
            var logToSave = SimpleRtspPlayer.GUI.Views.LoggerWindow.Instance.logTextBox.Text;
            var appendTextId = $"---------------  LogDate { DateTime.Now.Day }_{ DateTime.Now.Hour}_{ DateTime.Now.Minute}_{ DateTime.Now.Second} ---------------";

            StringBuilder sb = new StringBuilder($"{ appendTextId }\n{ logToSave }");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, sb.ToString());
            }

            File.AppendAllText(path, sb.ToString());
        }

        private void OnClearLog()
         => LoggerWindow.Instance.logTextBox.Clear();

        private void OnLogUpdated(string info)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoggerWindow.Instance.logTextBox.AppendText($"{ info }\n");
                LoggerWindow.Instance.logTextBox.CaretIndex = LoggerWindow.Instance.logTextBox.Text.Length;
                LoggerWindow.Instance.logTextBox.ScrollToEnd();
            });
        }
    }
}
