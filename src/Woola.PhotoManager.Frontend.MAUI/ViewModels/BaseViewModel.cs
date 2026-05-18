using System.ComponentModel;
using System.Runtime.CompilerServices;
using Woola.PhotoManager.Frontend.MAUI.Services;

namespace Woola.PhotoManager.Frontend.MAUI.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public static event EventHandler? UnauthorizedAccess;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                HasError = !string.IsNullOrEmpty(value);
        }
    }

    private string _successMessage = string.Empty;
    public string SuccessMessage
    {
        get => _successMessage;
        set => SetProperty(ref _successMessage, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected async Task ExecuteAsync(Func<Task> operation, string? loadingMessage = null)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;

        try
        {
            await operation();
        }
        catch (UnauthorizedException)
        {
            UnauthorizedAccess?.Invoke(this, EventArgs.Empty);
        }
        catch (ForbiddenException)
        {
            ErrorMessage = "Access denied";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Connection error. Make sure the backend is running on localhost:5150";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
