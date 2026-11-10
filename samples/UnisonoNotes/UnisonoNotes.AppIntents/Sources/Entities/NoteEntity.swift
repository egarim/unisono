import AppIntents

/// Wire shape of a note as the C# `NoteDto` serialises it (camelCase keys).
struct NoteDto: Codable {
    var id: String
    var title: String
    var body: String
    var createdAt: Date
    var updatedAt: Date?
    var openActionItems: Int
}

/// A note as Siri, Shortcuts and Spotlight see it.
struct NoteEntity: AppEntity {
    static var defaultQuery = NoteEntityQuery()
    static var typeDisplayRepresentation: TypeDisplayRepresentation = "Note"

    var id: String

    @Property(title: "Title")
    var title: String

    @Property(title: "Body")
    var body: String

    @Property(title: "Created")
    var createdAt: Date

    @Property(title: "Open Action Items")
    var openActionItems: Int

    var displayRepresentation: DisplayRepresentation {
        let preview = body.split(separator: "\n").map { $0.trimmingCharacters(in: .whitespaces) }
            .first { !$0.isEmpty && !$0.hasPrefix("- [") } ?? ""
        return DisplayRepresentation(title: "\(title)", subtitle: "\(preview)")
    }

    init(_ dto: NoteDto) {
        self.id = dto.id
        self.title = dto.title
        self.body = dto.body
        self.createdAt = dto.createdAt
        self.openActionItems = dto.openActionItems
    }
}
