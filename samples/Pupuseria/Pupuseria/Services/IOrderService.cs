using Pupuseria.Models;

namespace Pupuseria.Services;

/// <summary>What happened in the kitchen, so the pages can animate it whoever caused it: a tap, Siri, an MCP client or Gemini.</summary>
public sealed record KitchenEvent(string Tool, string Summary);

public interface IOrderService
{
	IReadOnlyList<Pupusa> Menu { get; }
	Pupusa? FindPupusa(string id);
	IReadOnlyList<Pupusa> SearchPupusas(string text);

	/// <summary>The cart: the order being assembled, status Carrito.</summary>
	Order Cart { get; }
	/// <summary>The last order sent to the comal, if any.</summary>
	Order? ActiveOrder { get; }

	OrderLine AddLine(Pupusa pupusa, int quantity, Masa masa, CurtidoLevel curtido, SalsaLevel salsa);
	OrderLine AddCustom(IReadOnlyList<Filling> fillings, int quantity, Masa masa, CurtidoLevel curtido, SalsaLevel salsa);
	bool SetQuantity(string lineId, int quantity);
	bool RemoveLine(string lineId);
	void ClearCart();
	Order PlaceOrder(DeliveryMode delivery, PaymentMethod payment, string? address, decimal tip);

	/// <summary>Raised after the kitchen changed; the pages animate from these. Always on the thread that caused it.</summary>
	event EventHandler<OrderLine>? LineAdded;
	event EventHandler? CartChanged;
	event EventHandler<Order>? OrderPlaced;
	event EventHandler<Order>? StatusChanged;
	/// <summary>A tool ran (from any front door); the pages show it as the "incoming call" banner.</summary>
	event EventHandler<KitchenEvent>? ToolCalled;
	void ReportToolCall(string tool, string summary);
}
