using System;
using System.Windows.Input;

namespace VanillaDownloader.Utils
{
    class Command : ICommand
    {
        readonly Action _execute;

        public Command(Action execute) => _execute = execute;

        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged;
    }
}
