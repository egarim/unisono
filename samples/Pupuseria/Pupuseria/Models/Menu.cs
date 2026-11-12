namespace Pupuseria.Models;

/// <summary>Corn or rice dough. Serialized camelCase ("maiz", "arroz") for MCP and the Siri bridge.</summary>
public enum Masa { Maiz, Arroz }

/// <summary>What goes inside. Loroco is an edible flower; yes, really.</summary>
public enum Filling { Queso, Frijol, Chicharron, Loroco, Ayote, Camaron, Espinaca, Jalapeno }

/// <summary>How much pickled cabbage goes on top. ModoTia is a mountain.</summary>
public enum CurtidoLevel { Nada, Normal, ModoTia }

/// <summary>How much tomato salsa. Banada means swimming.</summary>
public enum SalsaLevel { Poquita, Normal, Banada }

public enum DeliveryMode { Pickup, Delivery }

/// <summary>El Salvador uses the US dollar and, since 2021, Bitcoin. Sí, de verdad.</summary>
public enum PaymentMethod { Efectivo, Tarjeta, Bitcoin }

/// <summary>Where an order is, in pupusa-making order.</summary>
public enum OrderStatus { Carrito, Palmeada, EnElComal, Volteada, EnCamino, Entregada }

/// <summary>A pupusa on the menu. The id is stable: Siri and Shortcuts store entity references by id.</summary>
public sealed record Pupusa(string Id, string Name, string Tagline, decimal Price, IReadOnlyList<Filling> Fillings, string Image);

/// <summary>One line of an order: a menu pupusa or a custom one ("La Tuya"), with masa, quantity and toppings.</summary>
public sealed class OrderLine
{
	public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
	public string? PupusaId { get; init; }
	public required string Name { get; init; }
	public required IReadOnlyList<Filling> Fillings { get; init; }
	public required string Image { get; init; }
	public Masa Masa { get; init; } = Masa.Maiz;
	public int Quantity { get; set; } = 1;
	public CurtidoLevel Curtido { get; init; } = CurtidoLevel.Normal;
	public SalsaLevel Salsa { get; init; } = SalsaLevel.Normal;
	public decimal UnitPrice { get; init; }
	public decimal LineTotal => UnitPrice * Quantity;
}

public sealed class Order
{
	public int Number { get; init; }
	public List<OrderLine> Lines { get; } = new();
	public OrderStatus Status { get; set; } = OrderStatus.Carrito;
	public DeliveryMode Delivery { get; set; } = DeliveryMode.Delivery;
	public PaymentMethod Payment { get; set; } = PaymentMethod.Tarjeta;
	public string Address { get; set; } = "Colonia Escalón, San Salvador · timbre 2 veces";
	public decimal Tip { get; set; } = 1.00m;
	public DateTimeOffset? PlacedAt { get; set; }
	public int EtaMinutes { get; set; }

	public int PupusaCount => Lines.Sum(l => l.Quantity);
	public decimal Subtotal => Lines.Sum(l => l.LineTotal);
	public decimal DeliveryFee => Delivery == DeliveryMode.Delivery && Lines.Count > 0 ? 1.00m : 0m;
	public decimal Total => Subtotal + DeliveryFee + (Lines.Count > 0 ? Tip : 0m);
}
