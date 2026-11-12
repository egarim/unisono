import AppIntents

/// Task priority as an AppEnum. Raw values match the JSON the C# tools use ("low", "medium", ...).
enum TaskPriority: String, AppEnum, Codable {
    case low, medium, high, urgent

    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Priority"

    static var caseDisplayRepresentations: [TaskPriority: DisplayRepresentation] = [
        .low: DisplayRepresentation(title: "Low", subtitle: "Not time-sensitive"),
        .medium: DisplayRepresentation(title: "Medium", subtitle: "Normal priority"),
        .high: DisplayRepresentation(title: "High", subtitle: "Important"),
        .urgent: DisplayRepresentation(title: "Urgent", subtitle: "Needs immediate attention")
    ]
}
