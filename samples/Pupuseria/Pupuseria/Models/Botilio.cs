namespace Pupuseria.Models;

/// <summary>Everything Botilio says. He is a robot, so he is consistent; he is Salvadoran, so he has opinions.</summary>
public static class Botilio
{
	public const string Greeting = "¡Buenas! Soy Botilio, tu pupusero. What are we flipping today? The comal is hot and I have two hands. Technically.";

	public static string ForQuantity(int n) => n switch
	{
		<= 0 => "Zero pupusas is a cry for help.",
		1 => "One pupusa. A tasting. We respect curiosity.",
		2 => "Two. A polite start.",
		3 => "Three: a snack.",
		4 or 5 => $"{n}: honest hunger. Botilio approves.",
		6 or 7 => $"{n}: a meal. Loosen the belt now, save time later.",
		8 or 9 => $"{n}: ambitious. I like you.",
		10 => "Ten. Respect.",
		_ => $"{n}? The comal is big, but is your fridge?",
	};

	public static string ForAdded(OrderLine line, Order cart)
	{
		var what = line.Quantity == 1 ? line.Name : $"{line.Quantity} {Plural(line.Name)}";
		var masa = line.Masa == Masa.Arroz ? "de arroz, the Olocuilta way" : "de maíz, the classic";
		return $"¡Va! {what}, {masa}. {ForQuantity(cart.PupusaCount)} Cart: {cart.PupusaCount} pupusas, ${cart.Total:0.00}.";
	}

	public static string ForBuilt(OrderLine line)
	{
		var count = line.Fillings.Count;
		var verdict = count switch
		{
			0 => "No filling? That is a tortilla. I will make it anyway, out of respect.",
			1 => "One filling. Pure. Monastic.",
			2 => "Two fillings, a classic pairing.",
			3 => "Three fillings. Bold. I respect it.",
			_ => $"{count} fillings. Legal, barely. Nobody is watching.",
		};
		return $"La Tuya: {string.Join(", ", line.Fillings.Select(Name))}. {verdict}";
	}

	public static string ForPlaced(Order order)
	{
		var pay = order.Payment switch
		{
			PaymentMethod.Bitcoin => "Paid in Bitcoin. Sí, de verdad.",
			PaymentMethod.Efectivo => "Cash. Old school. I have change.",
			_ => "Card accepted.",
		};
		var how = order.Delivery == DeliveryMode.Delivery ? "I pedal to you" : "you come to the comal";
		return $"Order #{order.Number} is on. {order.PupusaCount} pupusas, ${order.Total:0.00}, {how}. {pay} Palmeando now. Do not call me, it breaks my concentration.";
	}

	public static string ForStatus(Order? order) => order is null
		? "No order yet. The comal is hot and lonely."
		: order.Status switch
		{
			OrderStatus.Carrito => $"Nothing on the comal yet: {order.PupusaCount} pupusas waiting in your cart. Say the word.",
			OrderStatus.Palmeada => $"Order #{order.Number}: palmeada. The dough is patted by hand; the slaps you heard were real. About {order.EtaMinutes} minutes.",
			OrderStatus.EnElComal => $"Order #{order.Number}: en el comal, side one. Patience is an ingredient. About {order.EtaMinutes} minutes.",
			OrderStatus.Volteada => $"Order #{order.Number}: volteada, just flipped. No spatula was harmed. About {order.EtaMinutes} minutes.",
			OrderStatus.EnCamino => $"Order #{order.Number}: en camino. I pedal, curtido rides on top as the law requires. About {order.EtaMinutes} minutes.",
			OrderStatus.Entregada => $"Order #{order.Number}: entregada. Eat with your hands. Forks are for tourists.",
			_ => "I lost track. Robots are not perfect; pupusas are.",
		};

	public static string StepTitle(OrderStatus s) => s switch
	{
		OrderStatus.Palmeada => "Palmeada",
		OrderStatus.EnElComal => "En el comal",
		OrderStatus.Volteada => "Volteada",
		OrderStatus.EnCamino => "En camino",
		OrderStatus.Entregada => "Entregada",
		_ => "Carrito",
	};

	public static string StepLine(OrderStatus s) => s switch
	{
		OrderStatus.Palmeada => "Masa patted by hand. The slaps you heard were real.",
		OrderStatus.EnElComal => "Side one. Patience is an ingredient.",
		OrderStatus.Volteada => "The flip. No spatula was harmed.",
		OrderStatus.EnCamino => "Botilio pedals. Curtido rides on top, as the law requires.",
		OrderStatus.Entregada => "Eat with your hands. Forks are for tourists.",
		_ => "",
	};

	public static string Name(Filling f) => f switch
	{
		Filling.Queso => "queso",
		Filling.Frijol => "frijol",
		Filling.Chicharron => "chicharrón",
		Filling.Loroco => "loroco",
		Filling.Ayote => "ayote",
		Filling.Camaron => "camarón",
		Filling.Espinaca => "espinaca",
		Filling.Jalapeno => "jalapeño",
		_ => f.ToString().ToLowerInvariant(),
	};

	public static string Name(CurtidoLevel c) => c switch { CurtidoLevel.Nada => "nada (who hurt you?)", CurtidoLevel.ModoTia => "modo tía (a mountain)", _ => "normal" };
	public static string Name(SalsaLevel s) => s switch { SalsaLevel.Poquita => "poquita", SalsaLevel.Banada => "bañada (swimming)", _ => "normal" };

	private static string Plural(string name) => name.EndsWith('a') ? name + "s" : name;
}
