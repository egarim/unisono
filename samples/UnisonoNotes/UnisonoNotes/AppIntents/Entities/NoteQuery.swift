import AppIntents

/// Resolves notes by asking the C# `IEntityQuery<NoteDto>` registered as "Note".
struct NoteEntityQuery: EntityStringQuery {
    func entities(for identifiers: [String]) async throws -> [NoteEntity] {
        try await UnisonoTools.entities("Note", ids: identifiers, as: NoteDto.self).map(NoteEntity.init)
    }

    func entities(matching string: String) async throws -> IntentItemCollection<NoteEntity> {
        IntentItemCollection(items: try await UnisonoTools.search("Note", string, as: NoteDto.self).map(NoteEntity.init))
    }

    func suggestedEntities() async throws -> IntentItemCollection<NoteEntity> {
        IntentItemCollection(items: try await UnisonoTools.suggested("Note", as: NoteDto.self).map(NoteEntity.init))
    }
}
