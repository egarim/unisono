import AppIntents

/// The phrases Siri answers to. Declaring this provider replaces the generated one, so every intent is listed here,
/// generated ones included (ListMenuIntent, BuildAPupusaIntent, ClearCartIntent come from the C# tools).
struct PupuseriaShortcuts: AppShortcutsProvider {
    @AppShortcutsBuilder
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: AddToOrderIntent(),
            phrases: [
                "Order pupusas in \(.applicationName)",
                "Order \(\.$pupusa) in \(.applicationName)",
                "Add \(\.$pupusa) in \(.applicationName)",
                "Add pupusas in \(.applicationName)"
            ],
            shortTitle: "Order Pupusas",
            systemImageName: "plus.circle"
        )

        AppShortcut(
            intent: BuildAPupusaIntent(),
            phrases: [
                "Build a pupusa in \(.applicationName)",
                "Make my own pupusa in \(.applicationName)"
            ],
            shortTitle: "Build a Pupusa",
            systemImageName: "hands.sparkles"
        )

        AppShortcut(
            intent: ListMenuIntent(),
            phrases: [
                "What is on the menu in \(.applicationName)",
                "Show the menu in \(.applicationName)",
                "What pupusas are there in \(.applicationName)"
            ],
            shortTitle: "Menu",
            systemImageName: "list.bullet"
        )

        AppShortcut(
            intent: PlaceOrderIntent(),
            phrases: [
                "Place my order in \(.applicationName)",
                "Send my order in \(.applicationName)",
                "Échale in \(.applicationName)"
            ],
            shortTitle: "Place Order",
            systemImageName: "flame"
        )

        AppShortcut(
            intent: GetOrderStatusIntent(),
            phrases: [
                "Where is my order in \(.applicationName)",
                "Where are my pupusas in \(.applicationName)",
                "Order status in \(.applicationName)"
            ],
            shortTitle: "Order Status",
            systemImageName: "clock"
        )

        AppShortcut(
            intent: ClearCartIntent(),
            phrases: [
                "Clear my cart in \(.applicationName)",
                "Empty my cart in \(.applicationName)"
            ],
            shortTitle: "Clear Cart",
            systemImageName: "trash"
        )
    }
}
