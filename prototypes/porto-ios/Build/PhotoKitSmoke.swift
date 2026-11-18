#if targetEnvironment(simulator)
import UIKit
import Photos
import CryptoKit

// Only the simulator test target contains this entry point or writes to a photo library.
@main
final class SmokeApp: UIResponder, UIApplicationDelegate {
    var window: UIWindow?
    func application(_ application: UIApplication, didFinishLaunchingWithOptions opts: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        window = UIWindow(frame: UIScreen.main.bounds)
        window?.rootViewController = UIViewController()
        window?.makeKeyAndVisible()
        Task { @MainActor in
            let result: String
            do { try await run(); result = "OK" }
            catch { result = "Errore: " + error.localizedDescription }
            let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
            try result.write(to: docs.appendingPathComponent("esito.txt"), atomically: true, encoding: .utf8)
        }
        return true
    }

    @MainActor func run() async throws {
        guard PHPhotoLibrary.authorizationStatus(for: .readWrite) == .authorized else { throw PortoError("Accesso simulatore assente") }
        let pngs = [UIColor.red, .blue].map { color in
            UIGraphicsImageRenderer(size: CGSize(width: 32, height: 32)).pngData { context in
                color.setFill(); context.fill(CGRect(x: 0, y: 0, width: 32, height: 32))
            }
        }
        let name = "Porto collaudo " + UUID().uuidString
        try await PHPhotoLibrary.shared().performChanges {
            let group = PHAssetCollectionChangeRequest.creationRequestForAssetCollection(withTitle: name)
            for (i, data) in pngs.enumerated() {
                let item = PHAssetCreationRequest.forAsset()
                let opts = PHAssetResourceCreationOptions()
                opts.originalFilename = "sample-\(i).png"
                item.addResource(with: .photo, data: data, options: opts)
                if let placeholder = item.placeholderForCreatedAsset { group.addAssets([placeholder] as NSArray) }
            }
        }
        let options = PHFetchOptions()
        options.predicate = NSPredicate(format: "title == %@", name)
        guard let album = PHAssetCollection.fetchAssetCollections(with: .album, subtype: .any, options: options).firstObject
            else { throw PortoError("Album di prova assente") }
        let items = PHAsset.fetchAssets(in: album, options: Filter.both.options)
        guard items.count == 2, PHAsset.fetchAssets(in: album, options: Filter.video.options).count == 0
            else { throw PortoError("Conteggio errato per album o filtro") }
        var expected = Set(pngs.map { hex(SHA256.hash(data: $0)) })
        for i in 0..<items.count {
            let staged = try await Original.stage(items.object(at: i), in: FileManager.default.temporaryDirectory)
            defer { try? FileManager.default.removeItem(at: staged.url) }
            guard expected.remove(staged.hash) != nil else { throw PortoError("Originale alterato") }
        }
    }
}
#endif
