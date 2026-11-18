import Foundation
import Security
import CryptoKit
import Photos

struct PortoError: LocalizedError {
    let message: String
    init(_ message: String) { self.message = message }
    var errorDescription: String? { message }
}

func hex<D: Sequence>(_ bytes: D) -> String where D.Element == UInt8 {
    bytes.map { String(format: "%02x", $0) }.joined()
}

struct Computer: Equatable {
    let ip: String
    let pin: String

    init?(link: String) {
        guard let c = URLComponents(string: link.trimmingCharacters(in: .whitespacesAndNewlines)),
              c.scheme?.lowercased() == "porto", c.host?.lowercased() == "ricevi",
              c.user == nil, c.password == nil, c.port == nil, c.fragment == nil, c.path.isEmpty,
              let items = c.queryItems, items.count == 2,
              let ip = items.first(where: { $0.name == "ip" })?.value,
              let pin = items.first(where: { $0.name == "pin" })?.value?.lowercased(),
              pin.utf8.count == 64,
              pin.utf8.allSatisfy({ (48...57).contains($0) || (97...102).contains($0) }) else { return nil }
        let parts = ip.split(separator: ".", omittingEmptySubsequences: false)
        let octets = parts.compactMap { UInt8($0) }
        guard octets.count == 4, octets.map({ String($0) }).joined(separator: ".") == ip,
              octets[0] == 10 || (octets[0] == 192 && octets[1] == 168)
              || (octets[0] == 172 && (16...31).contains(octets[1])) else { return nil }
        self.ip = ip
        self.pin = pin
    }
}

private final class Trust: NSObject, URLSessionTaskDelegate, @unchecked Sendable {
    let computer: Computer
    init(_ computer: Computer) { self.computer = computer }

    func urlSession(_ session: URLSession, didReceive challenge: URLAuthenticationChallenge,
                    completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void) {
        let space = challenge.protectionSpace
        guard space.authenticationMethod == NSURLAuthenticationMethodServerTrust,
              space.host == computer.ip, space.port == 53317,
              let trust = space.serverTrust,
              let leaf = (SecTrustCopyCertificateChain(trust) as? [SecCertificate])?.first,
              hex(SHA256.hash(data: SecCertificateCopyData(leaf) as Data)) == computer.pin else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }
        // The exact leaf chosen on the PC is trusted for this connection only.
        guard SecTrustSetAnchorCertificates(trust, [leaf] as CFArray) == errSecSuccess,
              SecTrustSetAnchorCertificatesOnly(trust, true) == errSecSuccess,
              SecTrustSetNetworkFetchAllowed(trust, false) == errSecSuccess,
              SecTrustEvaluateWithError(trust, nil) else {
            completionHandler(.cancelAuthenticationChallenge, nil)
            return
        }
        completionHandler(.useCredential, URLCredential(trust: trust))
    }

    func urlSession(_ session: URLSession, task: URLSessionTask,
                    willPerformHTTPRedirection response: HTTPURLResponse, newRequest request: URLRequest,
                    completionHandler: @escaping (URLRequest?) -> Void) {
        completionHandler(nil)
    }
}

enum Filter: String {
    case both, photo, video
    var label: String {
        switch self { case .both: return "Foto e video"; case .photo: return "Solo foto"; case .video: return "Solo video" }
    }
    var options: PHFetchOptions {
        let options = PHFetchOptions()
        if self == .both {
            options.predicate = NSPredicate(format: "mediaType == %d OR mediaType == %d", PHAssetMediaType.image.rawValue, PHAssetMediaType.video.rawValue)
        } else {
            options.predicate = NSPredicate(format: "mediaType == %d", self == .photo ? PHAssetMediaType.image.rawValue : PHAssetMediaType.video.rawValue)
        }
        options.includeHiddenAssets = false
        return options
    }
}

struct Original {
    let asset: String
    let name: String
    let url: URL
    let hash: String
    let size: Int64

    static func stage(_ asset: PHAsset, in folder: URL) async throws -> Original {
        try Task.checkCancellation()
        let type: PHAssetResourceType = asset.mediaType == .video ? .video : .photo
        let resources = PHAssetResource.assetResources(for: asset).filter { $0.type == type }
        guard resources.count == 1, let resource = resources.first else {
            throw PortoError("Originale assente o ambiguo.")
        }
        let target = folder.appendingPathComponent(UUID().uuidString)
        var keep = false
        defer { if !keep { try? FileManager.default.removeItem(at: target) } }
        let options = PHAssetResourceRequestOptions()
        options.isNetworkAccessAllowed = false
        try await withCheckedThrowingContinuation { (result: CheckedContinuation<Void, Error>) in
            PHAssetResourceManager.default().writeData(for: resource, toFile: target, options: options) { error in
                if let error = error { result.resume(throwing: error) } else { result.resume(returning: ()) }
            }
        }
        try Task.checkCancellation()
        let file = try FileHandle(forReadingFrom: target)
        defer { try? file.close() }
        var digest = SHA256()
        var size: Int64 = 0
        while let chunk = try file.read(upToCount: 1024 * 1024), !chunk.isEmpty {
            try Task.checkCancellation()
            size += Int64(chunk.count)
            digest.update(data: chunk)
        }
        guard size > 0 else { throw PortoError("Originale vuoto.") }
        keep = true
        return Original(asset: hex(SHA256.hash(data: Data(asset.localIdentifier.utf8))),
                        name: resource.originalFilename, url: target, hash: hex(digest.finalize()), size: size)
    }
}

final class Sender {
    private let computer: Computer
    private let network: URLSession

    init(_ computer: Computer) {
        self.computer = computer
        let config = URLSessionConfiguration.ephemeral
        config.urlCache = nil
        config.httpCookieStorage = nil
        config.urlCredentialStorage = nil
        config.connectionProxyDictionary = [:]
        config.requestCachePolicy = .reloadIgnoringLocalCacheData
        config.allowsCellularAccess = false
        config.tlsMinimumSupportedProtocolVersion = .TLSv12
        config.timeoutIntervalForRequest = 180
        config.timeoutIntervalForResource = 24 * 3600
        network = URLSession(configuration: config, delegate: Trust(computer), delegateQueue: nil)
    }
    func close() { network.invalidateAndCancel() }

    private func request(_ route: String, method: String, token: String? = nil, original: Original? = nil) throws -> URLRequest {
        var url = URLComponents()
        url.scheme = "https"; url.host = computer.ip; url.port = 53317; url.path = "/porto/v1/" + route
        if let item = original {
            url.queryItems = [URLQueryItem(name: "asset", value: item.asset), URLQueryItem(name: "version", value: item.hash)]
        }
        guard let address = url.url else { throw PortoError("Indirizzo del PC non valido.") }
        var r = URLRequest(url: address)
        r.httpMethod = method
        if let token = token { r.setValue("Bearer " + token, forHTTPHeaderField: "Authorization") }
        if let item = original {
            r.setValue(item.hash, forHTTPHeaderField: "X-Porto-SHA256")
            r.setValue(Data(item.name.utf8).base64EncodedString(), forHTTPHeaderField: "X-Porto-Name")
        }
        return r
    }
    func start(album: String, albumID: String, count: Int, device: String, filter: Filter) async throws -> String {
        var r = try request("session", method: "POST")
        r.setValue("application/json", forHTTPHeaderField: "Content-Type")
        r.httpBody = try JSONSerialization.data(withJSONObject: ["album": album, "albumId": hex(SHA256.hash(data: Data(albumID.utf8))), "count": count, "device": device, "selection": filter.rawValue])
        let (data, response) = try await network.data(for: r)
        try Self.requireSuccess(response)
        struct Reply: Decodable { let token: String }
        let token = try JSONDecoder().decode(Reply.self, from: data).token
        guard !token.isEmpty, token.utf8.count <= 1024, token.utf8.allSatisfy({ (33...126).contains($0) }) else {
            throw PortoError("Conferma del PC non valida.")
        }
        return token
    }
    enum Outcome { case new, present }
    func send(_ item: Original, token: String) async throws -> Outcome {
        let check = try request("check", method: "POST", token: token, original: item)
        let (_, reply) = try await network.data(for: check)
        let code = (reply as? HTTPURLResponse)?.statusCode
        if code == 204 { return .present }
        guard code == 404 else { throw PortoError("Verifica sul PC interrotta (HTTP " + String(code ?? 0) + ").") }
        var r = try request("file", method: "POST", token: token, original: item)
        r.setValue("application/octet-stream", forHTTPHeaderField: "Content-Type")
        r.setValue(String(item.size), forHTTPHeaderField: "Content-Length")
        let (_, response) = try await network.upload(for: r, fromFile: item.url)
        try Self.requireSuccess(response)
        return (response as? HTTPURLResponse)?.statusCode == 204 ? .present : .new
    }
    private static func requireSuccess(_ response: URLResponse) throws {
        guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
            throw PortoError("Il PC ha interrotto la richiesta (HTTP " + String((response as? HTTPURLResponse)?.statusCode ?? 0) + ").")
        }
    }
}
