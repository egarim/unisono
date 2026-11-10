import AppIntents

/// Opens the app on a note (the app can read the id and navigate; the demo opens the list).
struct OpenNoteIntent: AppIntent {
    static var title: LocalizedStringResource = "Open Note"
    static var description = IntentDescription("Opens a note in Unisono Notes")
    static var openAppWhenRun: Bool = true

    @Parameter(title: "Note")
    var note: NoteEntity

    static var parameterSummary: some ParameterSummary {
        Summary("Open \(\.$note)")
    }

    func perform() async throws -> some IntentResult {
        .result()
    }
}
