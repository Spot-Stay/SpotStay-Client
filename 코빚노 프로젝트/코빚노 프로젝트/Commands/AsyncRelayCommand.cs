using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace 코빚노_프로젝트
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> execute;
        private readonly Func<object, bool> canExecute;
        private bool isExecuting;

        public AsyncRelayCommand(Func<object, Task> execute)
            : this(execute, null)
        {
        }

        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            if (isExecuting)
            {
                return false;
            }

            if (canExecute == null)
            {
                return true;
            }

            return canExecute(parameter);
        }

        public async void Execute(object parameter)
        {
            isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await execute(parameter);
            }
            finally
            {
                isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            EventHandler handler = CanExecuteChanged;

            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}