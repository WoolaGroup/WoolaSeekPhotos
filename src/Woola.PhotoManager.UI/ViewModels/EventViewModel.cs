using Woola.PhotoManager.Core.Services;

namespace Woola.PhotoManager.UI.ViewModels;

/// <summary>Wrapper ligero de EventInfo para enlazar con la barra lateral.</summary>
public class EventViewModel
{
    public EventInfo Event { get; }
    public string DisplayName  => Event.Name;
    public string CountLabel   => $"({Event.PhotoCount})";

    public EventViewModel(EventInfo @event) => Event = @event;
}
