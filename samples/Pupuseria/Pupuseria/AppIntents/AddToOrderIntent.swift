import AppIntents

/// Hand-written on purpose: a nicer dialog than the generated one (Siri reads Botilio's line back) and a quantity
/// prompt. Everything it uses (PupusaEntity, Masa, OrderDto) is generated from the C# tools.
struct AddToOrderIntent: AppIntent {
    static var title: LocalizedStringResource = "Add to Order"
    static var description = IntentDescription("Adds pupusas from the menu to your order at Pupusería")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Pupusa")
    var pupusa: PupusaEntity

    @Parameter(title: "How many", default: 2, inclusiveRange: (1, 50), requestValueDialog: "How many?")
    var quantity: Int

    @Parameter(title: "Masa", default: .maiz)
    var masa: Masa

    static var parameterSummary: some ParameterSummary {
        Summary("Add \(\.$quantity) \(\.$pupusa)") {
            \.$masa
        }
    }

    struct Args: Encodable { var pupusaId: String; var quantity: Int; var masa: Masa }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let cart = try await UnisonoTools.invoke("add_to_order", Args(pupusaId: pupusa.id, quantity: quantity, masa: masa), returning: OrderDto.self)
        return .result(dialog: "\(cart.botilioSays)")
    }
}
