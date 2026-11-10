import AppIntents

/// Returns a note's open action items as text, one per item, so a Shortcut can create a task for each.
/// Calls the C# tool `extract_action_items`.
struct ExtractActionItemsIntent: AppIntent {
    static var title: LocalizedStringResource = "Extract Action Items"
    static var description = IntentDescription("Returns the open action items of a note (lines starting with '- [ ]', 'TODO:' or 'Action:'), one text per item.")
    static var openAppWhenRun: Bool = false

    @Parameter(title: "Note")
    var note: NoteEntity

    static var parameterSummary: some ParameterSummary {
        Summary("Extract action items from \(\.$note)")
    }

    struct Args: Encodable { var id: String }

    func perform() async throws -> some IntentResult & ReturnsValue<[String]> & ProvidesDialog {
        let items = try await UnisonoTools.invoke("extract_action_items", Args(id: note.id), returning: [String].self)
        if items.isEmpty {
            return .result(value: items, dialog: "'\(note.title)' has no open action items")
        }
        return .result(value: items, dialog: "Found \(items.count) action item\(items.count == 1 ? "" : "s") in '\(note.title)'")
    }
}
