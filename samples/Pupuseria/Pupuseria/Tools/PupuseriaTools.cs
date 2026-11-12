using System.ComponentModel;
using ModelContextProtocol.Server;
using Pupuseria.Models;
using Pupuseria.Services;

namespace Pupuseria.Tools;

/// <summary>
/// The one place the pupusería's actions are defined. Siri (generated App Intents), MCP clients and Android AppFunctions
/// all end up here. Descriptions matter: assistants choose tools and fill parameters from them.
/// </summary>
[McpServerToolType]
public sealed class PupuseriaTools(IOrderService kitchen)
{
	[McpServerTool(Name = "list_menu", Title = "List Menu", ReadOnly = true, Destructive = false)]
	[Description("Today's pupusas with prices in US dollars, each with its fillings. Every pupusa comes in corn (maiz) or rice (arroz) masa.")]
	public IReadOnlyList<PupusaDto> ListMenu()
	{
		kitchen.ReportToolCall("list_menu", "someone is reading the menu");
		return kitchen.Menu.Select(PupusaDto.From).ToList();
	}

	[McpServerTool(Name = "add_to_order", Title = "Add to Order", Destructive = false, Idempotent = false)]
	[Description("Adds pupusas from the menu to the order. Returns the cart with totals and what Botilio said.")]
	public OrderDto AddToOrder(
		[Description("Menu pupusa id or name: queso, revuelta, loroco, frijol, ayote or camaron")] string pupusaId,
		[Description("How many, 1 to 50")] int quantity = 2,
		[Description("Masa: maiz (corn) or arroz (rice)")] Masa masa = Masa.Maiz,
		[Description("Curtido on top: nada, normal or modoTia")] CurtidoLevel curtido = CurtidoLevel.Normal,
		[Description("Salsa: poquita, normal or banada")] SalsaLevel salsa = SalsaLevel.Normal)
	{
		var pupusa = kitchen.FindPupusa(pupusaId) ?? throw new ArgumentException($"No pupusa called '{pupusaId}'. The menu has: {string.Join(", ", kitchen.Menu.Select(p => p.Id))}.");
		var line = kitchen.AddLine(pupusa, quantity, masa, curtido, salsa);
		kitchen.ReportToolCall("add_to_order", $"{line.Quantity} × {line.Name}");
		return OrderDto.From(kitchen.Cart, Botilio.ForAdded(line, kitchen.Cart));
	}

	[McpServerTool(Name = "build_pupusa", Title = "Build a Pupusa", Destructive = false, Idempotent = false)]
	[Description("Adds a custom pupusa (La Tuya) with the fillings you choose. Returns the cart with totals and what Botilio said.")]
	public OrderDto BuildPupusa(
		[Description("Fillings, any of: queso, frijol, chicharron, loroco, ayote, camaron, espinaca, jalapeno")] IReadOnlyList<Filling> fillings,
		[Description("How many, 1 to 50")] int quantity = 1,
		[Description("Masa: maiz (corn) or arroz (rice)")] Masa masa = Masa.Maiz,
		[Description("Curtido on top: nada, normal or modoTia")] CurtidoLevel curtido = CurtidoLevel.Normal,
		[Description("Salsa: poquita, normal or banada")] SalsaLevel salsa = SalsaLevel.Normal)
	{
		var line = kitchen.AddCustom(fillings, quantity, masa, curtido, salsa);
		kitchen.ReportToolCall("build_pupusa", $"{line.Quantity} × La Tuya ({string.Join(", ", line.Fillings.Select(Botilio.Name))})");
		return OrderDto.From(kitchen.Cart, Botilio.ForBuilt(line) + " " + Botilio.ForQuantity(kitchen.Cart.PupusaCount));
	}

	[McpServerTool(Name = "get_cart", Title = "Get Cart", ReadOnly = true, Destructive = false)]
	[Description("The order being assembled: lines, pupusa count and totals, before it is placed.")]
	public OrderDto GetCart() => OrderDto.From(kitchen.Cart, Botilio.ForQuantity(kitchen.Cart.PupusaCount));

	[McpServerTool(Name = "place_order", Title = "Place Order", Destructive = true, Idempotent = false)]
	[Description("Sends the cart to the comal. Pickup or delivery; pay with efectivo (cash), tarjeta (card) or bitcoin. Returns the placed order with its number and status.")]
	public OrderDto PlaceOrder(
		[Description("pickup or delivery")] DeliveryMode delivery = DeliveryMode.Delivery,
		[Description("efectivo, tarjeta or bitcoin")] PaymentMethod payment = PaymentMethod.Tarjeta,
		[Description("Delivery address; the saved one if omitted")] string? address = null,
		[Description("Tip for Botilio in dollars; robots save for parts")] decimal tip = 1.00m)
	{
		var order = kitchen.PlaceOrder(delivery, payment, address, tip);
		kitchen.ReportToolCall("place_order", $"order #{order.Number}, ${order.Total:0.00}, {payment}");
		return OrderDto.From(order, Botilio.ForPlaced(order));
	}

	[McpServerTool(Name = "get_order_status", Title = "Get Order Status", ReadOnly = true, Destructive = false)]
	[Description("Where the last placed order is: palmeada, enElComal, volteada, enCamino or entregada, with the minutes left and Botilio's commentary.")]
	public OrderStatusDto GetOrderStatus()
	{
		kitchen.ReportToolCall("get_order_status", "someone asked where their pupusas are");
		var order = kitchen.ActiveOrder;
		return new OrderStatusDto(order?.Number, order?.Status ?? OrderStatus.Carrito, order is null ? "" : Botilio.StepTitle(order.Status),
			order?.EtaMinutes ?? 0, Botilio.ForStatus(order));
	}

	[McpServerTool(Name = "clear_cart", Title = "Clear Cart", Destructive = true, Idempotent = true)]
	[Description("Empties the cart. Returns how many pupusas were removed.")]
	public int ClearCart()
	{
		var n = kitchen.Cart.PupusaCount;
		kitchen.ClearCart();
		kitchen.ReportToolCall("clear_cart", $"{n} pupusas removed");
		return n;
	}
}

/// <summary>A menu pupusa on the wire; also the Siri entity "Pupusa".</summary>
public sealed record PupusaDto(string Id, string Name, string Tagline, decimal Price, IReadOnlyList<Filling> Fillings)
{
	public static PupusaDto From(Pupusa p) => new(p.Id, p.Name, p.Tagline, p.Price, p.Fillings);
}

public sealed record OrderLineDto(string Id, string Name, IReadOnlyList<Filling> Fillings, Masa Masa, int Quantity, CurtidoLevel Curtido, SalsaLevel Salsa, decimal UnitPrice, decimal LineTotal)
{
	public static OrderLineDto From(OrderLine l) => new(l.Id, l.Name, l.Fillings, l.Masa, l.Quantity, l.Curtido, l.Salsa, l.UnitPrice, l.LineTotal);
}

public sealed record OrderDto(int Number, OrderStatus Status, IReadOnlyList<OrderLineDto> Lines, int PupusaCount, decimal Subtotal, decimal DeliveryFee, decimal Tip, decimal Total, DeliveryMode Delivery, PaymentMethod Payment, int EtaMinutes, string BotilioSays)
{
	public static OrderDto From(Order o, string says) => new(o.Number, o.Status, o.Lines.Select(OrderLineDto.From).ToList(), o.PupusaCount, o.Subtotal, o.DeliveryFee, o.Lines.Count > 0 ? o.Tip : 0m, o.Total, o.Delivery, o.Payment, o.EtaMinutes, says);
}

public sealed record OrderStatusDto(int? Number, OrderStatus Status, string Step, int EtaMinutes, string BotilioSays);
