using SimpleRtspPlayer.GUI.Models;
using SimpleRtspPlayer.GUI.ViewModels;
using System;

namespace SimpleRtspPlayer
{
    class ViewModelLocator
    {
        private readonly Lazy<MainWindowViewModel> _mainWindowViewModelLazy =
            new Lazy<MainWindowViewModel>(CreateMainWindowViewModel);

        public MainWindowViewModel MainWindowViewModel => _mainWindowViewModelLazy.Value;

        private static MainWindowViewModel CreateMainWindowViewModel()
        {
            var model = new MainWindowModel();
            return new MainWindowViewModel(model);
        }

        private readonly Lazy<LoggerWindowViewModel> _loggerWindowViewModelLazy =
            new Lazy<LoggerWindowViewModel>(CreateLoggerWindowViewModel);

        public LoggerWindowViewModel LoggerWindowViewModel => _loggerWindowViewModelLazy.Value;

        private static LoggerWindowViewModel CreateLoggerWindowViewModel()
            => new LoggerWindowViewModel();
        
    }
}