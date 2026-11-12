import AppIntents

/// Searches tasks by keyword. Calls the C# tool `search_tasks`.
struct SearchTasksIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "Search Tasks"
    static var description = IntentDescription("Searches for tasks by keyword in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Search Query")
    var query: String

    static var parameterSummary: some ParameterSummary {
        Summary("Search for \(\.$query)")
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$query)) { query in
            DisplayRepresentation(title: "Search for \(query)", subtitle: "Find tasks in TaskTracker")
        }
    }

    struct Args: Encodable { var query: String }

    func perform() async throws -> some IntentResult & ReturnsValue<[TaskEntity]> & ProvidesDialog {
        let entities = try await UnisonoTools.invoke("search_tasks", Args(query: query), returning: [TaskDto].self).map(TaskEntity.init)
        if entities.isEmpty {
            return .result(value: entities, dialog: "No tasks found matching '\(query)'")
        }
        return .result(value: entities, dialog: "Found \(entities.count) task\(entities.count == 1 ? "" : "s") matching '\(query)'")
    }
}
