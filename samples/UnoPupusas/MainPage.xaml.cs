using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace UnoPupusas;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (TotalText is not null) TotalText.Text = "Server total: —";
    }

    private async void Calculate_Click(object sender, RoutedEventArgs e)
    {
        var quantity = double.IsNaN(QuantityInput.Value) ? 0 : Math.Max(0, QuantityInput.Value);
        var price = double.IsNaN(PriceInput.Value) ? 0 : Math.Max(0, PriceInput.Value);

#if __WASM__
        try
        {
            TotalText.Text = "Calling C# MCP tool…";
            var total = await BrowserMcpBridge.CalculatePupusasAsync((int)quantity, price);
            TotalText.Text = $"Server total: ${total:0.00}";
        }
        catch (Exception ex)
        {
            TotalText.Text = "MCP Apps call failed: " + ex.Message;
        }
#else
        TotalText.Text = "The MCP Apps bridge is available in WebAssembly builds.";
#endif
    }
}
