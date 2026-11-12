import Foundation

/// App-specific errors; the generic ones (app not ready, tool failed) come from UnisonoError.
enum IntentError: Error, CustomLocalizedStringResourceConvertible {
    case taskNotFound

    var localizedStringResource: LocalizedStringResource {
        switch self {
        case .taskNotFound: return "The task could not be found."
        }
    }
}
