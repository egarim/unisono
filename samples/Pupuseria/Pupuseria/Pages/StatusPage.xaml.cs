using Pupuseria.Models;
using Pupuseria.Services;

namespace Pupuseria.Pages;

public partial class StatusPage : ContentPage
{
	private static readonly OrderStatus[] StepOrder = { OrderStatus.Palmeada, OrderStatus.EnElComal, OrderStatus.Volteada, OrderStatus.EnCamino, OrderStatus.Entregada };
	private readonly IOrderService _kitchen;
	private readonly Dictionary<OrderStatus, (Border dot, Label title, Label line)> _rows = new();
	private bool _loops;

	public StatusPage(IOrderService kitchen)
	{
		InitializeComponent();
		_kitchen = kitchen;
		BuildSteps();
		Render(_kitchen.ActiveOrder);
		_kitchen.OrderPlaced += (_, o) => MainThread.BeginInvokeOnMainThread(() => { Render(o); _ = Flare(); });
		_kitchen.StatusChanged += (_, o) => MainThread.BeginInvokeOnMainThread(() => { Render(o); _ = Flare(); if (o.Status == OrderStatus.Volteada) _ = FlipPupusa(); });
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Render(_kitchen.ActiveOrder);
		if (!_loops) { _loops = true; StartLoops(); }
	}

	private void BuildSteps()
	{
		foreach (var s in StepOrder)
		{
			var dot = new Border { WidthRequest = 26, HeightRequest = 26, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 13 }, StrokeThickness = 2, Stroke = Color.FromArgb("#5A5651"), BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Center };
			var title = new Label { Text = Models.Botilio.StepTitle(s), FontFamily = "NunitoExtraBold", FontSize = 14, TextColor = Color.FromArgb("#BFAE93") };
			var line = new Label { Text = Models.Botilio.StepLine(s), FontFamily = "Nunito", FontSize = 12, TextColor = Color.FromArgb("#8C867F"), LineBreakMode = LineBreakMode.WordWrap };
			var rail = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center };
			rail.Add(dot);
			if (s != OrderStatus.Entregada) rail.Add(new BoxView { WidthRequest = 2, HeightRequest = 22, Color = Color.FromArgb("#5A5651"), HorizontalOptions = LayoutOptions.Center });
			var text = new VerticalStackLayout { Padding = new Thickness(0, 3, 0, 0) }; text.Add(title); text.Add(line);
			var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(26), new ColumnDefinition(GridLength.Star)), ColumnSpacing = 12 };
			row.Add(rail, 0); row.Add(text, 1);
			Steps.Add(row);
			_rows[s] = (dot, title, line);
		}
	}

	private void Render(Order? order)
	{
		if (order is null)
		{
			Eyebrow.Text = "NO ORDER YET"; Headline.Text = "The comal is hot and lonely."; Eta.Text = "—";
			BubbleText.Text = "Botilio: " + Models.Botilio.ForStatus(null);
			AdvanceButton.IsVisible = false;
		}
		else
		{
			Eyebrow.Text = $"PEDIDO #{order.Number} · {(order.Delivery == DeliveryMode.Delivery ? "A DOMICILIO" : "PARA RECOGER")}";
			Headline.Text = order.Status switch
			{
				OrderStatus.Palmeada => "Tu pupusa está en las manos de Botilio.",
				OrderStatus.EnElComal => "Tu pupusa está en el comal.",
				OrderStatus.Volteada => "Tu pupusa ya dio la vuelta.",
				OrderStatus.EnCamino => "Tu pupusa está en camino.",
				OrderStatus.Entregada => "Tu pupusa llegó. ¡Buen provecho!",
				_ => "Tu pupusa está en el carrito.",
			};
			Eta.Text = order.Status == OrderStatus.Entregada ? "¡Ya!" : $"{order.EtaMinutes} min";
			BubbleText.Text = "Botilio: " + Models.Botilio.ForStatus(order);
			AdvanceButton.IsVisible = order.Status != OrderStatus.Entregada;
		}
		var current = order?.Status ?? OrderStatus.Carrito;
		var idx = Array.IndexOf(StepOrder, current);
		for (var i = 0; i < StepOrder.Length; i++)
		{
			var (dot, title, line) = _rows[StepOrder[i]];
			if (i < idx || current == OrderStatus.Entregada && i == idx)
			{
				dot.BackgroundColor = Color.FromArgb("#5E9E4A"); dot.Stroke = Color.FromArgb("#5E9E4A");
				dot.Content = new Image { Source = "check.png", WidthRequest = 14, HeightRequest = 14, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
				title.TextColor = Color.FromArgb("#FFF6E5"); line.TextColor = Color.FromArgb("#BFAE93");
			}
			else if (i == idx)
			{
				dot.BackgroundColor = Color.FromArgb("#E89B1F"); dot.Stroke = Color.FromArgb("#E89B1F");
				dot.Content = new BoxView { WidthRequest = 10, HeightRequest = 10, CornerRadius = 5, Color = Color.FromArgb("#2B2A28"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
				title.TextColor = Color.FromArgb("#F2B134"); line.TextColor = Color.FromArgb("#BFAE93");
			}
			else
			{
				dot.BackgroundColor = Colors.Transparent; dot.Stroke = Color.FromArgb("#5A5651"); dot.Content = null;
				title.TextColor = Color.FromArgb("#BFAE93"); line.TextColor = Color.FromArgb("#8C867F");
			}
		}
	}

	private void StartLoops()
	{
		Loop(Flame1, 0, 1.0, 1.35, 520); Loop(Flame2, 120, 1.0, 1.45, 480); Loop(Flame3, 240, 1.0, 1.3, 560);
		Steam(Steam1, 0); Steam(Steam2, 600); Steam(Steam3, 1200);
	}

	private void Loop(VisualElement v, int delay, double from, double to, uint ms)
	{
		var a = new Animation();
		a.Add(0, 0.5, new Animation(x => v.ScaleY = x, from, to, Easing.SinInOut));
		a.Add(0.5, 1, new Animation(x => v.ScaleY = x, to, from, Easing.SinInOut));
		Dispatcher.StartTimer(TimeSpan.FromMilliseconds(delay), () => { a.Commit(this, "flame" + v.Id, 16, ms, null, null, () => true); return false; });
	}

	private void Steam(VisualElement v, int delay)
	{
		var a = new Animation();
		a.Add(0, 1, new Animation(y => v.TranslationY = y, 0, -34, Easing.SinOut));
		a.Add(0, 0.3, new Animation(o => v.Opacity = o, 0, 0.9));
		a.Add(0.3, 1, new Animation(o => v.Opacity = o, 0.9, 0));
		Dispatcher.StartTimer(TimeSpan.FromMilliseconds(delay), () => { a.Commit(this, "steam" + v.Id, 16, 1800, null, null, () => true); return false; });
	}

	/// <summary>The comal reacts to a status change: the flames jump and Botilio swings the spatula.</summary>
	private async Task Flare()
	{
		await Task.WhenAll(Flame1.ScaleTo(1.6, 140), Flame2.ScaleTo(1.7, 140), Flame3.ScaleTo(1.6, 140), Spatula.RotateTo(-45, 140, Easing.CubicOut));
		await Task.WhenAll(Flame1.ScaleTo(1, 320, Easing.BounceOut), Flame2.ScaleTo(1, 320, Easing.BounceOut), Flame3.ScaleTo(1, 320, Easing.BounceOut), Spatula.RotateTo(0, 260, Easing.BounceOut));
		await Bubble.ScaleTo(1.06, 120); await Bubble.ScaleTo(1, 140, Easing.BounceOut);
	}

	private async Task FlipPupusa()
	{
		await Pupusa2.TranslateTo(0, -18, 160, Easing.CubicOut);
		await Pupusa2.RotateXTo(180, 240, Easing.CubicInOut);
		await Pupusa2.TranslateTo(0, 0, 160, Easing.BounceOut);
		Pupusa2.Source = "pupusa_cooked.png";
		Pupusa2.RotationX = 0;
	}

	private async void OnAgain(object? s, EventArgs e) => await Shell.Current.GoToAsync("//menu");

	/// <summary>For the demo: moves the comal one step without waiting the 20 seconds.</summary>
	private void OnHurry(object? s, EventArgs e)
	{
		if (_kitchen is OrderService os) os.Hurry();
	}
}
