# rc.23 — ridurre le riletture del catalogo senza fidarsi dei soli metadati

18 novembre 2026. Candidata Windows x64. Nessun telefono, catalogo personale o file delle prove utilizzato durante lo sviluppo. Tutte le fixture sono sintetiche.

## Risposta alla richiesta

Non abbiamo trovato un modo verificato per evitare ogni lettura completa conservando i controlli attuali. La soluzione implementata evita una lettura completa ridondante quando l'intero contenuto appena acquisito coincide con un catalogo già verificato. Non è lettura selettiva del solo album e non elimina il requisito di memoria della cattura rc.22.

Data e dimensione possono rimanere uguali dopo una modifica. Il contatore nell'intestazione SQLite non rappresenta necessariamente ogni commit WAL. AFC dispone di seek, ma non fornisce nel percorso usato da Porto un token di snapshot atomico verificato. Leggere soltanto le pagine dell'album tramite un VFS remoto richiederebbe nuove garanzie di coerenza e integrità: non è stato implementato né dichiarato equivalente a integrity_check dell'intero database.

## Implementazione e condizioni

- `AppleCatalogCapture.Read`: prima cattura completa con EOF esatto, controllo di metadati prima/dopo e journal inattivo. Calcolo SHA-256 di tutto DB e WAL **prima** dell'applicazione locale del WAL.
- `AppleCatalogCache`: una sola osservazione in RAM con identità del dispositivo, metadati, impronte e catalogo interpretato. Nessuna immagine grezza DB/WAL trattenuta. Nessuna persistenza, credenziale o nuovo accesso al telefono.
- Primo uso o qualsiasi differenza: seconda lettura completa e confronto byte per byte; validazione WAL, integrity_check, decodifica e controlli dello schema precedenti. Solo dopo il successo l'osservazione diventa riutilizzabile.
- Corrispondenza completa: l'elenco già interpretato viene riutilizzato senza seconda lettura e decodifica. Si usa l'assunzione crittografica SHA-256, già adottata per l'integrità delle copie; nessun campionamento di byte. L'identità del dispositivo e la presenza/assenza del WAL fanno parte della chiave.
- Errore o cancellazione durante la cattura: cache svuotata. Cambio dispositivo e chiusura finestra: svuotamento. La UI impedisce operazioni concorrenti. Il riavvio dell'app richiede nuovamente due letture iniziali.
- `AppleGallerySource.Inventory` continua a selezionare l'album corrente e richiedere i metadati dei media pertinenti. I controlli finali del motore e la ripresa confrontano ancora la selezione. Originali locali mancanti, visibilità incerta e associazioni irrisolte rimangono segnalati/bloccati.
- I buffer nativi e il WAL sono azzerati/liberati anche sul percorso rapido. La singola immagine e il budget di rc.22 rimangono; nessuna garanzia di memoria illimitata o prenotata.

La prima osservazione richiede sempre due passaggi. Una successiva osservazione invariata ne richiede uno. Una osservazione cambiata ne richiede due, non tre. Per scelta album, inventario iniziale e finale invariati: **6 -> 4 passaggi**, circa 33% in meno di payload del catalogo. Senza scelta album: **4 -> 3**. Dopo una scelta già verificata: **4 -> 2** nei soli inventari iniziale/finale. Non sono percentuali della durata totale dell'importazione. Se il catalogo continua a cambiare, il risparmio può essere nullo e si aggiunge il piccolo costo dell'hash.

Resta una osservazione del catalogo, non uno snapshot atomico garantito da iOS. La doppia lettura precedente non forniva tale certificazione. Nessuna garanzia universale su schema privato Foto, libreria cloud o album supportati.

## Riproduzione e test

Regressione iniziale contro il comportamento precedente: fallimento atteso, due letture invece di una dopo il primo catalogo verificato. Quattordici casi dedicati verificano: riutilizzo, catalogo cambiato con data/dimensione/primi 100 byte uguali, cambio dispositivo, svuotamento, troncamento, crescita, corruzione, cancellazione, metadati mutati durante la lettura, journal attivo, WAL non valido, presenza/metadati WAL modificati, sola modifica valida del WAL con membri album diversi, disponibilità e dimensioni degli originali aggiornate, quattro passaggi per l'intero flusso UI simulato.

Durante la preparazione dei nuovi test sono stati corretti due errori delle fixture: il flag EOF rimasto dal riscaldamento e l'ipotesi errata che l'album sintetico predefinito contenesse il video. I log iniziali non sono conteggiati come successi. Nessun controllo produttivo è stato rimosso per far passare i test. Esiti finali, prova grande e log in TESTS.md e test-results.txt.

Esito finale: **579/579** casi ordinari e **14/14** dedicati già inclusi. Catalogo sintetico 1.426.288.640 byte + WAL 7.337.752 byte: prima cattura 2.867.252.784 byte/quattro aperture, seconda 1.433.626.392 byte/due aperture con un riutilizzo. Hash sorgenti invariati e album corretti. Prima cattura locale 4.068 ms; picco finale 1.488.330.752 byte, memoria privata prima/dopo 19.062.784/37.019.648 byte. La cache non trattiene un'immagine da 1,43 GB. Questa fixture da disco non misura la velocità USB. Packaging sintetico 4/4. Nessuna modifica produttiva dopo questi controlli.

## Benchmark controllato

Fixture SQLite valida 16.928.768 byte, un video sintetico riconosciuto, tre osservazioni consecutive. Windows x64, .NET 10.0.14, build Release; nessun USB reale. Letture limitate a 256 KiB nel fake con **Sleep(2 ms) artificiale per richiesta**: la sospensione reale dipende dal timer Windows e può durare più di due millisecondi. Ordine delle varianti alternato; tre ripetizioni, sola variabile cache attiva/disattiva.

Sistema Windows build 26200, Intel64 Family 6 Model 154 Stepping 4, 12 processori logici. RAM installata non rilevata: CIM non disponibile nel contesto ristretto. La misura del picco del processo è raccolta nella prova grande separata. Nessuna attribuzione di queste condizioni al computer della cliente.

| Ripetizione | Senza riutilizzo | Con riutilizzo |
| --- | ---: | ---: |
| 1 | 5.610,38 ms | 3.702,83 ms |
| 2 | 5.643,19 ms | 3.670,05 ms |
| 3 | 5.624,78 ms | 3.838,88 ms |

Mediana 5.624,78 -> 3.702,83 ms, riduzione 34,2%; intervalli senza sovrapposizione. Sei aperture/101.572.608 byte -> quattro aperture/67.715.072 byte. La riduzione deterministica del traffico è 33,3%. Il benchmark include hashing e verifica della selezione riconosciuta; non copia media e non rappresenta 800 video o il catalogo reale. Non permette di stimare 29 GB in un'ora. L'effetto reale dipenderà da quanto tempo il catalogo occupa sul totale e da quante volte cambia.

## Telemetria e collaudo necessario

`performance-*.json`, sezione AppleTransport: CatalogValidatedReuses, CatalogFullValidations, CatalogFingerprintMilliseconds, insieme a CatalogBytes e contatori AFC già presenti. Sono aggregati locali senza nuovi nomi, percorsi o impronte personali. Il file descrive il singolo passaggio d'importazione: la scelta album precedente è esclusa, come esplicitato in TimingScope. Nessun invio automatico.

Sul telefono occorre confrontare lo stesso album e la stessa destinazione, annotando dimensioni, stato iCloud, durata da scelta album a risultato finale, versioni iOS/Windows, condizioni USB e catalogo cambiato o stabile. Verificare esito finale e ripresa senza nuove copie dei file già verificati. Non promettiamo un'ora né che la fase catalogo diventi istantanea. Autenticazione, trasporto media, verifiche del file salvato e protezione delle copie precedenti non cambiano.

Revisione locale del diff e regressioni; non presentata come revisione indipendente. Revisori delegati non disponibili entro la quota, nessun fallback a pagamento. Nessuna spesa, driver/servizio o riduzione delle protezioni. Le versioni precedenti e tutti i file delle prove sono conservati.

## Fonti primarie

- [SQLite formato, contatore delle modifiche](https://www.sqlite.org/fileformat2.html): un commit WAL non deve necessariamente aggiornare il contatore nel file DB.
- [SQLite integrity_check](https://www.sqlite.org/pragma.html#pragma_integrity_check): il controllo parziale non copre tutti i vincoli di allocazione e duplicazione di pagine fra tabelle.
- [libimobiledevice AFC](https://raw.githubusercontent.com/libimobiledevice/libimobiledevice/master/src/afc.c): supporto a file seek; la sola possibilità di cercare un offset non dimostra coerenza di uno snapshot Foto.

Consultate il 18 novembre 2026. La conclusione sul riuso sicuro deriva dal codice Porto e dai test, non da una garanzia Apple di accesso al catalogo privato.
