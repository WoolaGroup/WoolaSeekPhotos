using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Woola.PhotoManager.UI.ViewModels;

/// <summary>
/// IMP-T3-002: ObservableCollection con AddRange/ReplaceAll que dispara
/// un único CollectionChanged(Reset) en lugar de N eventos Add individuales.
/// Reduce de N pasadas de layout a 1 al cargar lotes de fotos.
/// </summary>
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification;

    public RangeObservableCollection() { }
    public RangeObservableCollection(IEnumerable<T> items) : base(items) { }

    /// <summary>
    /// Añade un rango de items con una única notificación Reset al final.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _suppressNotification = true;
        try
        {
            foreach (var item in items)
                Items.Add(item);    // directo al backing List<T>, sin eventos
        }
        finally
        {
            _suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Limpia y reemplaza el contenido completo con una única notificación Reset.
    /// Equivalente a Clear() + AddRange() pero en un solo evento.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _suppressNotification = true;
        try
        {
            Items.Clear();
            foreach (var item in items)
                Items.Add(item);
        }
        finally
        {
            _suppressNotification = false;
        }

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }
}
