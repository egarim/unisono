import AppIntents

/// Sets or updates the due date of a task. Calls the C# tool `set_due_date`.
struct SetDueDateIntent: AppIntent {
    static var title: LocalizedStringResource = "Set Due Date"
    static var description = IntentDescription("Sets the due date for a task in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Task")
    var target: TaskEntity

    @Parameter(title: "Due Date")
    var dueDate: Date

    static var parameterSummary: some ParameterSummary {
        Summary("Set due date of \(\.$target) to \(\.$dueDate)")
    }

    struct Args: Encodable {
        var id: String
        var dueDate: Date
    }

    func perform() async throws -> some IntentResult & ProvidesDialog {
        let success = try await UnisonoTools.invoke("set_due_date", Args(id: target.id, dueDate: dueDate), returning: Bool.self)
        guard success else { throw IntentError.taskNotFound }

        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        return .result(dialog: "Set due date of '\(target.title)' to \(formatter.string(from: dueDate))")
    }
}
