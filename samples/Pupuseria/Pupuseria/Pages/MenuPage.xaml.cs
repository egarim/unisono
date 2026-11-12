using Pupuseria.Models;
using Pupuseria.Services;

namespace Pupuseria.Pages;

public partial class MenuPage : ContentPage
{
	private readonly IOrderService _kitchen;
	private Masa _masa = Masa.Maiz;
	private readonly Dictionary<string, View> _rowImages = new();

	public MenuPage(IOrderService kitchen)
	{
		InitializeComponent();
		_kitchen = kitchen;
		BuildMenu();
		UpdateCart();
		Botilio.Say("¡Buenas! Soy Botilio, tu pupusero.", "What are we flipping today? The comal is hot and I have two hands. Technically.");

		_kitchen.LineAdded += (_, line) => MainThread.BeginInvokeOnMainThread(() => _ = OnLineAdded(line));
		_kitchen.CartChanged += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateCart);
		_kitchen.ToolCalled += (_, e) => MainThread.BeginInvokeOnMainThread(() => _ = ShowBanner(e));
		_kitchen.OrderPlaced += (_, order) => MainThread.BeginInvokeOnMainThread(() =>
			Botilio.Say($"Order #{order.Number} is on.", "Palmeando now. Do not call me, it breaks my concentration."));
	}

	private void BuildMenu()
	{
		MenuList.Children.Clear();
		var round = (Style)Application.Current!.Resources["Round"];
		var cardStyle = (Style)Application.Current!.Resources["Card"];
		foreach (var p in _kitchen.Menu)
		{
			var image = new Image { Source = p.Image, WidthRequest = 56, HeightRequest = 48, VerticalOptions = LayoutOptions.Center };
			_rowImages[p.Id] = image;
			var add = new Button { Text = "+", Style = round };
			add.Clicked += (_, _) => _kitchen.AddLine(p, 1, _masa, CurtidoLevel.Normal, SalsaLevel.Normal);
			SemanticProperties.SetDescription(add, $"Agregar pupusa de {p.Name}");
			var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(56), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)), ColumnSpacing = 12 };
			row.Add(image, 0);
			var text = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
			text.Add(new Label { Text = p.Name, FontFamily = "FredokaSemiBold", FontSize = 16, TextColor = Color.FromArgb("#2A211C") });
			text.Add(new Label { Text = p.Tagline, FontFamily = "Nunito", FontSize = 12, TextColor = Color.FromArgb("#5C4E42"), LineBreakMode = LineBreakMode.WordWrap });
			row.Add(text, 1);
			row.Add(new Label { Text = $"${p.Price:0.00}", FontFamily = "NunitoExtraBold", FontSize = 15, TextColor = Color.FromArgb("#2A211C"), VerticalOptions = LayoutOptions.Center }, 2);
			row.Add(add, 3);
			var card = new Border { Content = row, Style = cardStyle };
			if (p.Id == "revuelta") { card.Stroke = Color.FromArgb("#E89B1F"); card.StrokeThickness = 2; }
			MenuList.Add(card);
		}
	}

	private void UpdateCart()
	{
		var cart = _kitchen.Cart;
		BadgeCount.Text = cart.PupusaCount.ToString();
		Badge.IsVisible = cart.PupusaCount > 0;
		CartTotal.Text = $"${cart.Total:0.00}";
		CartSummary.Text = cart.Lines.Count == 0
			? "The cart is empty. Botilio is waiting."
			: $"{cart.PupusaCount} pupusas · " + string.Join(", ", cart.Lines.Select(l => $"{l.Quantity} {l.Name.ToLowerInvariant()}"));
	}

	/// <summary>The animation every front door triggers: the pupusa flies from its row (or from Botilio) into the cart.</summary>
	private async Task OnLineAdded(OrderLine line)
	{
		Botilio.Say($"¡Va! {line.Quantity} × {line.Name}.", LineFor(line));
		Botilio.Hold(line.Image);
		_ = Botilio.Flip();

		var start = line.PupusaId is not null && _rowImages.TryGetValue(line.PupusaId, out var rowImage) ? rowImage : Botilio;
		var from = PointIn(start);
		var to = PointIn(CartButton);
		Flyer.Source = line.Image;
		Flyer.TranslationX = from.X; Flyer.TranslationY = from.Y;
		Flyer.Scale = 1; Flyer.Rotation = 0; Flyer.Opacity = 1; Flyer.IsVisible = true;
		await Task.WhenAll(
			Flyer.TranslateTo(to.X + 4, to.Y + 4, 650, Easing.CubicInOut),
			Flyer.ScaleTo(0.45, 650, Easing.CubicIn),
			Flyer.RotateTo(300, 650, Easing.CubicOut));
		Flyer.IsVisible = false;

		await Badge.ScaleTo(1.45, 110, Easing.CubicOut);
		await Badge.ScaleTo(1, 160, Easing.BounceOut);
		CartBar.BackgroundColor = Color.FromArgb("#C8432B");
		await Task.Delay(220);
		CartBar.BackgroundColor = Color.FromArgb("#2A211C");
	}

	private string LineFor(OrderLine line)
	{
		var masa = line.Masa == Masa.Arroz ? "De arroz, the Olocuilta way." : "De maíz, the classic.";
		return $"{masa} {Models.Botilio.ForQuantity(_kitchen.Cart.PupusaCount)}";
	}

	/// <summary>Top-left of an element relative to this page, walking up the parents.</summary>
	private Point PointIn(VisualElement element)
	{
		double x = 0, y = 0;
		Element? e = element;
		while (e is VisualElement v && e != this)
		{
			x += v.X + v.TranslationX; y += v.Y + v.TranslationY;
			e = e.Parent;
		}
		return new Point(x, y);
	}

	private async Task ShowBanner(KitchenEvent e)
	{
		BannerTool.Text = e.Tool;
		BannerText.Text = e.Summary;
		Banner.CancelAnimations();
		await Task.WhenAll(Banner.TranslateTo(0, 0, 260, Easing.CubicOut), Banner.FadeTo(1, 200));
		await Task.Delay(2200);
		await Task.WhenAll(Banner.TranslateTo(0, -120, 260, Easing.CubicIn), Banner.FadeTo(0, 200));
	}

	private void OnMaiz(object? s, EventArgs e) => SetMasa(Masa.Maiz);
	private void OnArroz(object? s, EventArgs e) => SetMasa(Masa.Arroz);

	private void SetMasa(Masa masa)
	{
		_masa = masa;
		var on = (Style)Application.Current!.Resources["PillOn"]; var off = (Style)Application.Current!.Resources["Pill"];
		MaizButton.Style = masa == Masa.Maiz ? on : off;
		ArrozButton.Style = masa == Masa.Arroz ? on : off;
		if (masa == Masa.Maiz) { ArrozButton.BackgroundColor = Colors.Transparent; ArrozButton.BorderWidth = 0; }
		else { MaizButton.BackgroundColor = Colors.Transparent; MaizButton.BorderWidth = 0; }
		MasaHint.Text = masa == Masa.Arroz ? "Rice masa: softer, chewier, Olocuilta-approved." : "The original, slightly crispy.";
	}

	private async void OnCartTapped(object? s, TappedEventArgs e) => await Shell.Current.GoToAsync("//cart");
}
