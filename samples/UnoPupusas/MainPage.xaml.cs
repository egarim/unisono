using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnoPupusas;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        UpdateTotal();
    }

    private void OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        => UpdateTotal();

    private void UpdateTotal()
    {
        if (QuantityInput is null || PriceInput is null || TotalText is null) return;
        var quantity = double.IsNaN(QuantityInput.Value) ? 0 : Math.Max(0, QuantityInput.Value);
        var price = double.IsNaN(PriceInput.Value) ? 0 : Math.Max(0, PriceInput.Value);
        TotalText.Text = $"Total: ${quantity * price:0.00}";
    }
}
