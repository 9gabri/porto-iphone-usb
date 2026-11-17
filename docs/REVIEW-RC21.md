# Porto 0.1.0-rc.21 — catalogo Foto oltre 1 GiB

17 novembre 2026. Correzione del blocco mostrato nelle schermate rc.20. Nessun telefono, record di abbinamento, catalogo personale o file delle prove letto o modificato. Solo dati sintetici in nuove directory. Nessuna spesa, nuova dipendenza, installazione di servizi/driver o riduzione delle protezioni.

## Problema confermato e riproduzione

La schermata mostra `[APPLE-CATALOG-DB-LIMIT]`, database 1.416.859.648 byte, WAL 7.337.752 byte, limite 1.073.741.824 byte. È il rifiuto preventivo in `AppleCatalogCapture.ValidateMetadata`, non una prova di cavo difettoso, memoria fisica esaurita o catalogo corrotto. Il database da solo supera il limite della rc.20. L'attesa e l'opzione USB compatibile non cambiano quel controllo. Il trasferimento dei media non è iniziato in questa operazione.

Due regressioni scritte prima della modifica, una con le dimensioni esatte mostrate e una senza WAL, falliscono entrambe contro il codice rc.20: 0/2, con lo stesso DB-LIMIT. Log locale `artifacts/t21/red.txt`. Dopo la modifica passano. Non è stato aperto il database reale per riprodurre il problema.

## Modifica e confini

- `AppleSqliteImage.MaximumBytes` diventa **2.146.435.072 byte, 2047 MiB**: 2 GiB meno 1 MiB. Vale per DB, WAL, somma acquisita e immagine risultante dal WAL. Il margine sotto il limite degli interi/array copre i 32 byte aggiuntivi della deserializzazione nativa e l'avanzamento massimo di un frame WAL. Non è supporto illimitato.
- `AppleCatalogMemory.CaptureAllocation` calcola in interi a 64 bit il budget per acquisizione più copia di verifica, con i 32 byte di padding. `Capture` lo controlla **prima di leggere il database**. Con i numeri della schermata: 2.848.394.832 byte di budget + 536.870.912 di riserva = **3.385.265.744 byte** richiesti sia come RAM libera sia come capacità di allocazione. Non equivale alla RAM totale installata.
- Se il WAL produce un'immagine di dimensione diversa, si verifica nuovamente lo spazio per la nuova immagine e la successiva copia nativa. La copia nativa mantiene inoltre il proprio controllo immediato. Questi controlli misurano disponibilità volatile, non prenotano RAM: gli errori di allocazione continuano a essere intercettati. Un PC con memoria insufficiente viene fermato con diagnostica esplicita.
- Messaggi e fixture dell'interfaccia mostrano il nuovo limite reale. Non si nasconde una lettura parziale, non si sostituisce un elenco USB generico agli album e non si riduce il controllo d'integrità per accettare il catalogo.

Confermati invariati: lettura completa del catalogo e confronto completo della seconda lettura, controlli prima/dopo su metadati, WAL e journal, checksum WAL, integrità SQLite readonly, padding nativo, cancellazione e azzeramento dei buffer. Scadenza complessiva di dieci minuti per il catalogo e limite SQLite di trenta secondi conservati. Nessun database del telefono salvato su disco. Il file SQLite su disco del test è generato artificialmente e appartiene solo alla fixture.

Nessuna modifica a autenticazione TLS, richieste AFC da 512 KiB/compatibilità 64 KiB, copie dei media, hash, ripresa, molteplicità, cataloghi di destinazione o protezione dei file esistenti. Le ottimizzazioni rc.19/20 restano, ma questa correzione non promette 29 GB entro un'ora.

## Verifiche mirate

- Quattro nuovi casi ordinari: i due input dimensionali della riproduzione, budget sufficiente esatto e rifiuto quando manca un byte di RAM/commit, limiti di array/padding/frame e valori negativi/nulli/eccessivi. 4/4 passati. I casi diagnostici esistenti continuano a verificare limite esatto, oltre limite, somma, dimensioni ostili e tipi non regolari senza aprire file.
- Test completo opt-in `--large-apple --filter="rc21 large catalog full"`: SQLite valido generato in una nuova directory, **1.426.288.640 byte**, maggiore del database segnalato; WAL **7.337.752 byte**, uguale alla dimensione segnalata. La falsa sorgente legge il file sintetico a blocchi, senza tenere una copia sorgente aggiuntiva da 1,42 GB in RAM.
- Prima esecuzione: acquisizione, confronto di tutti i byte, applicazione WAL, integrità SQLite e decodifica album passati. Quattro aperture (DB/WAL due volte), **2.867.252.784 byte** letti; cattura/decodifica 3.767 ms, allocazioni gestite 1.434.785.400 byte, picco processo 2.907.529.216 byte. Hash del file sorgente e del WAL invariati. Conteggi attesi: 2 foto visibili, 1 video visibile, 1 nascosto, 1 eliminato, album con 2 membri.
- Ripetizione estesa nello stesso processo: prima cattura 3.276 ms, 1.434.786.664 byte gestiti allocati, picco 2.907.385.856 byte; **seconda cattura completa consecutiva verificata**, con quattro aperture e lo stesso numero di byte. Copre la rilettura dopo la scelta album; non è stato necessario introdurre una raccolta forzata globale della memoria.

Sono misure locali su disco/cache/RAM, **non tempi USB**. Il test non dimostra che lo schema iOS concreto sia compatibile, che la libreria sia immutabile o che tutti gli originali siano sul telefono. Il contatore dei video non determina la dimensione del catalogo e la dimensione del catalogo non rappresenta i GB dei video da copiare.

Esiti della suite finale e del confezionamento in TESTS.md e test-results.txt; download e avvio del pacchetto pubblico registrati dopo il tag in RELEASE-VERIFICATION.md. La revisione delegata resta indisponibile per quota esaurita: nessun tentativo di aggiramento o fallback a pagamento. Revisione locale e test non vengono presentati come approvazione indipendente.

## Prossima prova reale

Chiudere la versione precedente, estrarre la nuova in una cartella distinta, aprire Porto.exe e confermare rc.21. Selezionare `iPhone · libreria Foto via USB`, cercare il dispositivo e riprovare `Scegli album`, mantenendo l'iPhone collegato e sbloccato. Per questo DB-LIMIT non serve attivare USB compatibile. La nuova candidata elimina il limite da 1 GiB, ma richiede memoria sufficiente e può ancora segnalare un altro problema reale: conservare sempre il testo completo.

Se l'album diventa selezionabile, scegliere tipo di contenuto e destinazione consapevolmente; per riprendere un'importazione precedente dello stesso album usare il suo archivio completo con .porto e report. File di prove precedenti, anche di un altro telefono, non devono essere cancellati. Nessuna certificazione o garanzia universale: compatibilità, lista album, conteggi, copia e ripresa vanno confermati sul dispositivo.
