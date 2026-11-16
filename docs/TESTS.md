# Prove della candidata 0.1.0-rc.10

Windows 11 x64, SDK .NET 10.0.403, runtime 10.0.14. Dati sintetici in cartelle NTFS isolate nel progetto. Nessun telefono, archivio personale o media della cliente letto o modificato.

## Risultati effettivi

**Suite completa 387/387 superata**, in un’unica esecuzione con `--large`: 383 casi ordinari e quattro estesi. Comprende le regressioni precedenti e tutte le nuove prove foto, filtri e scope USB. Le modifiche successive al build della suite riguardano soltanto documentazione e uno smoke UI aggiuntivo del selettore cartella; il codice Core/USB verificato è quello della candidata.

Build test e applicazione senza errori né avvisi. **Sette smoke UI superati**: cartella, recupero USB, pausa, incompleto USB, risultato cartella, modalità foto/video con scope USB e finestra scelta cartella. Screenshot controllati; nessuna enumerazione hardware. **Quattro prove confezionamento superate**: interruzione compressione, ZIP invalido, archivio già esistente preservato, successo con checksum. Non sono sommati ai 387 casi della suite.

[Output](test-results.txt) · [Revisione indipendente e controesempi](REVIEW-RC10.md).

## Nuove coperture

- JPEG, PNG, HEIC/HEIF HEVC sintetici validi; troncamenti, magic/brand incoerenti, CRC/Adler, scanline, ordini dei chunk, code compresse, extent, item primari, configurazioni e geometria grid. Limite NAL cumulativo e cancellazione.
- Solo video / Solo foto / Foto e video: letture e pubblicazioni dei soli tipi scelti; inventario completo conservato; contraddizioni di tipo non certificate nemmeno nel riuso di copie precedenti.
- 193 file video sintetici precedenti conservati byte per byte nell’importazione da cartella foto. Molteplicità delle foto identiche, cataloghi precedenti, nomi generati e validazione del namespace.
- Namespace diversi con ID oggetto uguali, serializzazione legacy invariata, assenza di classificazione Apple indiscriminata sui telefoni non Apple.
- Sottocartella USB: esclusione dei fratelli, ID transitori cambiati, PUID cambiati, cartella mancante, duplicata, spostata, parent mancanti, cicli, etichette con separatori e inoltro del recupero.
- Discovery WPD multiproduttore, identificatori mancanti/duplicati, elenchi troncati, metadati incompleti. Fixture COM native per audio/document/generic file; video generici non supportati ancora ambigui.

I test hanno riprodotto controesempi prima delle correzioni. La revisione documenta anche una fixture inizialmente errata, distinta dai difetti confermati. I test di collegamenti/junction hanno permessi limitati alle fixture necessarie; nessuna protezione Windows ridotta.

## Regressioni ed estese

SHA-256, EOF, troncamenti, letture discordanti, dimensioni native, QuickTime, timeout USB, cancellazione, priorità di recupero, processi terminati nei punti di pubblicazione, journal/cataloghi corrotti, percorsi ostili, hardlink/junction, collisioni, permessi, disco pieno, inventari incompleti, cambi di ID e obblighi storici.

Quattro prove estese: stream di 4 GiB + 17 byte realmente letto e hashato senza allocazione su disco; inventario di 50.000 elementi e 250 GB logici simulati; 2.000 file fisici su inventario di 50.000 elementi con secondo passaggio senza copie aggiuntive; video oltre 4 GiB scritto, riletto e pubblicato su NTFS. I 250 GB sono simulati, non scritti sul disco.

## Riproduzione e pacchetto

`scripts/dev.ps1 test -Large` esegue la suite estesa. Il runner supporta `--filter=photo`, `--filter=multidevice`, `--filter=media`, `--filter=folder`. `tests/PackageStagingTests.ps1` copre i quattro casi packaging. `scripts/package.ps1` richiede tag/versione coerenti e checkout pulito, crea un nuovo stage, verifica manifest/checksum ed esegue interop, worker e sette smoke UI senza telefoni.

Il riscaricamento pubblico, l’integrità e l’avvio della release sono registrati separatamente in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md), dopo la pubblicazione. Le vecchie release restano immutate.

## Non verificato con hardware reale

Collegamento e trasferimento fisico su iPhone/Android, driver specifici, album effettivamente esposti come cartelle, esportazione reale degli album da Foto/Google Foto, contenuti solo cloud e archivio personale precedente. I controlli foto/video sono strutturali, non decodifica completa o prova di originalità. Limiti e formati nella [guida](PHONES-AND-ALBUMS.md). Nessuna promessa di compatibilità universale, assenza di bug o totale prestabilito.
