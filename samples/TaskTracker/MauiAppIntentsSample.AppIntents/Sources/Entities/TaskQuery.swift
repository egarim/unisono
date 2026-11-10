import AppIntents

/// Resolves tasks for Siri and Shortcuts by asking the C# `IEntityQuery<TaskDto>` registered as "Task".
struct TaskEntityQuery: EntityStringQuery {
    func entities(for identifiers: [String]) async throws -> [TaskEntity] {
        try await UnisonoTools.entities("Task", ids: identifiers, as: TaskDto.self).map(TaskEntity.init)
    }

    func entities(matching string: String) async throws -> IntentItemCollection<TaskEntity> {
        IntentItemCollection(items: try await UnisonoTools.search("Task", string, as: TaskDto.self).map(TaskEntity.init))
    }

    func suggestedEntities() async throws -> IntentItemCollection<TaskEntity> {
        IntentItemCollection(items: try await UnisonoTools.suggested("Task", as: TaskDto.self).map(TaskEntity.init))
    }
}
