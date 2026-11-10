import AppIntents

/// Creates a note. Calls the C# tool `create_note`.
struct CreateNoteIntent: AppIntent {
    static var title: LocalizedStringResource = "Create Note"
    static var description = IntentDescription("Creates a note in Unisono Notes. Lines starting with '- [ ]' become action items.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Title")
    var noteTitle: String

    @Parameter(title: "Body")
    var body: String?

    static var parameterSummary: some ParameterSummary {
        Summary("Create note \(\.$noteTitle)") {
            \.$body
        }
    }

    struct Args: Encodable {
        var title: String
        var body: String?
    }

    func perform() async throws -> some IntentResult & ReturnsValue<NoteEntity> & ProvidesDialog {
        let created = try await UnisonoTools.invoke("create_note", Args(title: noteTitle, body: body), returning: NoteDto.self)
        return .result(value: NoteEntity(created), dialog: "Created the note '\(created.title)'")
    }
}
