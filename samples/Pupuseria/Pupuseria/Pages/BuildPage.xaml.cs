using Pupuseria.Models;
using Pupuseria.Services;

namespace Pupuseria.Pages;

public partial class BuildPage : ContentPage
{
	private readonly IOrderService _kitchen;
	private Masa _masa = Masa.Maiz;
	private int _qty = 1;
	private CurtidoLevel _curtido = CurtidoLevel.Normal;
	private SalsaLevel _salsa = SalsaLevel.Normal;
	private readonly HashSet<Filling> _fillings = new();
	private readonly Dictionary<Filling, Button> _chips = new();
	private readonly Dictionary<CurtidoLevel, Button> _curtidoButtons = new();
	private readonly Dictionary<SalsaLevel, Button> _salsaButtons = new();

	public BuildPage(IOrderService kitchen)
	{
		InitializeComponent();
		_kitchen = kitchen;
		foreach (var f in Enum.GetValues<Filling>())
		{
			var chip = new Button { Text = Models.Botilio.Name(f), Style = (Style)Application.Current!.Resources["Pill"], Margin = new Thickness(0, 0, 8, 8) };
			chip.Clicked += (_, _) => Toggle(f);
			_chips[f] = chip;
			Chips.Add(chip);
		}
		foreach (var c in Enum.GetValues<CurtidoLevel>())
		{
			var b = Choice(Models.Botilio.Name(c)); b.Clicked += (_, _) => { _curtido = c; RefreshChoices(); };
			_curtidoButtons[c] = b; CurtidoChoices.Add(b);
		}
		foreach (var s in Enum.GetValues<SalsaLevel>())
		{
			var b = Choice(Models.Botilio.Name(s)); b.Clicked += (_, _) => { _salsa = s; RefreshChoices(); };
			_salsaButtons[s] = b; SalsaChoices.Add(b);
		}
		RefreshChoices();
		Refresh();
	}

	private static Button Choice(string text) => new()
	{
		Text = text, FontFamily = "NunitoBold", FontSize = 13, TextColor = Color.FromArgb("#2A211C"), BackgroundColor = Colors.Transparent,
		CornerRadius = 10, HeightRequest = 36, Padding = new Thickness(10, 0), HorizontalOptions = LayoutOptions.Fill,
	};

	private async void Toggle(Filling f)
	{
		if (!_fillings.Remove(f)) _fillings.Add(f);
		Refresh();
		await _chips[f].ScaleTo(1.08, 80);
		await _chips[f].ScaleTo(1, 120, Easing.BounceOut);
	}

	private void RefreshChoices()
	{
		foreach (var (c, b) in _curtidoButtons) { b.BackgroundColor = c == _curtido ? Color.FromArgb("#FBE7B6") : Colors.Transparent; b.FontFamily = c == _curtido ? "NunitoExtraBold" : "NunitoBold"; }
		foreach (var (s, b) in _salsaButtons) { b.BackgroundColor = s == _salsa ? Color.FromArgb("#FBE7B6") : Colors.Transparent; b.FontFamily = s == _salsa ? "NunitoExtraBold" : "NunitoBold"; }
	}

	private void Refresh()
	{
		var on = (Style)Application.Current!.Resources["PillOn"]; var off = (Style)Application.Current!.Resources["Pill"];
		foreach (var (f, chip) in _chips)
		{
			var selected = _fillings.Contains(f);
			chip.Style = selected ? on : off;
			if (selected)
			{
				chip.BackgroundColor = f switch
				{
					Filling.Queso => Color.FromArgb("#F2B134"), Filling.Frijol => Color.FromArgb("#5B2E1E"), Filling.Loroco => Color.FromArgb("#5E9E4A"),
					Filling.Chicharron => Color.FromArgb("#7A4A2A"), Filling.Ayote => Color.FromArgb("#E88A2E"), Filling.Camaron => Color.FromArgb("#E9735C"),
					Filling.Espinaca => Color.FromArgb("#2F6B2A"), _ => Color.FromArgb("#3E8E2E"),
				};
				chip.BorderColor = chip.BackgroundColor;
				chip.TextColor = f == Filling.Queso ? Color.FromArgb("#2A211C") : Colors.White;
			}
			chip.Text = selected ? Models.Botilio.Name(f) + " ✓" : Models.Botilio.Name(f);
		}
		OvQueso.IsVisible = _fillings.Contains(Filling.Queso); OvFrijol.IsVisible = _fillings.Contains(Filling.Frijol);
		OvChicharron.IsVisible = _fillings.Contains(Filling.Chicharron); OvLoroco.IsVisible = _fillings.Contains(Filling.Loroco);
		OvAyote.IsVisible = _fillings.Contains(Filling.Ayote); OvCamaron.IsVisible = _fillings.Contains(Filling.Camaron);
		OvEspinaca.IsVisible = _fillings.Contains(Filling.Espinaca); OvJalapeno.IsVisible = _fillings.Contains(Filling.Jalapeno);

		MaizButton.Style = _masa == Masa.Maiz ? on : off; ArrozButton.Style = _masa == Masa.Arroz ? on : off;
		foreach (var b in new[] { MaizButton, ArrozButton }) { b.CornerRadius = 14; b.HeightRequest = 48; }
		var names = _fillings.Count == 0 ? "nothing yet" : string.Join(", ", _fillings.Select(Models.Botilio.Name));
		PreviewText.Text = $"{(_masa == Masa.Maiz ? "Maíz" : "Arroz")} · {names}";
		Verdict.Text = "Botilio: " + (_fillings.Count switch
		{
			0 => "pick something. Even cheese counts.",
			1 => "one filling. Pure. Monastic.",
			2 => "two fillings, a classic pairing.",
			3 => "three fillings. Bold. I respect it.",
			_ => $"{_fillings.Count} fillings. Legal, barely. Nobody is watching.",
		});
		Qty.Text = _qty.ToString();
		QtyVerdict.Text = Models.Botilio.ForQuantity(_qty);
		var price = 1.25m + Math.Max(0, _fillings.Count - 1) * 0.25m + (_fillings.Contains(Filling.Camaron) ? 0.50m : 0m);
		AddButton.Text = $"Agregar {_qty} al pedido · ${price * _qty:0.00}";
	}

	private void OnMaiz(object? s, EventArgs e) { _masa = Masa.Maiz; Refresh(); }
	private void OnArroz(object? s, EventArgs e) { _masa = Masa.Arroz; Refresh(); }
	private void OnLess(object? s, EventArgs e) { _qty = Math.Max(1, _qty - 1); Refresh(); }
	private void OnMore(object? s, EventArgs e) { _qty = Math.Min(50, _qty + 1); Refresh(); }

	private async void OnAdd(object? s, EventArgs e)
	{
		if (_fillings.Count == 0) { await DisplayAlertAsync("Botilio", "No filling? That is a tortilla. Pick at least one.", "Fine"); return; }
		await AddButton.ScaleTo(0.96, 70); await AddButton.ScaleTo(1, 120, Easing.BounceOut);
		_kitchen.AddCustom(_fillings.ToList(), _qty, _masa, _curtido, _salsa);
		_kitchen.ReportToolCall("build_pupusa", $"{_qty} × La Tuya from the app");
		await Shell.Current.GoToAsync("//menu");
	}
}
