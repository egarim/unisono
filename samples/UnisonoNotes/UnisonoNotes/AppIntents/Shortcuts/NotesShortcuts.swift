import AppIntents

/// Siri phrases for Unisono Notes. These appear in the Shortcuts app without any user setup.
struct NotesShortcuts: AppShortcutsProvider {
    @AppShortcutsBuilder
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: CreateNoteIntent(),
            phrases: [
                "Create a note in \(.applicationName)",
                "New note in \(.applicationName)"
            ],
            shortTitle: "Create Note",
            systemImageName: "square.and.pencil"
        )

        AppShortcut(
            intent: FindNotesIntent(),
            phrases: [
                "Find notes in \(.applicationName)",
                "Search my notes in \(.applicationName)"
            ],
            shortTitle: "Find Notes",
            systemImageName: "magnifyingglass"
        )

        AppShortcut(
            intent: ExtractActionItemsIntent(),
            phrases: [
                "Extract action items in \(.applicationName)",
                "What are my action items in \(.applicationName)"
            ],
            shortTitle: "Action Items",
            systemImageName: "checklist"
        )

        AppShortcut(
            intent: AppendToNoteIntent(),
            phrases: [
                "Add to a note in \(.applicationName)",
                "Append to a note in \(.applicationName)"
            ],
            shortTitle: "Append to Note",
            systemImageName: "text.append"
        )
    }
}
