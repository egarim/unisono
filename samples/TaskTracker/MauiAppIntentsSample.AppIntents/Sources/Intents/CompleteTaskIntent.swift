import AppIntents

/// Marks a task as completed. Calls the C# tool `complete_task`.
struct CompleteTaskIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "Complete Task"
    static var description = IntentDescription("Marks a task as completed in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Task")
    var target: TaskEntity

    static var parameterSummary: some ParameterSummary {
        Summary("Complete \(\.$target)")
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$target)) { target in
            DisplayRepresentation(title: "Complete \(target)", subtitle: "Mark a task as done")
        }
    }

    struct Args: Encodable { var id: String }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let success = try await UnisonoTools.invoke("complete_task", Args(id: target.id), returning: Bool.self)
        guard success else { throw IntentError.taskNotFound }
        return .result(dialog: "Done! '\(target.title)' has been marked as completed.")
    }
}
