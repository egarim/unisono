import AppIntents

/// Due-date window for listing tasks. Raw values match the C# `DueFilter` enum as serialized ("thisWeek").
enum DueFilter: String, AppEnum, Codable {
    case any, today, tomorrow, thisWeek, overdue

    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Due"

    static var caseDisplayRepresentations: [DueFilter: DisplayRepresentation] = [
        .any: DisplayRepresentation(title: "Any time", subtitle: "All open tasks"),
        .today: DisplayRepresentation(title: "Today", subtitle: "Due today"),
        .tomorrow: DisplayRepresentation(title: "Tomorrow", subtitle: "Due tomorrow"),
        .thisWeek: DisplayRepresentation(title: "This week", subtitle: "Due in the next seven days"),
        .overdue: DisplayRepresentation(title: "Overdue", subtitle: "Past their due date")
    ]
}
