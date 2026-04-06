using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Size = System.Windows.Size;   // IMP-T3-001: evita ambigüedad con System.Drawing.Size
using Rect = System.Windows.Rect;

namespace Woola.PhotoManager.UI.Controls;

/// <summary>
/// IMP-T3-001: Panel virtualizante de tipo wrap para tarjetas de fotos de tamaño fijo.
///
/// Soporta VirtualizationMode.Recycling: reutiliza contenedores en vez de destruirlos.
/// Solo materializa los elementos visibles en el viewport, reduciendo la carga WPF de
/// N elementos a ~15 elementos visibles con independencia del tamaño de la colección.
///
/// Requiere CanContentScroll="True" en el ScrollViewer padre para que IScrollInfo sea
/// invocado en este panel (en lugar de scroll pixel-a-pixel en el ScrollViewer).
/// </summary>
public class VirtualizingWrapPanel : VirtualizingPanel, IScrollInfo
{
    // ── Dimensiones fijas de cada tarjeta ────────────────────────────────────
    private const double ItemWidth  = 236.0;   // 220 card + 8 margen izq + 8 margen der
    private const double ItemHeight = 278.0;   // 180 imagen + ~90 texto + márgenes

    // ── Estado IScrollInfo ───────────────────────────────────────────────────
    private ScrollViewer? _scrollOwner;
    private bool          _canHScroll;
    private bool          _canVScroll = true;
    private double        _verticalOffset;
    private Size          _extent     = Size.Empty;
    private Size          _viewport   = Size.Empty;

    // ── IScrollInfo: propiedades ─────────────────────────────────────────────
    public ScrollViewer? ScrollOwner  { get => _scrollOwner;  set { _scrollOwner = value; } }
    public bool CanHorizontallyScroll { get => _canHScroll;   set => _canHScroll = value; }
    public bool CanVerticallyScroll   { get => _canVScroll;   set => _canVScroll = value; }
    public double ExtentWidth         => _extent.Width;
    public double ExtentHeight        => _extent.Height;
    public double ViewportWidth       => _viewport.Width;
    public double ViewportHeight      => _viewport.Height;
    public double HorizontalOffset    => 0;
    public double VerticalOffset      => _verticalOffset;

    // ── IScrollInfo: comandos de desplazamiento ───────────────────────────────
    public void LineUp()           => SetVerticalOffset(_verticalOffset - ItemHeight / 2);
    public void LineDown()         => SetVerticalOffset(_verticalOffset + ItemHeight / 2);
    public void PageUp()           => SetVerticalOffset(_verticalOffset - _viewport.Height);
    public void PageDown()         => SetVerticalOffset(_verticalOffset + _viewport.Height);
    public void MouseWheelUp()     => SetVerticalOffset(_verticalOffset - ItemHeight * 2);
    public void MouseWheelDown()   => SetVerticalOffset(_verticalOffset + ItemHeight * 2);
    public void LineLeft()         { }
    public void LineRight()        { }
    public void PageLeft()         { }
    public void PageRight()        { }
    public void MouseWheelLeft()   { }
    public void MouseWheelRight()  { }
    public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;

    public void SetHorizontalOffset(double offset) { }

    public void SetVerticalOffset(double offset)
    {
        var max = Math.Max(0, _extent.Height - _viewport.Height);
        offset = Math.Max(0, Math.Min(offset, max));
        if (Math.Abs(offset - _verticalOffset) < 0.5) return;
        _verticalOffset = offset;
        _scrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }

    // ── Layout: Measure ──────────────────────────────────────────────────────
    protected override Size MeasureOverride(Size availableSize)
    {
        if (availableSize.Width == 0) return new Size(0, 0);

        var itemsOwner = ItemsControl.GetItemsOwner(this);
        var itemCount  = itemsOwner?.Items.Count ?? 0;
        var generator  = ItemContainerGenerator;

        int columns    = Math.Max(1, (int)(availableSize.Width / ItemWidth));
        int rows       = (int)Math.Ceiling((double)itemCount / columns);
        double totalH  = rows * ItemHeight;

        var vpHeight   = double.IsInfinity(availableSize.Height) ? totalH : availableSize.Height;
        var viewport   = new Size(availableSize.Width, vpHeight);

        // Actualizar scroll info si cambió
        bool scrollChanged = _viewport != viewport || _extent.Height != totalH;
        if (scrollChanged)
        {
            _viewport = viewport;
            _extent   = new Size(availableSize.Width, totalH);
        }

        // Limitar offset al rango válido
        if (_extent.Height > 0)
        {
            var maxOff = Math.Max(0, _extent.Height - _viewport.Height);
            if (_verticalOffset > maxOff) _verticalOffset = maxOff;
        }

        if (scrollChanged)
            _scrollOwner?.InvalidateScrollInfo();

        // Rango visible (por filas)
        int firstRow   = (int)Math.Floor(_verticalOffset / ItemHeight);
        int lastRow    = (int)Math.Ceiling((_verticalOffset + vpHeight) / ItemHeight);
        int firstIndex = Math.Max(0, firstRow * columns);
        int lastIndex  = Math.Min(itemCount - 1, lastRow * columns - 1);

        if (itemCount == 0)
        {
            CleanupChildren(0, -1, GetVirtualizationMode(this) == VirtualizationMode.Recycling);
            return viewport;
        }

        // Generar contenedores para el rango visible
        var startPos = generator.GeneratorPositionFromIndex(firstIndex);
        using var _ = generator.StartAt(startPos, GeneratorDirection.Forward, true);

        for (int i = firstIndex; i <= lastIndex; i++)
        {
            bool isNew;
            var child = (UIElement)generator.GenerateNext(out isNew);
            if (isNew)
            {
                if (i < InternalChildren.Count) InsertInternalChild(i - firstIndex, child);
                else                             AddInternalChild(child);
                generator.PrepareItemContainer(child);
            }
            child.Measure(new Size(ItemWidth, ItemHeight));
        }

        CleanupChildren(firstIndex, lastIndex, GetVirtualizationMode(this) == VirtualizationMode.Recycling);

        return viewport;
    }

    // ── Layout: Arrange ──────────────────────────────────────────────────────
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize.Width == 0) return finalSize;

        var itemsOwner = ItemsControl.GetItemsOwner(this);
        if (itemsOwner == null) return finalSize;

        int columns   = Math.Max(1, (int)(finalSize.Width / ItemWidth));
        var generator = (System.Windows.Controls.ItemContainerGenerator)ItemContainerGenerator;

        foreach (UIElement child in InternalChildren)
        {
            var index = generator.IndexFromContainer(child);
            if (index < 0) continue;

            int col  = index % columns;
            int row  = index / columns;
            double x = col * ItemWidth;
            double y = row * ItemHeight - _verticalOffset;
            child.Arrange(new Rect(x, y, ItemWidth, ItemHeight));
        }

        return finalSize;
    }

    // ── Virtualización: limpiar contenedores fuera del viewport ──────────────
    private void CleanupChildren(int firstVisible, int lastVisible, bool recycling)
    {
        // IndexFromContainer solo existe en la clase concreta
        var generator  = (System.Windows.Controls.ItemContainerGenerator)ItemContainerGenerator;
        // Recycle es de IRecyclingItemContainerGenerator; Remove es impl. explícita en IItemContainerGenerator
        var generatorI = (IItemContainerGenerator)generator;
        var generatorR = (IRecyclingItemContainerGenerator)generator;

        for (int i = InternalChildren.Count - 1; i >= 0; i--)
        {
            var child      = InternalChildren[i];
            var childIndex = generator.IndexFromContainer(child);
            if (childIndex >= firstVisible && childIndex <= lastVisible) continue;

            if (recycling)
                generatorR.Recycle(new GeneratorPosition(i, 0), 1);
            else
                generatorI.Remove(new GeneratorPosition(i, 0), 1);

            RemoveInternalChildRange(i, 1);
        }
    }
}
