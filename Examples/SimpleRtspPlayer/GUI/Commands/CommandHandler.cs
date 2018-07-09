using System;
using System.Windows.Input;

namespace SimpleRtspPlayer.GUI.Commands
{
    class CommandHandler : ICommand
    {
        private readonly Action _action;
        private bool _canExecute;

        public event EventHandler CanExecuteChanged;

        public CommandHandler(Action action, bool canExecute)
        {
            _action = action;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute;
        }

        public void SetCanExecute(bool value)
        {
            _canExecute = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Execute(object parameter)
        {
            _action();
        }
    }
}