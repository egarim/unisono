using Pupuseria.Models;
using Pupuseria.Services;

namespace Pupuseria.Pages;

public partial class CartPage : ContentPage
{
	private readonly IOrderService _kitchen;
	private DeliveryMode _delivery = DeliveryMode.Delivery;
	private PaymentMethod _payment = PaymentMethod.Tarjeta;
	private decimal _tip = 1m;

	public CartPage(IOrderService kitchen)
	{
		InitializeComponent();
		_kitchen = kitchen;
		_kitchen.CartChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
		Refresh();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Refresh();
	}

	private void Refresh()
	{
		var cart = _kitchen.Cart;
		cart.Delivery = _delivery; cart.Payment = _payment; cart.Tip = _tip;
		Subtitle.Text = cart.PupusaCount switch { 0 => "Nothing yet. A quiet Tuesday.", 1 => "1 pupusa. A tasting.", var n => $"{n} pupusas. {Models.Botilio.ForQuantity(n)}" };
		Lines.Children.Clear();
		var cardStyle = (Style)Application.Current!.Resources["Card"];
		foreach (var line in cart.Lines)
		{
			var l = line;
			var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(44), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(52)), ColumnSpacing = 10 };
			row.Add(new Image { Source = l.Image, WidthRequest = 44, HeightRequest = 36, VerticalOptions = LayoutOptions.Center }, 0);
			var text = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
			text.Add(new Label { Text = $"{l.Name} · {(l.Masa == Masa.Maiz ? "maíz" : "arroz")}", FontFamily = "FredokaSemiBold", FontSize = 15, TextColor = Color.FromArgb("#2A211C") });
			var detail = l.PupusaId is null ? string.Join(", ", l.Fillings.Select(Models.Botilio.Name)) + " · " : "";
			text.Add(new Label { Text = $"{detail}${l.UnitPrice:0.00} each · curtido {Models.Botilio.Name(l.Curtido).Split(' ')[0]}, salsa {Models.Botilio.Name(l.Salsa).Split(' ')[0]}", FontFamily = "Nunito", FontSize = 11, TextColor = Color.FromArgb("#5C4E42"), LineBreakMode = LineBreakMode.TailTruncation });
			row.Add(text, 1);
			var stepper = new Border { BackgroundColor = Color.FromArgb("#F1E3C8"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, Padding = 2, VerticalOptions = LayoutOptions.Center };
			var minus = new Button { Text = "−", WidthRequest = 32, HeightRequest = 32, CornerRadius = 16, Padding = 0, BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#2A211C"), FontFamily = "NunitoExtraBold", FontSize = 18 };
			var plus = new Button { Text = "+", WidthRequest = 32, HeightRequest = 32, CornerRadius = 16, Padding = 0, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#2A211C"), FontFamily = "NunitoExtraBold", FontSize = 18 };
			minus.Clicked += (_, _) => _kitchen.SetQuantity(l.Id, l.Quantity - 1);
			plus.Clicked += (_, _) => _kitchen.SetQuantity(l.Id, l.Quantity + 1);
			var s = new HorizontalStackLayout { Spacing = 2 };
			s.Add(minus); s.Add(new Label { Text = l.Quantity.ToString(), WidthRequest = 24, HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center, FontFamily = "NunitoExtraBold", FontSize = 14, TextColor = Color.FromArgb("#2A211C") }); s.Add(plus);
			stepper.Content = s;
			row.Add(stepper, 2);
			row.Add(new Label { Text = $"${l.LineTotal:0.00}", FontFamily = "NunitoExtraBold", FontSize = 14, TextColor = Color.FromArgb("#2A211C"), HorizontalTextAlignment = TextAlignment.End, VerticalOptions = LayoutOptions.Center }, 3);
			var card = new Border { Content = row, Style = cardStyle };
			if (l.PupusaId is null) { card.Stroke = Color.FromArgb("#E89B1F"); card.StrokeThickness = 2; }
			Lines.Add(card);
		}
		if (cart.Lines.Count == 0)
			Lines.Add(new Label { Text = "Botilio is polishing the comal. Add something from the menu.", FontFamily = "Nunito", FontSize = 13, TextColor = Color.FromArgb("#7A6A5C"), Margin = new Thickness(0, 8) });

		SubtotalLabel.Text = $"${cart.Subtotal:0.00}";
		DeliveryLabel.Text = _delivery == DeliveryMode.Delivery ? "Envío (Botilio pedals)" : "Envío (you walk, it is good for you)";
		DeliveryFeeLabel.Text = $"${cart.DeliveryFee:0.00}";
		AddressCard.IsVisible = _delivery == DeliveryMode.Delivery;
		TotalLabel.Text = $"${cart.Total:0.00}";
		PlaceButton.IsEnabled = cart.Lines.Count > 0;
		PlaceButton.Opacity = cart.Lines.Count > 0 ? 1 : 0.5;
		Styles();
	}

	private void Styles()
	{
		var on = (Style)Application.Current!.Resources["PillOn"]; var off = (Style)Application.Current!.Resources["Pill"];
		PickupButton.Style = _delivery == DeliveryMode.Pickup ? on : off; DeliveryButton.Style = _delivery == DeliveryMode.Delivery ? on : off;
		foreach (var b in new[] { PickupButton, DeliveryButton }) if (b.Style == off) { b.BackgroundColor = Colors.Transparent; b.BorderWidth = 0; }
		CashButton.Style = _payment == PaymentMethod.Efectivo ? on : off; CardButton.Style = _payment == PaymentMethod.Tarjeta ? on : off; BitcoinButton.Style = _payment == PaymentMethod.Bitcoin ? on : off;
		foreach (var b in new[] { CashButton, CardButton, BitcoinButton }) { b.CornerRadius = 14; b.HeightRequest = 52; b.Padding = new Thickness(6, 0); }
		Tip0.Style = _tip == 0 ? on : off; Tip1.Style = _tip == 1 ? on : off; Tip2.Style = _tip == 2 ? on : off;
		foreach (var b in new[] { Tip0, Tip1, Tip2 }) { b.HeightRequest = 30; b.FontSize = 12; b.Padding = new Thickness(9, 0); b.BorderWidth = 1; b.CornerRadius = 15; }
	}

	private void OnPickup(object? s, EventArgs e) { _delivery = DeliveryMode.Pickup; Refresh(); }
	private void OnDelivery(object? s, EventArgs e) { _delivery = DeliveryMode.Delivery; Refresh(); }
	private void OnCash(object? s, EventArgs e) { _payment = PaymentMethod.Efectivo; Refresh(); }
	private void OnCard(object? s, EventArgs e) { _payment = PaymentMethod.Tarjeta; Refresh(); }
	private void OnBitcoin(object? s, EventArgs e) { _payment = PaymentMethod.Bitcoin; Refresh(); }
	private void OnTip0(object? s, EventArgs e) { _tip = 0; Refresh(); }
	private void OnTip1(object? s, EventArgs e) { _tip = 1; Refresh(); }
	private void OnTip2(object? s, EventArgs e) { _tip = 2; Refresh(); }

	private async void OnPlace(object? s, EventArgs e)
	{
		if (_kitchen.Cart.Lines.Count == 0) return;
		await PlaceButton.ScaleTo(0.96, 70); await PlaceButton.ScaleTo(1, 140, Easing.BounceOut);
		var order = _kitchen.PlaceOrder(_delivery, _payment, Address.Text, _tip);
		_kitchen.ReportToolCall("place_order", $"order #{order.Number} from the app, ${order.Total:0.00}");
		await Shell.Current.GoToAsync("//status");
	}
}
