import AppIntents

/// Deletes every completed task after confirmation. Calls the C# tool `delete_completed_tasks`.
struct DeleteCompletedTasksIntent: AppIntent {
    static var title: LocalizedStringResource = "Delete Completed Tasks"
    static var description = IntentDescription("Removes all completed tasks from TaskTracker")
    static var openAppWhenRun: Bool = false

    struct Args: Encodable {}

    func perform() async throws -> some IntentResult & ProvidesDialog {
        try await requestConfirmation(result: .result(dialog: "Delete all completed tasks?"))
        let count = try await UnisonoTools.invoke("delete_completed_tasks", Args(), returning: Int.self)
        return .result(dialog: count == 0 ? "There were no completed tasks." : "Deleted \(count) completed task\(count == 1 ? "" : "s").")
    }
}
