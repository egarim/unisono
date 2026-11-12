import AppIntents

/// Wire shape of a task as the C# `TaskDto` serialises it (camelCase keys).
struct TaskDto: Codable {
    var id: String
    var title: String
    var notes: String?
    var priority: TaskPriority
    var category: TaskCategory
    var dueDate: Date?
    var estimatedMinutes: Int?
    var isCompleted: Bool
    var createdAt: Date?
}

/// A task as Siri, Shortcuts and Spotlight see it.
struct TaskEntity: AppEntity {
    static var defaultQuery = TaskEntityQuery()
    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Task"

    var id: String

    @Property(title: "Title")
    var title: String

    @Property(title: "Notes")
    var notes: String?

    @Property(title: "Priority")
    var priority: TaskPriority

    @Property(title: "Category")
    var category: TaskCategory

    @Property(title: "Due Date")
    var dueDate: Date?

    @Property(title: "Estimated Minutes")
    var estimatedMinutes: Int?

    @Property(title: "Completed")
    var isCompleted: Bool

    var displayRepresentation: DisplayRepresentation {
        let priorityText = TaskPriority.caseDisplayRepresentations[priority]?.title ?? "Medium"
        let categoryText = TaskCategory.caseDisplayRepresentations[category]?.title ?? "Personal"
        return DisplayRepresentation(title: "\(title)", subtitle: "\(priorityText) · \(categoryText)")
    }

    init(_ dto: TaskDto) {
        self.id = dto.id
        self.title = dto.title
        self.notes = dto.notes
        self.priority = dto.priority
        self.category = dto.category
        self.dueDate = dto.dueDate
        self.estimatedMinutes = dto.estimatedMinutes
        self.isCompleted = dto.isCompleted
    }
}
