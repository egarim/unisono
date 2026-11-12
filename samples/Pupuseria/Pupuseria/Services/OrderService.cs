using Pupuseria.Models;

namespace Pupuseria.Services;

/// <summary>In-memory kitchen. The comal timer moves a placed order one step every <see cref="StepSeconds"/>.</summary>
public sealed class OrderService : IOrderService
{
	public const int StepSeconds = 20;
	private static readonly OrderStatus[] Steps = { OrderStatus.Palmeada, OrderStatus.EnElComal, OrderStatus.Volteada, OrderStatus.EnCamino, OrderStatus.Entregada };

	private readonly object _lock = new();
	private int _nextNumber = 42;
	private Timer? _comal;

	public IReadOnlyList<Pupusa> Menu { get; } = new List<Pupusa>
	{
		new("queso", "Queso", "The classic. Cheese that stretches to the next table.", 1.25m, new[] { Filling.Queso }, "pupusa_queso.png"),
		new("revuelta", "Revuelta", "Chicharrón, frijol, queso. The holy trinity.", 1.50m, new[] { Filling.Chicharron, Filling.Frijol, Filling.Queso }, "pupusa_revuelta.png"),
		new("loroco", "Loroco con queso", "An edible flower. Yes, really. Yes, delicious.", 1.50m, new[] { Filling.Loroco, Filling.Queso }, "pupusa_loroco.png"),
		new("frijol", "Frijol con queso", "Beans and cheese: a committed relationship.", 1.25m, new[] { Filling.Frijol, Filling.Queso }, "pupusa_frijol.png"),
		new("ayote", "Ayote", "Squash. Your grandmother approves.", 1.25m, new[] { Filling.Ayote, Filling.Queso }, "pupusa_ayote.png"),
		new("camaron", "Camarón", "Shrimp. For when you are feeling coastal.", 2.00m, new[] { Filling.Camaron, Filling.Queso }, "pupusa_camaron.png"),
	};

	public Order Cart { get; private set; } = new() { Number = 0 };
	public Order? ActiveOrder { get; private set; }

	public event EventHandler<OrderLine>? LineAdded;
	public event EventHandler? CartChanged;
	public event EventHandler<Order>? OrderPlaced;
	public event EventHandler<Order>? StatusChanged;
	public event EventHandler<KitchenEvent>? ToolCalled;

	public Pupusa? FindPupusa(string id)
	{
		var key = id.Trim().ToLowerInvariant();
		return Menu.FirstOrDefault(p => p.Id == key)
			?? Menu.FirstOrDefault(p => p.Name.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase))
			?? Menu.FirstOrDefault(p => p.Name.Contains(id.Trim(), StringComparison.OrdinalIgnoreCase));
	}

	public IReadOnlyList<Pupusa> SearchPupusas(string text)
	{
		var t = text.Trim();
		if (t.Length == 0) return Menu;
		return Menu.Where(p => p.Name.Contains(t, StringComparison.OrdinalIgnoreCase)
			|| p.Tagline.Contains(t, StringComparison.OrdinalIgnoreCase)
			|| p.Fillings.Any(f => Botilio.Name(f).Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();
	}

	public OrderLine AddLine(Pupusa pupusa, int quantity, Masa masa, CurtidoLevel curtido, SalsaLevel salsa)
	{
		var line = new OrderLine
		{
			PupusaId = pupusa.Id, Name = pupusa.Name, Fillings = pupusa.Fillings, Image = pupusa.Image,
			Masa = masa, Quantity = Math.Clamp(quantity, 1, 50), Curtido = curtido, Salsa = salsa, UnitPrice = pupusa.Price,
		};
		return Add(line);
	}

	public OrderLine AddCustom(IReadOnlyList<Filling> fillings, int quantity, Masa masa, CurtidoLevel curtido, SalsaLevel salsa)
	{
		var distinct = fillings.Distinct().ToList();
		var price = 1.25m + Math.Max(0, distinct.Count - 1) * 0.25m + (distinct.Contains(Filling.Camaron) ? 0.50m : 0m);
		var line = new OrderLine
		{
			Name = "La Tuya", Fillings = distinct, Image = "pupusa_latuya.png",
			Masa = masa, Quantity = Math.Clamp(quantity, 1, 50), Curtido = curtido, Salsa = salsa, UnitPrice = price,
		};
		return Add(line);
	}

	private OrderLine Add(OrderLine line)
	{
		lock (_lock) Cart.Lines.Add(line);
		LineAdded?.Invoke(this, line);
		CartChanged?.Invoke(this, EventArgs.Empty);
		return line;
	}

	public bool SetQuantity(string lineId, int quantity)
	{
		lock (_lock)
		{
			var line = Cart.Lines.FirstOrDefault(l => l.Id == lineId);
			if (line is null) return false;
			if (quantity <= 0) Cart.Lines.Remove(line); else line.Quantity = Math.Min(quantity, 50);
		}
		CartChanged?.Invoke(this, EventArgs.Empty);
		return true;
	}

	public bool RemoveLine(string lineId) => SetQuantity(lineId, 0);

	public void ClearCart()
	{
		lock (_lock) Cart.Lines.Clear();
		CartChanged?.Invoke(this, EventArgs.Empty);
	}

	public Order PlaceOrder(DeliveryMode delivery, PaymentMethod payment, string? address, decimal tip)
	{
		Order order;
		lock (_lock)
		{
			if (Cart.Lines.Count == 0) throw new InvalidOperationException("The cart is empty. Botilio cannot flip air.");
			order = Cart;
			order.Delivery = delivery;
			order.Payment = payment;
			if (!string.IsNullOrWhiteSpace(address)) order.Address = address;
			order.Tip = Math.Max(0, tip);
			order.Status = OrderStatus.Palmeada;
			order.PlacedAt = DateTimeOffset.Now;
			order.EtaMinutes = 12 + Math.Max(0, order.PupusaCount - 4);
			ActiveOrder = order;
			Cart = new Order { Number = order.Number + 1 };
			_comal?.Dispose();
			_comal = new Timer(_ => Advance(), null, TimeSpan.FromSeconds(StepSeconds), TimeSpan.FromSeconds(StepSeconds));
		}
		OrderPlaced?.Invoke(this, order);
		CartChanged?.Invoke(this, EventArgs.Empty);
		return order;
	}

	private void Advance()
	{
		Order? order;
		lock (_lock)
		{
			order = ActiveOrder;
			if (order is null) return;
			var i = Array.IndexOf(Steps, order.Status);
			if (i < 0 || i >= Steps.Length - 1) { _comal?.Dispose(); _comal = null; return; }
			order.Status = Steps[i + 1];
			order.EtaMinutes = Math.Max(0, order.EtaMinutes - 3);
			if (order.Status == OrderStatus.Entregada) { order.EtaMinutes = 0; _comal?.Dispose(); _comal = null; }
		}
		StatusChanged?.Invoke(this, order);
	}

	/// <summary>Demo helper: advance the comal now instead of waiting for the timer.</summary>
	public void Hurry() => Advance();

	public void ReportToolCall(string tool, string summary) => ToolCalled?.Invoke(this, new KitchenEvent(tool, summary));

	public OrderService()
	{
		Cart = new Order { Number = _nextNumber };
	}
}
