import AppIntents

/// Finds notes by text. Calls the C# tool `list_notes`.
struct FindNotesIntent: AppIntent {
    static var title: LocalizedStringResource = "Find Notes"
    static var description = IntentDescription("Finds notes in Unisono Notes whose title or body contain some text; without text, lists every note.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Text")
    var query: String?

    static var parameterSummary: some ParameterSummary {
        Summary("Find notes containing \(\.$query)")
    }

    struct Args: Encodable { var query: String? }

    func perform() async throws -> some IntentResult & ReturnsValue<[NoteEntity]> & ProvidesDialog {
        let notes = try await UnisonoTools.invoke("list_notes", Args(query: query), returning: [NoteDto].self).map(NoteEntity.init)
        return .result(value: notes, dialog: "Found \(notes.count) note\(notes.count == 1 ? "" : "s")")
    }
}
