import AppIntents

/// Lists tasks with optional filters. Calls the C# tool `list_tasks`.
struct ListTasksIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "List Tasks"
    static var description = IntentDescription("Lists tasks in TaskTracker, optionally filtered by due window, category or priority")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Category")
    var category: TaskCategory?

    @Parameter(title: "Priority")
    var priority: TaskPriority?

    @Parameter(title: "Show Completed", default: false)
    var showCompleted: Bool

    @Parameter(title: "Due", default: .any)
    var due: DueFilter

    static var parameterSummary: some ParameterSummary {
        Summary("List tasks due \(\.$due)") {
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
        var due: DueFilter
    }

    func perform() async throws -> some IntentResult & ReturnsValue<[TaskEntity]> & ProvidesDialog {
        let args = Args(category: category, priority: priority, showCompleted: showCompleted, due: due)
        let entities = try await UnisonoTools.invoke("list_tasks", args, returning: [TaskDto].self).map(TaskEntity.init)

        var parts: [String] = []
        if let category { parts.append(String(localized: TaskCategory.caseDisplayRepresentations[category]?.title ?? "")) }
        if let priority { parts.append(String(localized: TaskPriority.caseDisplayRepresentations[priority]?.title ?? "")) }
        let filterText = parts.isEmpty ? "" : parts.joined(separator: " ") + " "
        let dueText = due == .any ? "" : " due " + String(localized: DueFilter.caseDisplayRepresentations[due]?.title ?? "").lowercased()
        let titles = entities.prefix(5).map(\.title).joined(separator: ", ")
        let summary = entities.isEmpty ? "" : ": " + titles + (entities.count > 5 ? ", and more" : "")

        return .result(value: entities, dialog: "You have \(entities.count) \(filterText)task\(entities.count == 1 ? "" : "s")\(dueText)\(summary)")
    }
}
