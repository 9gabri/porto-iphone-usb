# Prove della candidata 0.1.0-rc.11

Windows 11 x64, SDK .NET 10.0.403, runtime 10.0.14. Dati sintetici in cartelle NTFS isolate. Nessun telefono, archivio personale, credenziale reale o media della cliente letto durante sviluppo e test.

## Sequenza effettiva delle verifiche

- **445/445 test ordinari superati** nella regressione completa dopo le correzioni al messaggio di selezione vuota, ai ruoli Foto sconosciuti e ai test TLS.
- **Quattro prove estese superate** nella precedente esecuzione: stream di 4 GiB + 17 byte letto e hashato (3,66 s); inventario di 50.000 elementi e 250 GB logici simulati (11,85 s); 2.000 file fisici copiati e verificati una seconda volta senza copie aggiuntive (464,70 s); file di 4.294.971.392 byte scritto, riletto e pubblicato su NTFS (30,27 s). Tempi di questa macchina, non stime per i telefoni.
- Quella prima esecuzione estesa non era interamente verde: 442/449. Aveva rilevato una regressione nel messaggio di inventario vuoto con copie storiche mancanti e sei aspettative di versione ancora fissate a rc.10 nei test. Entrambe corrette, poi verificate nella suite ordinaria 445/445. I quattro casi estesi non dipendono da tali correzioni. Non viene dichiarata una singola esecuzione finale 449/449.
- Dopo la suite ordinaria sono state verificate separatamente le ultime correzioni al recupero e agli errori dei metadati iPhone: **37/37 casi Apple e 6/6 casi del motore galleria**, compresi quattro nuovi casi di regressione. Le due anomalie erano state riprodotte prima della correzione. Dettagli in test-results.txt e REVIEW-RC11.md.

Build test e applicazione senza avvisi o errori. **Nove smoke UI superati**: avvio, recupero, pausa, incompleto USB, esito cartella, filtri foto/video, navigazione cartelle, esito galleria iPhone e scelta album Foto. Screenshot ispezionati; nessuna enumerazione hardware. **Quattro prove confezionamento superate**: interruzione compressione, ZIP invalido, archivio esistente preservato, successo con checksum. Non sono sommati alla suite applicativa.

[Output](test-results.txt) · [Revisione e controesempi](REVIEW-RC11.md).

## Nuove coperture

- Navigazione USB per livello: 60.000 discendenti estranei non visitati; copie limitate al ramo selezionato, ID rimappati, percorsi duplicati/mancanti, inventari incompleti, timeout e cancellazione del worker.
- Album WPD nativi immagine/video/misti: riferimenti espliciti, elenco vuoto, duplicati, cicli, membri mancanti e tipi non validi; nessun ampliamento implicito alla memoria intera. Fixture COM native e passaggio nel worker.
- Cataloghi Foto sintetici: ruolo video distinto da immagine/Live Photo, appartenenza agli album personali e cartelle genitrici, campi sconosciuti, UUID duplicati, schema non riconosciuto, percorsi ostili, originali assenti e cambi del catalogo.
- Solo video non controlla gli originali di 10.000 foto escluse. Gli elementi di ruolo ignoto non diventano video grazie all'estensione MOV e restano visibili come non risolti. I metadati delle foto escluse non vengono salvati nell'inventario della selezione video.
- Database/WAL: integrità SQLite, checksum nei due ordini di byte, commit completo, frame/header troncati, salt errato, journal attivo, variazione fra acquisizioni e cancellazione. Dati esclusivamente sintetici e in memoria durante la lettura.
- TLS Apple: credenziali sintetiche effimere, pin del dispositivo, mutua autenticazione richiesta, rifiuto della firma handshake SHA-1, handshake bloccato cancellabile. Protocollo plist e AFC con framing, limiti di memoria, UTF-8 invalido e nessun comando di scrittura.
- Confronto automatico con vecchie copie, comprese risorse Live precedenti conservate, selezione vuota, mancanti noti, cambi di appartenenza e ripetizione dopo errore senza duplicazioni spurie.

## Regressioni conservate

SHA-256, EOF, troncamenti, letture discordanti, parser MOV/MP4/M4V e JPEG/PNG/HEIF, copie identiche distinte, collisioni, ripresa, arresti del processo nei punti critici, cataloghi corrotti, percorsi ostili, hardlink/junction, permessi, disco pieno, selezioni foto/video e obblighi storici. Nessuna riduzione delle protezioni Windows.

## Riproduzione e pacchetto

`scripts/dev.ps1 test -Large` esegue tutti i casi, inclusi quelli estesi. Filtri utili: `--filter=apple`, `--filter=gallery import`, `--filter=USB navigation`. `tests/PackageStagingTests.ps1` copre i quattro casi packaging. `scripts/package.ps1` richiede tag/versione coerenti e checkout pulito, crea un nuovo stage e controlla manifest, checksum, interop, worker e nove stati UI.

La dipendenza TLS BouncyCastle 2.7.0 è fissata con contentHash e licenze incluse; restore NuGet completato senza avvisi di vulnerabilità. Il riscaricamento pubblico e l'avvio del pacchetto sono registrati separatamente in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md) dopo la pubblicazione. Le vecchie release restano immutate.

## Limiti delle prove

Il nuovo trasporto Apple, lo schema iOS, gli album reali, gli originali cloud e la ripresa dopo scollegamento fisico devono ancora essere collaudati su dispositivi autorizzati. Non è stata provata compatibilità universale iPhone/Android. Il catalogo Foto privato può cambiare o non essere esposto; raccolte intelligenti/condivise e album privati delle app non sono universalmente disponibili. Nascosti/Eliminati sono esclusi con conteggi.

Le letture ripetute e l'integrità del database non dimostrano uno snapshot atomico della libreria. I controlli dei media sono strutturali e dei byte, non decodifica completa né certificazione degli originali. Nessun totale prestabilito, garanzia di assenza di bug o completezza dell'intera libreria cloud. Capacità e confini in [GALLERY-IMPORT.md](GALLERY-IMPORT.md).
