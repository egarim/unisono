import AppIntents

/// Appends a line to a note. Calls the C# tool `append_to_note`.
struct AppendToNoteIntent: AppIntent {
    static var title: LocalizedStringResource = "Append to Note"
    static var description = IntentDescription("Adds a line of text to the end of a note in Unisono Notes.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Note")
    var note: NoteEntity

    @Parameter(title: "Text")
    var text: String

    static var parameterSummary: some ParameterSummary {
        Summary("Append \(\.$text) to \(\.$note)")
    }

    struct Args: Encodable {
        var id: String
        var text: String
    }

    func perform() async throws -> some IntentResult & ReturnsValue<NoteEntity> & ProvidesDialog {
        let updated = try await UnisonoTools.invoke("append_to_note", Args(id: note.id, text: text), returning: NoteDto.self)
        return .result(value: NoteEntity(updated), dialog: "Added it to '\(updated.title)'")
    }
}
