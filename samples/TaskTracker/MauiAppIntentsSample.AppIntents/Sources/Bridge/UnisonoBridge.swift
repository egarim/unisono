// UnisonoBridge.swift
// Generic bridge between native App Intents code and the .NET Unisono tool registry.
//
// This file is the same in every app; the app-specific Swift (intents, entities, enums) sits next to it.
// The fixed Objective-C names UnisonoHost and UnisonoBridge are what the static C# binding
// (Unisono.AppIntents.Binding) talks to, so no per-app binding is needed. Every payload is JSON text,
// which is also what the Android (AppFunctions) and Windows (App Actions) adapters exchange with C#.

import Foundation

/// Implemented in C# by Unisono.AppIntents.
/// completion(result, error): on success `result` is the JSON result (nil for a void tool) and `error` is nil;
/// on failure `error` carries a message suitable for showing to the user.
@objc(UnisonoHost) public protocol UnisonoHost: AnyObject {
    func invoke(_ tool: String, argumentsJSON: String, completion: @escaping (String?, String?) -> Void)
    func entities(_ type: String, ids: [String], completion: @escaping (String?, String?) -> Void)
    func searchEntities(_ type: String, text: String, completion: @escaping (String?, String?) -> Void)
    func suggestedEntities(_ type: String, completion: @escaping (String?, String?) -> Void)
}

/// Holds the C# host. Set once at app start by `UnisonoAppIntents.Attach(registry)`.
@objc(UnisonoBridge) public final class UnisonoBridge: NSObject {
    @objc public static let shared = UnisonoBridge()
    @objc public weak var host: UnisonoHost?
    private override init() { super.init() }
}

public enum UnisonoError: Error, CustomLocalizedStringResourceConvertible {
    case appNotReady
    case failed(String)
    case badResult(String)

    public var localizedStringResource: LocalizedStringResource {
        switch self {
        case .appNotReady: return "The app is not ready yet. Open it once and try again."
        case .failed(let message): return LocalizedStringResource(stringLiteral: message)
        case .badResult(let message): return LocalizedStringResource(stringLiteral: "Unexpected result: \(message)")
        }
    }
}

/// Typed async helpers used by the intents and entity queries.
public enum UnisonoTools {
    public static func host() throws -> UnisonoHost {
        guard let host = UnisonoBridge.shared.host else { throw UnisonoError.appNotReady }
        return host
    }

    /// Invokes a tool and decodes its JSON result.
    public static func invoke<Args: Encodable, Result: Decodable>(_ tool: String, _ args: Args, returning: Result.Type) async throws -> Result {
        guard let json = try await invokeRaw(tool, try encode(args)) else {
            throw UnisonoError.badResult("\(tool) returned no value")
        }
        return try decode(Result.self, from: json)
    }

    /// Invokes a tool whose result is not needed.
    public static func invoke<Args: Encodable>(_ tool: String, _ args: Args) async throws {
        _ = try await invokeRaw(tool, try encode(args))
    }

    /// Invokes a tool with pre-encoded JSON arguments; returns the raw JSON result.
    public static func invokeRaw(_ tool: String, _ argumentsJSON: String) async throws -> String? {
        let host = try host()
        return try await withCheckedThrowingContinuation { continuation in
            host.invoke(tool, argumentsJSON: argumentsJSON) { result, error in
                if let error { continuation.resume(throwing: UnisonoError.failed(error)) }
                else { continuation.resume(returning: result) }
            }
        }
    }

    public static func entities<E: Decodable>(_ type: String, ids: [String], as: E.Type = E.self) async throws -> [E] {
        let host = try host()
        return try await list(E.self) { host.entities(type, ids: ids, completion: $0) }
    }

    public static func search<E: Decodable>(_ type: String, _ text: String, as: E.Type = E.self) async throws -> [E] {
        let host = try host()
        return try await list(E.self) { host.searchEntities(type, text: text, completion: $0) }
    }

    public static func suggested<E: Decodable>(_ type: String, as: E.Type = E.self) async throws -> [E] {
        let host = try host()
        return try await list(E.self) { host.suggestedEntities(type, completion: $0) }
    }

    private static func list<E: Decodable>(_: E.Type, _ call: (@escaping (String?, String?) -> Void) -> Void) async throws -> [E] {
        let json: String? = try await withCheckedThrowingContinuation { continuation in
            call { result, error in
                if let error { continuation.resume(throwing: UnisonoError.failed(error)) }
                else { continuation.resume(returning: result) }
            }
        }
        guard let json else { return [] }
        return try decode([E].self, from: json)
    }

    // MARK: JSON

    public static let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }()

    public static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .custom { decoder in
            let text = try decoder.singleValueContainer().decode(String.self)
            guard let date = parseDate(text) else {
                throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "Unrecognised date: \(text)"))
            }
            return date
        }
        return decoder
    }()

    public static func encode<T: Encodable>(_ value: T) throws -> String {
        String(decoding: try encoder.encode(value), as: UTF8.self)
    }

    public static func decode<T: Decodable>(_ type: T.Type, from json: String) throws -> T {
        try decoder.decode(type, from: Data(json.utf8))
    }

    /// .NET writes dates as "2026-11-10T04:42:14.893174Z", "2026-11-17T00:00:00+03:00" or, for an
    /// unspecified kind, "2026-11-17T00:00:00". Fractional seconds are cut to 3 digits; a missing offset
    /// is read as local time.
    static func parseDate(_ text: String) -> Date? {
        var s = text
        if let dot = s.firstIndex(of: ".") {
            let start = s.index(after: dot)
            var end = start
            while end < s.endIndex, s[end].isNumber { end = s.index(after: end) }
            let digits = s[start..<end]
            if digits.count > 3 { s.replaceSubrange(start..<end, with: String(digits.prefix(3))) }
        }
        let hasOffset = s.hasSuffix("Z") || s.range(of: #"[+-]\d\d:?\d\d$"#, options: .regularExpression) != nil
        if hasOffset {
            return isoFractional.date(from: s) ?? iso.date(from: s)
        }
        return localFractional.date(from: s) ?? local.date(from: s)
    }

    private static let iso: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter(); f.formatOptions = [.withInternetDateTime]; return f
    }()
    private static let isoFractional: ISO8601DateFormatter = {
        let f = ISO8601DateFormatter(); f.formatOptions = [.withInternetDateTime, .withFractionalSeconds]; return f
    }()
    private static let local: DateFormatter = {
        let f = DateFormatter(); f.locale = Locale(identifier: "en_US_POSIX"); f.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"; return f
    }()
    private static let localFractional: DateFormatter = {
        let f = DateFormatter(); f.locale = Locale(identifier: "en_US_POSIX"); f.dateFormat = "yyyy-MM-dd'T'HH:mm:ss.SSS"; return f
    }()
}
