import AppIntents

/// Deletes one task after confirmation. Calls the C# tool `delete_task`.
struct DeleteTaskIntent: AppIntent {
    static var title: LocalizedStringResource = "Delete Task"
    static var description = IntentDescription("Deletes a task from TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Task")
    var target: TaskEntity

    static var parameterSummary: some ParameterSummary {
        Summary("Delete \(\.$target)")
    }

    struct Args: Encodable { var id: String }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        try await requestConfirmation(result: .result(dialog: "Delete '\(target.title)'?"))
        let success = try await UnisonoTools.invoke("delete_task", Args(id: target.id), returning: Bool.self)
        guard success else { throw IntentError.taskNotFound }
        return .result(dialog: "Deleted '\(target.title)'.")
    }
}
