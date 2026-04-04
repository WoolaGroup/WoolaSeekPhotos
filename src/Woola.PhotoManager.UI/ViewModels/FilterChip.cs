using CommunityToolkit.Mvvm.Input;

namespace Woola.PhotoManager.UI.ViewModels;

public class FilterChip
{
    public string Label { get; }
    public IRelayCommand RemoveCommand { get; }

    public FilterChip(string label, Action removeAction)
    {
        Label = label;
        RemoveCommand = new RelayCommand(removeAction);
    }
}
