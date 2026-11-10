import AppIntents

/// Creates a new task. Calls the C# tool `create_task` through the Unisono bridge.
struct CreateTaskIntent: AppIntent, PredictableIntent {
    static var title: LocalizedStringResource = "Create Task"
    static var description = IntentDescription("Creates a new task in TaskTracker")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Title")
    var taskTitle: String

    @Parameter(title: "Priority", default: .medium)
    var priority: TaskPriority

    @Parameter(title: "Category", default: .personal)
    var category: TaskCategory

    @Parameter(title: "Due Date")
    var dueDate: Date?

    @Parameter(title: "Estimated Minutes", inclusiveRange: (1, 480))
    var estimatedMinutes: Int?

    @Parameter(title: "Notes")
    var notes: String?

    static var parameterSummary: some ParameterSummary {
        Summary("Create \(\.$taskTitle) task") {
            \.$priority
            \.$category
            \.$dueDate
            \.$estimatedMinutes
            \.$notes
        }
    }

    static var predictionConfiguration: some IntentPredictionConfiguration {
        IntentPrediction(parameters: (\.$priority, \.$category)) { priority, category in
            DisplayRepresentation(title: "Create a \(priority) \(category) task", subtitle: "Add a new task to TaskTracker")
        }
    }

    /// Mirrors the parameters of the C# `create_task` tool.
    struct Args: Encodable {
        var title: String
        var priority: TaskPriority
        var category: TaskCategory
        var dueDate: Date?
        var estimatedMinutes: Int?
        var notes: String?
    }

    func perform() async throws -> some IntentResult & ReturnsValue<TaskEntity> & ProvidesDialog {
        let args = Args(title: taskTitle, priority: priority, category: category,
                        dueDate: dueDate, estimatedMinutes: estimatedMinutes, notes: notes)
        let created = try await UnisonoTools.invoke("create_task", args, returning: TaskDto.self)
        let entity = TaskEntity(created)
        let priorityText = TaskPriority.caseDisplayRepresentations[priority]?.title ?? "medium"
        let categoryText = TaskCategory.caseDisplayRepresentations[category]?.title ?? "personal"
        return .result(value: entity, dialog: "Created '\(created.title)' with \(priorityText) priority in \(categoryText)")
    }
}
