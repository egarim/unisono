import AppIntents

/// Hand-written so Siri reads Botilio's status line instead of a generic "done".
struct GetOrderStatusIntent: AppIntent {
    static var title: LocalizedStringResource = "Get Order Status"
    static var description = IntentDescription("Where your pupusas are: palmeada, en el comal, volteada, en camino")
    static var openAppWhenRun: Bool = false

    struct NoArgs: Encodable {}

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let status = try await UnisonoTools.invoke("get_order_status", NoArgs(), returning: OrderStatusDto.self)
        return .result(dialog: "\(status.botilioSays)")
    }
}
