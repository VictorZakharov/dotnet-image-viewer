using ImageViewer.Models;

namespace ImageViewer.Tests;

public sealed class SelectionModelTests
{
    [Fact]
    public void RangeSelectionUsesCurrentSortOrderAndStableAnchor()
    {
        var alpha = new Item("alpha");
        var beta = new Item("beta");
        var gamma = new Item("gamma");
        var delta = new Item("delta");
        var original = new[] { alpha, beta, gamma, delta };
        var sorted = new[] { delta, gamma, beta, alpha };
        var selection = new SelectionModel<Item>();

        selection.SelectOnly(beta);
        selection.SelectRange(original, delta, additive: false);
        selection.SelectRange(sorted, alpha, additive: false);

        Assert.Equal(beta, selection.Anchor);
        Assert.Equal(alpha, selection.Focus);
        Assert.Equal(2, selection.Count);
        Assert.Contains(beta, selection.Selected);
        Assert.Contains(alpha, selection.Selected);
    }

    [Fact]
    public void RangeSelectionUsesFilteredOrderWithoutDroppingHiddenItemsPrematurely()
    {
        var alpha = new Item("alpha");
        var beta = new Item("beta");
        var gamma = new Item("gamma");
        var delta = new Item("delta");
        var all = new[] { alpha, beta, gamma, delta };
        var filtered = new[] { beta, delta };
        var selection = new SelectionModel<Item>();

        selection.SelectOnly(beta);
        selection.SelectRange(all, delta, additive: false);
        selection.Reconcile(all);
        Assert.Contains(gamma, selection.Selected);

        selection.SelectRange(filtered, delta, additive: false);

        Assert.Equal(new[] { beta, delta }, selection.Selected.OrderBy(x => x.Name));
    }

    [Fact]
    public void HiddenAnchorFallsBackToVisibleKeyboardFocus()
    {
        var alpha = new Item("alpha");
        var beta = new Item("beta");
        var delta = new Item("delta");
        var filtered = new[] { beta, delta };
        var selection = new SelectionModel<Item>();

        selection.SelectOnly(alpha);
        selection.FocusOnly(beta);
        selection.SelectRange(filtered, delta, additive: false);

        Assert.Equal(beta, selection.Anchor);
        Assert.Equal(new[] { beta, delta }, selection.Selected.OrderBy(x => x.Name));
    }

    private sealed record Item(string Name);
}
