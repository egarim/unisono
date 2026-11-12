import AppIntents

/// Defines Siri shortcut phrases for all TaskTracker intents.
/// These phrases are automatically available to Siri without any user setup.
struct TaskTrackerShortcuts: AppShortcutsProvider {
    @AppShortcutsBuilder
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: CreateTaskIntent(),
            phrases: [
                "Create a task in \(.applicationName)",
                "Add a new task in \(.applicationName)",
                "New task in \(.applicationName)",
                "Add to-do in \(.applicationName)"
            ],
            shortTitle: "Create Task",
            systemImageName: "plus.circle"
        )

        AppShortcut(
            intent: OpenTaskIntent(),
            phrases: [
                "Open a task in \(.applicationName)",
                "Show task in \(.applicationName)",
                "View task in \(.applicationName)"
            ],
            shortTitle: "Open Task",
            systemImageName: "eye"
        )

        AppShortcut(
            intent: CompleteTaskIntent(),
            phrases: [
                "Complete a task in \(.applicationName)",
                "Mark task done in \(.applicationName)",
                "Finish task in \(.applicationName)",
                "Check off task in \(.applicationName)"
            ],
            shortTitle: "Complete Task",
            systemImageName: "checkmark.circle"
        )

        AppShortcut(
            intent: ListTasksIntent(),
            phrases: [
                "Show my tasks in \(.applicationName)",
                "List tasks in \(.applicationName)",
                "What are my tasks in \(.applicationName)",
                "Show to-do list in \(.applicationName)",
                "Show my tasks for \(\.$due) in \(.applicationName)",
                "What are my tasks for \(\.$due) in \(.applicationName)",
                "Show my \(\.$due) tasks in \(.applicationName)"
            ],
            shortTitle: "List Tasks",
            systemImageName: "list.bullet"
        )

        AppShortcut(
            intent: DeleteTaskIntent(),
            phrases: [
                "Delete a task in \(.applicationName)",
                "Remove a task in \(.applicationName)"
            ],
            shortTitle: "Delete Task",
            systemImageName: "trash"
        )

        AppShortcut(
            intent: DeleteCompletedTasksIntent(),
            phrases: [
                "Delete completed tasks in \(.applicationName)",
                "Clear completed tasks in \(.applicationName)"
            ],
            shortTitle: "Delete Completed",
            systemImageName: "trash.slash"
        )

        AppShortcut(
            intent: SearchTasksIntent(),
            phrases: [
                "Search tasks in \(.applicationName)",
                "Find task in \(.applicationName)",
                "Look up task in \(.applicationName)"
            ],
            shortTitle: "Search Tasks",
            systemImageName: "magnifyingglass"
        )

        AppShortcut(
            intent: SetDueDateIntent(),
            phrases: [
                "Set due date in \(.applicationName)",
                "Change deadline in \(.applicationName)",
                "Update due date in \(.applicationName)"
            ],
            shortTitle: "Set Due Date",
            systemImageName: "calendar"
        )
    }
}
