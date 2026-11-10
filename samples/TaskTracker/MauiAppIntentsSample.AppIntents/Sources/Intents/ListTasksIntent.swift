import AppIntents

/// Lists tasks with optional filters. Calls the C# tool `list_tasks`.
struct ListTasksIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "List Tasks"
    static var description = IntentDescription("Lists tasks in TaskTracker, optionally filtered by category or priority")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Category")
    var category: TaskCategory?

    @Parameter(title: "Priority")
    var priority: TaskPriority?

    @Parameter(title: "Show Completed", default: false)
    var showCompleted: Bool

    static var parameterSummary: some ParameterSummary {
        Summary("List tasks") {
            \.$category
            \.$priority
            \.$showCompleted
        }
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$category, \.$priority)) { _, _ in
            DisplayRepresentation(title: "Show my tasks", subtitle: "List tasks in TaskTracker")
        }
    }

    struct Args: Encodable {
        var category: TaskCategory?
        var priority: TaskPriority?
        var showCompleted: Bool
    }

    func perform() async throws -> some IntentResult & ReturnsValue<[TaskEntity]> & ProvidesDialog {
        let args = Args(category: category, priority: priority, showCompleted: showCompleted)
        let entities = try await UnisonoTools.invoke("list_tasks", args, returning: [TaskDto].self).map(TaskEntity.init)

        var parts: [String] = []
        if let category { parts.append(String(localized: TaskCategory.caseDisplayRepresentations[category]?.title ?? "")) }
        if let priority { parts.append(String(localized: TaskPriority.caseDisplayRepresentations[priority]?.title ?? "")) }
        let filterText = parts.isEmpty ? "all" : parts.joined(separator: " ")

        return .result(value: entities, dialog: "Found \(entities.count) \(filterText) task\(entities.count == 1 ? "" : "s")")
    }
}
