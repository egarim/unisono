import AppIntents

/// Task category as an AppEnum. Raw values match the JSON the C# tools use.
enum TaskCategory: String, AppEnum, Codable {
    case work, personal, shopping, health, fitness

    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Category"

    static var caseDisplayRepresentations: [TaskCategory: DisplayRepresentation] = [
        .work: DisplayRepresentation(title: "Work", subtitle: "Professional tasks"),
        .personal: DisplayRepresentation(title: "Personal", subtitle: "Personal errands"),
        .shopping: DisplayRepresentation(title: "Shopping", subtitle: "Shopping lists"),
        .health: DisplayRepresentation(title: "Health", subtitle: "Health-related tasks"),
        .fitness: DisplayRepresentation(title: "Fitness", subtitle: "Exercise and workouts")
    ]
}
