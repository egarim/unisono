namespace Pupuseria.Pages;

/// <summary>Botilio with his speech bubble. Say(...) pops the bubble and nods; Flip() swings the spatula.</summary>
public partial class BotilioView : ContentView
{
	public BotilioView()
	{
		InitializeComponent();
	}

	public void Say(string title, string line)
	{
		Title.Text = title;
		Line.Text = line;
		_ = Pop();
	}

	private async Task Pop()
	{
		Bubble.CancelAnimations();
		Bubble.Scale = 0.6; Bubble.Opacity = 0;
		var pop = Bubble.ScaleTo(1.06, 180, Easing.CubicOut);
		var fade = Bubble.FadeTo(1, 180);
		var nod = Nod();
		await Task.WhenAll(pop, fade, nod);
		await Bubble.ScaleTo(1, 120, Easing.CubicInOut);
	}

	public async Task Nod()
	{
		Figure.AnchorY = 1;
		await Figure.RotateTo(-6, 90, Easing.CubicOut);
		await Figure.RotateTo(5, 110, Easing.CubicInOut);
		await Figure.RotateTo(0, 110, Easing.CubicIn);
	}

	public async Task Flip()
	{
		await Spatula.RotateTo(-45, 120, Easing.CubicOut);
		await Spatula.RotateTo(0, 220, Easing.BounceOut);
	}

	public void Hold(string image) => Held.Source = image;
}
