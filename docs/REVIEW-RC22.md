# rc.22 — memoria del catalogo iPhone

18 novembre 2026. Candidata Windows x64, nessun accesso a telefoni o file personali durante sviluppo e verifica.

## Evidenza e riproduzione

L'utente riferisce una prova rc.21 riuscita su tre video, seguita dal blocco della selezione Snapseed da 120 foto. La seconda schermata, dopo riavvio, riporta 3.371.982.864 byte richiesti, 2.641.379.328 di RAM libera e 3.821.715.456 di commit disponibile. Il blocco avviene prima di copiare elementi della selezione. Non dimostra danni alle foto né identifica quale processo occupa il resto della RAM.

La regressione nuova riproduce lo stesso errore con gli esatti numeri della schermata (0/1 prima della correzione). Il budget rc.21 è `2*(DB+WAL)+32+512 MiB`: l'immagine acquisita e la copia nativa SQLite coesistono. Anche il rilascio del grande array gestito dipendeva dal GC; non attribuiamo a ciò il consumo reale osservato senza una misura del processo.

## Correzione

`AppleCatalogBuffer` è un proprietario SafeHandle della RAM nativa, con 32 byte di padding azzerato. Capture lo riempie tramite scratch da 1 MiB; la seconda lettura confronta ancora tutti i byte. Il WAL viene validato integralmente e applicato all'immagine locale. Se cambia la dimensione serve un nuovo buffer, preceduto da controllo RAM/commit e azzerato in caso di errore.

`AppleMemorySqlite` prende un riferimento esplicito al medesimo proprietario fino alla chiusura della connessione. Non usa FREEONCLOSE o RESIZEABLE. Mantiene READONLY, query_only, trusted_schema=OFF, temp_store=MEMORY, limiti di query e integrity_check. I soli byte 18/19 dell'immagine privata sono adattati per la lettura senza WAL, come prima, e ripristinati dopo la chiusura. Nessun byte del telefono cambia. I buffer sono azzerati e liberati in finally; il riferimento protegge anche da una Dispose anticipata del proprietario. Una chiusura SQLite fallita non libera memoria ancora in uso.

Non rimane una copia grande nel Large Object Heap da attendere fra due catture. Il nuovo budget iniziale è `DB+WAL+32+512 MiB`: per la seconda schermata 1.954.426.904 byte. Non è una prenotazione e non garantisce successo su un PC sotto pressione. Il WAL ridimensionato può richiedere di più. Restano il limite 2047 MiB, scadenza catalogo dieci minuti, scadenza SQLite trenta secondi e gestione degli OutOfMemory.

## Verifica

Sei regressioni mirate: budget della schermata, confini RAM/commit, padding, sola lettura anche dopo query_only=OFF, GC durante la connessione, proprietario chiuso prima di SQLite, Dispose ripetuta, cancellazione e fallimento d'integrità con ripristino dei byte. Le prove precedenti continuano a verificare cambiamenti/troncamenti della sorgente, journal, WAL, album e incertezze.

Prima prova estesa della correzione: database sintetico valido 1.426.288.640 byte e WAL 7.337.752 byte, due catture complete consecutive, ciascuna con quattro aperture e 2.867.252.784 byte letti. Prima cattura 1.455 ms, allocazioni gestite 9.538.008 byte, picco finale 1.491.365.888 byte. Memoria privata prima/dopo due catture 18.456.576/37.015.552 byte: nessuna immagine da 1,43 GB trattenuta. Il precedente test rc.21 aveva picco circa 2,91 GB. Sono fixture locali, non velocità USB o tempi dell'iPhone. Esiti finali e log in TESTS.md e test-results.txt.

Nessuna riscrittura del trasporto o del motore di copia; autenticazione, hash, molteplicità, parziali, file precedenti e gestione iCloud invariati. Nessuna spesa, driver/servizio, modifica delle protezioni o file della prova. Revisione locale e test; revisori delegati non disponibili per quota, senza fallback a pagamento. Restano da confermare catalogo e album reali, disponibilità degli originali e prestazioni hardware. Non certifica 29 GB in un'ora.

## Fonti primarie

- [SQLite deserialize](https://www.sqlite.org/c3ref/deserialize.html): durata del buffer, margine per input malformati e normalizzazione dei byte WAL. Conservato padding di 32 byte, maggiore di quello consigliato.
- [SQLite flags](https://www.sqlite.org/c3ref/c_deserialize_freeonclose.html): READONLY e proprietà del buffer in assenza di FREEONCLOSE/RESIZEABLE.

Consultate il 17–18 novembre 2026. Le fonti descrivono SQLite, non certificano lo schema privato Foto di Apple.
