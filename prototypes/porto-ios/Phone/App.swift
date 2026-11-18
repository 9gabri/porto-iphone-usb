import UIKit
import Photos

@main
final class AppDelegate: UIResponder, UIApplicationDelegate {
    var window: UIWindow?
    let screen = AlbumScreen()

    func application(_ application: UIApplication, didFinishLaunchingWithOptions options: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        let window = UIWindow(frame: UIScreen.main.bounds)
        window.rootViewController = UINavigationController(rootViewController: screen)
        self.window = window
        window.makeKeyAndVisible()
        return true
    }

    func application(_ app: UIApplication, open url: URL, options: [UIApplication.OpenURLOptionsKey: Any] = [:]) -> Bool {
        screen.offer(url.absoluteString)
    }
}

@MainActor private func label(_ text: String, _ style: UIFont.TextStyle = .body) -> UILabel {
    let view = UILabel()
    view.text = text
    view.numberOfLines = 0
    view.font = .preferredFont(forTextStyle: style)
    view.adjustsFontForContentSizeCategory = true
    return view
}

@MainActor private func button(_ text: String, primary: Bool = false) -> UIButton {
    var config: UIButton.Configuration = primary ? .filled() : .tinted()
    config.title = text
    config.cornerStyle = .medium
    config.buttonSize = .large
    return UIButton(configuration: config)
}

@MainActor
final class AlbumScreen: UIViewController {
    private var computer: Computer?
    private var album: PHAssetCollection?
    private var transfer: Task<Void, Never>?
    private let machine = label("Sul PC avvia il ricevitore Porto Album. Inquadra il suo QR con Fotocamera.")
    private let enter = button("Inserisci collegamento PC")
    private let choose = button("Scegli album")
    private let selected = label("Nessun album scelto")
    private let kinds = [Filter.both, .photo, .video]
    private lazy var filters = UISegmentedControl(items: kinds.map { $0.label })
    private let send = button("Copia album sul PC", primary: true)
    private let stop = button("Pausa")
    private let state = label("Durante la copia tieni Porto aperto. Gli originali disponibili solo su iCloud vengono segnalati.")
    private let defaults = UserDefaults.standard

    override func viewDidLoad() {
        super.viewDidLoad()
        title = "Porto Album"
        view.backgroundColor = .systemBackground
        let scroll = UIScrollView()
        let stack = UIStackView(arrangedSubviews: [label("Computer", .headline), machine, enter,
            label("Album", .headline), selected, choose, filters, send, stop, state])
        stack.axis = .vertical
        stack.spacing = 12
        scroll.translatesAutoresizingMaskIntoConstraints = false
        stack.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(scroll)
        scroll.addSubview(stack)
        NSLayoutConstraint.activate([
            scroll.leadingAnchor.constraint(equalTo: view.safeAreaLayoutGuide.leadingAnchor),
            scroll.trailingAnchor.constraint(equalTo: view.safeAreaLayoutGuide.trailingAnchor),
            scroll.topAnchor.constraint(equalTo: view.safeAreaLayoutGuide.topAnchor),
            scroll.bottomAnchor.constraint(equalTo: view.safeAreaLayoutGuide.bottomAnchor),
            stack.leadingAnchor.constraint(equalTo: scroll.contentLayoutGuide.leadingAnchor, constant: 18),
            stack.trailingAnchor.constraint(equalTo: scroll.contentLayoutGuide.trailingAnchor, constant: -18),
            stack.topAnchor.constraint(equalTo: scroll.contentLayoutGuide.topAnchor, constant: 16),
            stack.bottomAnchor.constraint(equalTo: scroll.contentLayoutGuide.bottomAnchor, constant: -20),
            stack.widthAnchor.constraint(equalTo: scroll.frameLayoutGuide.widthAnchor, constant: -36)
        ])
        view.tintColor = .systemTeal
        filters.selectedSegmentIndex = kinds.firstIndex { $0.rawValue == defaults.string(forKey: "filter") } ?? 0
        enter.addAction(UIAction { [weak self] _ in self?.manual() }, for: .touchUpInside)
        choose.addAction(UIAction { [weak self] _ in Task { await self?.chooseAlbum() } }, for: .touchUpInside)
        send.addAction(UIAction { [weak self] _ in self?.begin() }, for: .touchUpInside)
        stop.addAction(UIAction { [weak self] _ in
            self?.state.text = "Pausa richiesta. Attendo la chiusura dell’operazione in corso…"
            self?.transfer?.cancel()
        }, for: .touchUpInside)
        updateButtons()
    }

    private func updateButtons() {
        let idle = transfer == nil
        enter.isEnabled = idle; choose.isEnabled = idle; filters.isEnabled = idle
        send.isEnabled = idle && album != nil && computer != nil
        stop.isHidden = idle
    }
    private func manual() {
        let alert = UIAlertController(title: "Collegamento del PC", message: "Incolla il collegamento mostrato da Porto sul tuo PC.", preferredStyle: .alert)
        alert.addTextField { field in field.placeholder = "porto://ricevi?…"; field.autocapitalizationType = .none; field.autocorrectionType = .no }
        alert.addAction(UIAlertAction(title: "Annulla", style: .cancel))
        alert.addAction(UIAlertAction(title: "Continua", style: .default) { [weak self, weak alert] _ in
            let text = alert?.textFields?.first?.text ?? ""
            DispatchQueue.main.async { _ = self?.offer(text) }
        })
        present(alert, animated: true)
    }

    @discardableResult func offer(_ text: String) -> Bool {
        loadViewIfNeeded()
        guard transfer == nil, let pc = Computer(link: text) else {
            if transfer == nil { state.text = "Collegamento non valido. Usa il QR o il collegamento del ricevitore Porto Album." }
            return false
        }
        // Receiving a link never starts a transfer or requests Photos access.
        let alert = UIAlertController(title: "Collegare questo PC?", message: "Verifica sul PC l’indirizzo " + pc.ip + " e l’inizio dell’impronta " + pc.pin.prefix(12).uppercased() + ".", preferredStyle: .alert)
        alert.addAction(UIAlertAction(title: "Annulla", style: .cancel))
        alert.addAction(UIAlertAction(title: "Collega", style: .default) { [weak self] _ in
            self?.computer = pc
            self?.machine.text = "PC selezionato · " + pc.ip
            self?.updateButtons()
        })
        guard let host = navigationController?.visibleViewController, host.presentedViewController == nil else {
            state.text = "Chiudi la finestra aperta e inquadra di nuovo il QR."
            return false
        }
        host.present(alert, animated: true)
        return true
    }

    private func chooseAlbum() async {
        guard transfer == nil else { return }
        let permission = await PHPhotoLibrary.requestAuthorization(for: .readWrite)
        guard permission == .authorized || permission == .limited else {
            state.text = "L’accesso alle foto è disattivato. Puoi gestirlo nelle Impostazioni di iPhone."
            return
        }
        if permission == .limited { state.text = "Accesso limitato. Porto vede solo gli elementi autorizzati." }
        let query = PHAssetCollection.fetchAssetCollections(with: .album, subtype: .any, options: nil)
        var albums: [PHAssetCollection] = []
        query.enumerateObjects { album, _, _ in
            if album.assetCollectionSubtype != .albumCloudShared { albums.append(album) }
        }
        guard !albums.isEmpty else { state.text = "Nessun album personale visibile con i permessi attuali."; return }
        albums.sort { ($0.localizedTitle ?? "").localizedStandardCompare($1.localizedTitle ?? "") == .orderedAscending }
        let picker = AlbumList(albums: albums) { [weak self] value in
            self?.album = value
            self?.selected.text = value.localizedTitle ?? "Album"
            self?.updateButtons()
        }
        navigationController?.pushViewController(picker, animated: true)
    }

    private func begin() {
        guard transfer == nil, let pc = computer, let selectedAlbum = album else { return }
        let filter = kinds[filters.selectedSegmentIndex]
        defaults.set(filter.rawValue, forKey: "filter")
        transfer = Task { [weak self] in
            guard let self = self else { return }
            UIApplication.shared.isIdleTimerDisabled = true
            defer { self.transfer = nil; self.updateButtons(); UIApplication.shared.isIdleTimerDisabled = false }
            await self.copy(selectedAlbum, to: pc, filter: filter)
        }
        updateButtons()
    }

    private func copy(_ album: PHAssetCollection, to pc: Computer, filter: Filter) async {
        let permission = PHPhotoLibrary.authorizationStatus(for: .readWrite)
        guard permission == .limited || permission == .authorized else { state.text = "Accesso alle foto revocato."; return }
        var verified = 0, existing = 0, skipped = 0
        var firstIssue = ""
        let assets = PHAsset.fetchAssets(in: album, options: filter.options)
        let total = assets.count
        guard total > 0 else { state.text = "L’album visibile non contiene elementi del tipo scelto."; return }
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent("porto-album-originals", isDirectory: true)
        let sender = Sender(pc)
        defer { sender.close() }
        do {
            // Only this app's dedicated staging directory contains disposable originals.
            if FileManager.default.fileExists(atPath: folder.path) { try FileManager.default.removeItem(at: folder) }
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: false)
            defer { try? FileManager.default.removeItem(at: folder) }
            let device = defaults.string(forKey: "archiveIdentity") ?? UUID().uuidString
            defaults.set(device, forKey: "archiveIdentity")
            state.text = "Accetta sul PC l’invio di " + String(total) + " elementi."
            let token = try await sender.start(album: album.localizedTitle ?? "Album", albumID: album.localIdentifier,
                                               count: total, device: device, filter: filter)
            for index in 0..<total {
                try Task.checkCancellation()
                state.text = "Preparo " + String(index + 1) + " di " + String(total) + ". Verificati sul PC: " + String(verified) + "."
                let asset = assets.object(at: index)
                let item: Original
                do { item = try await Original.stage(asset, in: folder) }
                catch {
                    try Task.checkCancellation()
                    skipped += 1
                    if firstIssue.isEmpty { firstIssue = "Elemento " + String(index + 1) + " · " + error.localizedDescription }
                    continue
                }
                defer { try? FileManager.default.removeItem(at: item.url) }
                state.text = "Confronto e copia di " + item.name + " · " + String(index + 1) + "/" + String(total)
                if try await sender.send(item, token: token) == .present { existing += 1 }
                verified += 1
            }
            let summary = String(verified) + "/" + String(total) + " verificati. Già presenti: " + String(existing) + "."
            let visibility = permission == .limited ? " Accesso limitato agli elementi autorizzati." : ""
            if skipped == 0 { state.text = "Selezione completata. " + summary + visibility }
            else { state.text = "Copia incompleta. " + summary + " Originali non leggibili: " + String(skipped) + ". " + firstIssue + visibility }
        } catch {
            let reason = Task.isCancelled ? "Copia in pausa." : "Invio interrotto. " + error.localizedDescription
            state.text = reason + " Confermati: " + String(verified) + "/" + String(total) + ". Ripremi Copia album con la stessa cartella sul PC."
        }
    }
}

@MainActor
final class AlbumList: UITableViewController {
    private let albums: [PHAssetCollection]
    private let chosen: (PHAssetCollection) -> Void
    init(albums: [PHAssetCollection], chosen: @escaping (PHAssetCollection) -> Void) {
        self.albums = albums; self.chosen = chosen
        super.init(style: .insetGrouped)
        title = "Scegli album"
    }
    required init?(coder: NSCoder) { fatalError("Storyboard non previsto") }
    override func tableView(_ tableView: UITableView, numberOfRowsInSection section: Int) -> Int { albums.count }
    override func tableView(_ tableView: UITableView, cellForRowAt indexPath: IndexPath) -> UITableViewCell {
        let cell = tableView.dequeueReusableCell(withIdentifier: "album") ?? UITableViewCell(style: .default, reuseIdentifier: "album")
        var content = cell.defaultContentConfiguration()
        content.text = albums[indexPath.row].localizedTitle ?? "Album"
        cell.contentConfiguration = content
        return cell
    }
    override func tableView(_ tableView: UITableView, didSelectRowAt indexPath: IndexPath) {
        chosen(albums[indexPath.row])
        navigationController?.popViewController(animated: true)
    }
}
