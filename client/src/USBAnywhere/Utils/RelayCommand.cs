// USBAnywhere - github.com/oscarlosu-lab/USBAnywhere
// (c) 2025 SnakeFoxu - GPL v3

using System.Windows.Input;

namespace USBAnywhere.Utils;

/// <summary>
/// Simple ICommand implementation for async actions
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public async void Execute(object? parameter)
    {
        try { await _execute(parameter); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RelayCommand] {ex}"); }
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Typed version of RelayCommand
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public async void Execute(object? parameter)
    {
        try { await _execute((T?)parameter); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RelayCommand<T>] {ex}"); }
    }
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
