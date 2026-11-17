using System.Windows.Input;

namespace TubeCadCam.Presentation;

public sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>Runs an async action; exceptions are reported through <paramref name="onError"/> instead of crashing the UI thread.</summary>
public sealed class AsyncRelayCommand(Func<object?, Task> execute, Action<Exception> onError, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;

    public async void Execute(object? parameter)
    {
        try
        {
            await execute(parameter);
        }
        catch (Exception ex)
        {
            onError(ex);
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
