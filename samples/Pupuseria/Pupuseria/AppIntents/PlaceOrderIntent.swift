import AppIntents

/// Hand-written: confirms with the real total before sending the cart to the comal, then reads Botilio's answer.
struct PlaceOrderIntent: AppIntent {
    static var title: LocalizedStringResource = "Place Order"
    static var description = IntentDescription("Sends your cart to the comal: pickup or delivery, pay with cash, card or Bitcoin")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Delivery", default: .delivery)
    var delivery: DeliveryMode

    @Parameter(title: "Payment", default: .tarjeta)
    var payment: PaymentMethod

    static var parameterSummary: some ParameterSummary {
        Summary("Place the order for \(\.$delivery)") {
            \.$payment
        }
    }

    struct NoArgs: Encodable {}
    struct Args: Encodable { var delivery: DeliveryMode; var payment: PaymentMethod }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let cart = try await UnisonoTools.invoke("get_cart", NoArgs(), returning: OrderDto.self)
        guard cart.pupusaCount > 0 else {
            return .result(dialog: "Your cart is empty. Botilio cannot flip air. Order something first.")
        }
        let total = String(format: "%.2f", cart.total)
        try await requestConfirmation(result: .result(dialog: "Send \(cart.pupusaCount) pupusas, $\(total), to the comal?"))
        let order = try await UnisonoTools.invoke("place_order", Args(delivery: delivery, payment: payment), returning: OrderDto.self)
        return .result(dialog: "\(order.botilioSays)")
    }
}
