# Attività e criteri

- [x] Modello e copia: copia identica, hash rilettura, >4 GB, niente overwrite; compilazione e test.
- [x] Catalogo e recupero: commit SQLite, crash prima/dopo rename, file rimossi/corrotti; test fault injection.
- [x] Riconciliazione: ID variabili e duplicati distinti, collisioni e path ostili; test.
- [x] USB: GUID/vtable contro header ufficiali, accesso GENERIC_READ e STGM_READ, isolamento; smoke COM senza enumerazione.
- [x] UI: telefono/cartella/spazio/conteggi/progresso/velocità/ETA/pausa/riprendi; avvio e ispezione senza USB.
- [x] Report: categorie complete, inventario incompleto e libreria non verificabile sempre distinguibili; test.
- [x] Scala: 50.000 elementi, oltre 4 GB letti, volume virtuale 250 GB; tempi e RAM documentati.
- [x] Revisione: sicurezza, correttezza, licenze, dipendenze e mutazione di un controllo d'integrità.
- [x] Pacchetto: self-contained x64, hash, istruzioni e limiti, avvio pacchetto.
- [x] Pubblicazione: repo/release/Pages e download con hash identico; vedi docs/RELEASE-VERIFICATION.md.

## rc.2 — revisione richiesta dall'utente
- [x] Audit e baseline: riproduzioni sintetiche dei difetti, piano e prompt salvati.
- [x] Discovery: elenco variabile, heartbeat metadati, diagnostica, ABI e test.
- [x] Copia: errori isolati non bloccano i successivi; pendenti e zero falsi successi.
- [x] Recupero: debiti storici visibili, verifica finale inventario e regressioni crash.
- [x] Sessione: retry automatici limitati, cancellazione immediata, nessun cambio telefono.
- [x] UI: diagnostica, DPI e avanzamento, cartella ricordata, versione visibile.
- [x] Verifica: suite estesa, stress, mutazione, revisione e smoke pacchetto senza USB.
- [x] Release: codice/tag coerenti, ZIP, checksum, sito e download verificato.

## rc.3 — revisione avversaria conclusiva
- [x] Revisori con contesto nuovo e riproduzioni sulla baseline.
- [x] Molteplicità, letture pendenti, conflitti e riparazioni.
- [x] Errori per file, sessioni USB, pipe e progressi.
- [x] Hard link, cataloghi/journal, report e gerarchia media.
- [x] Clip codificate generate localmente, mutazione isolata e build UI.
- [x] Suite finale ordinaria e scala, risultati consolidati: 124/124.
- [x] Pacchetto con staging nuovo, ZIP e codice coerenti.
- [x] Pubblicazione, download indipendente e verifica finale.

## rc.4 — ultimo ciclo richiesto dall'utente
- [x] Baseline 120/120, tre revisori indipendenti e massimo tre cicli per area.
- [x] Copie storiche, Unknown persistenti e priorità globale dopo blocchi USB.
- [x] Errori nativi di sessione, nuovo inventario e video omonimi distinti.
- [x] Cataloghi, preflight, percorsi, preferenze, snapshot e compatibilità legacy.
- [x] 188 test ordinari e quattro estesi; mutazione isolata e tre stati UI.
- [x] Confezionamento con pubblicazione atomica dello ZIP, quattro prove sintetiche.
- [x] Nuovo pacchetto, tag, pubblicazione, download pubblico, checksum e cinque smoke verificati.

## rc.5 — correzioni dopo il collaudo fisico
- [x] Diagnosi dai report autorizzati, senza lettura dei media o modifica della prova.
- [x] EOF nativo, dimensione per risorsa, doppia lettura obbligatoria sulle discrepanze e riuso.
- [x] Accessori AAE, contatori distinti e risultato incompleto senza barra piena.
- [x] Revisione indipendente, regressioni sulla baseline e mutazione negativa isolata.
- [x] Suite finale unica 233/233, quattro prove packaging, build senza avvisi/errori.
- [x] Pacchetto nuovo, pubblicazione, riscaricamento e sei smoke senza hardware.
- [x] Versioni precedenti conservate e metadati della destinazione reale invariati.

Prossimo riscontro esterno: collaudo rc.5 con l'utente, in una nuova cartella
vuota, per confermare la stabilità USB e confrontare i video autonomi attesi.

## rc.6 — recupero delle letture interrotte
- [x] Analisi dei report allegati senza accesso a telefono, PC della cliente o media.
- [x] Cleanup WPD e proprietà dello stream, wrapper obsoleti e cause strutturate IPC.
- [x] Priorità dei file effettivamente falliti; successivo non aperto ancora pendente.
- [x] Motivi delle ambiguità espliciti e contatori dell'ultimo passaggio chiariti.
- [x] Revisione indipendente, baseline negative e mutazione isolata rilevata.
- [x] Suite finale unica 253/253, quattro prove packaging e quattro stati UI.
- [x] Nuovo ZIP, pubblicazione, riscaricamento, checksum e sei smoke verificati.

Riscontro esterno ancora necessario: recupero fisico con rc.6 nella destinazione
rc.5 conservata e riconciliazione dei conteggi Foto. Nessun completo recupero
dei video reali dichiarato sulla sola base delle prove sintetiche.

## rc.8 — copertura e recupero senza esclusioni per indizi
- [x] Diagnosi dagli allegati autorizzati, senza lettura dei media.
- [x] Due revisori indipendenti: parser/copertura e USB/continuità, controllo reciproco e storage.
- [x] Tutte le risorse candidate tentate; copie aggiuntive separate e temporanei precedenti preservati.
- [x] Continuazioni limitate, priorità ai non raggiunti, report storico distinto senza cancellare debiti.
- [x] Mutazione negativa isolata rilevata e quattro test packaging.
- [x] Suite 322/322 e 48 ricontrolli pertinenti sul codice finale; build e interfaccia verificate.
- [x] Pacchetto nuovo e pubblicazione prerelease rc.8; riscaricamento pubblico, checksum, manifest e sei smoke verificati.
- [x] Pagina pubblica e immagine corrispondenti al tag; versioni precedenti conservate.

Il totale Foto dichiarato dall’utente e il recupero reale richiedono ancora una
prova con il telefono. Nessun totale artificiale basato sui componenti Live Photo.


## rc.9 — cartella esportata e confronto archivio
- [x] Sorgente locale in sola lettura e confronto anche senza catalogo precedente.
- [x] Riutilizzo SHA-256, molteplicità, copie extra preservate e report distinto.
- [x] Revisione indipendente in tre cicli circoscritti e regressioni sintetiche.
- [x] Verifiche finali (348 casi distinti), pacchetto nuovo, pubblicazione, checksum pubblico, sette smoke e pagina verificati.
- [ ] Prova guidata con esportazione reale da iPhone (richiede intervento dell’utente).


## rc.10 — foto, dispositivi e cartelle
- [x] Specifica: Solo video / Solo foto / Foto e video; copia in massa telefono→PC mantenuta.
- [x] Discovery WPD generica, identificatori isolati per dispositivo, selezione fisica fail-closed.
- [x] Importazione foto e cataloghi compatibili; conservazione copie precedenti.
- [x] UI esplicita, album virtuali distinti da cartelle fisiche, nessuna ricerca hardware negli smoke.
- [x] Revisione indipendente, suite 387/387, sette stati UI e quattro prove packaging.
- [x] Pacchetto autonomo, release pubblica, riscaricamento SHA-256/CRC, nove smoke sul download e pagina pubblica verificati.
- [ ] Prova fisica con iPhone/Android e album reali (richiede dispositivo dell’utente).

## Gallerie e album — richiesta del 16 novembre 2026
- [x] Requisiti e limiti scritti in docs/GALLERY-IMPORT.md; nessuna esportazione preliminare nel flusso richiesto.
- [x] Navigazione USB per livello e inventario del solo ramo selezionato, anche dopo riconnessione.
- [x] Accesso in sola lettura al catalogo Foto iPhone, asset e appartenenze album; originali mancanti visibili.
- [x] Interfaccia coerente con capacità e limiti reali dei dispositivi.
- [x] Verifiche sintetiche, revisione indipendente e build senza avvisi/errori.
- [x] Pacchetto rc.11 nuovo, pubblicazione, riscaricamento pubblico, checksum/CRC e undici smoke verificati.
- [ ] Collaudo hardware autorizzato su iPhone e Android; compatibilità universale non dichiarabile.

## rc.12 — JPEG e completamento della prova
- [x] Causa riprodotta con metadati strutturali e nessuna modifica alla prova personale.
- [x] Correzione comune del lettore JPEG, coda preservata e limiti espliciti anche nel riuso.
- [x] Regressioni sintetiche prima/dopo e confronto in sola lettura di tutte le 29 copie interessate.
- [x] Revisione indipendente del contratto e del diff finale.
- [x] Regressione finale 457/457 e build senza avvisi/errori.
- [x] Nuovo pacchetto pubblicato e riscaricato senza autenticazione: checksum/CRC, versione/contenuto, undici smoke e pagina pubblica verificati; rc.1–rc.11 invariati.
- [ ] Importazione fisica rc.12 da completare con l'utente; nessuna compatibilità universale dichiarata.

## rc.13 — revisione avversaria del percorso iPhone
- [x] Stato effettivo, prerequisiti e separazione fra percorso WPD e catalogo Apple ricontrollati.
- [x] Baseline del binario rc.12: 37/37 test Apple; nessun telefono o archivio personale consultato.
- [x] Tre revisioni indipendenti: protocollo Apple, catalogo/selezione, archivio/ripresa.
- [x] Riproduzioni prima delle correzioni, modifiche circoscritte e riesame indipendente.
- [x] Verifiche integrate 479/479, incluse quattro prove estese e regressioni Android; dopo i chiarimenti testuali, 16/16 casi report e due smoke UI.
- [x] Nuovo pacchetto rc.13 pubblicato e riscaricato: checksum/CRC, versione/contenuto, undici smoke e pagina pubblica verificati; dodici archivi precedenti invariati.
- [ ] Collaudo iPhone reale: collegamento, catalogo, video/Live Photo, copie e ripresa.

## rc.14 — blocco TLS osservato su iPhone
- [x] Analisi del messaggio reale e fonti primarie; nessuna lettura del telefono.
- [x] Quattro rifiuti di firme forti riprodotti; ipotesi catena SHA-1 esclusa.
- [x] Negoziazione e diagnostica corrette; 15/15 nuovi casi, revisione indipendente.
- [x] Suite finale 490/490, pacchetto nuovo, pubblicazione e riscaricamento: checksum/CRC, versione/contenuto, undici smoke e pagina pubblica verificati; tredici archivi precedenti invariati.
- [ ] Collaudo iPhone: causa reale non ancora attribuita con certezza o dichiarata risolta.

## rc.15 — APPLE-TLS-LOCAL-42
- [x] Rifiuto dei certificati con issuer vuoto riprodotto contro il binario pubblico rc.14.
- [x] Adattatore limitato ai byte esatti già autorizzati, senza cambiare policy globali o certificati.
- [x] Revisione indipendente della sicurezza della correzione.
- [x] Suite finale 503/503, pacchetto nuovo pubblicato e riscaricato: checksum/CRC, versione/contenuto, undici smoke e pagina pubblica verificati; quattordici archivi precedenti invariati.
- [ ] Conferma sull'iPhone reale: autenticazione, lettura album e copia di prova.

## rc.16 — diagnostica del catalogo
- [x] Nuova prova reale rc.15: superata autenticazione, blocco sui metadati catalogo.
- [x] Messaggi distinti, dimensioni numeriche e tipo non regolare, senza aumentare limiti o seguire link.
- [x] 11 nuovi casi, verifica finale pertinente 88/88 Apple e 12/12 galleria; build e schermata diagnostica verificate.
- [x] Pacchetto diagnostico pubblico riscaricato e verificato: hash, CRC, contenuto/versione, dodici smoke, pagina e anteprima; quindici archivi precedenti invariati.
- [x] Ottenuti codice/dimensioni dalla prova reale: DB-LIMIT. Correzione del limite sviluppata in rc.17; prova album reale ancora pendente.

## rc.17 — catalogo oltre 512 MiB
- [x] Diagnosi reale: DB 563425280 byte, WAL 4227152 byte; limite riprodotto contro rc.16.
- [x] Confronto completo a blocchi di 1 MiB, limite 1 GiB, controlli memoria e pulizia WAL.
- [x] Revisione indipendente e prova completa con catalogo sintetico di 566362112 byte più WAL 4227152 byte.
- [x] Suite finale 528/528, prova grande Apple 3/3, pacchetto pubblico riscaricato: hash/CRC/contenuto/versione, dodici smoke e pagina verificati; sedici archivi precedenti invariati.
- [ ] Conferma su iPhone: lista album, schema iOS, copia piccola e ripresa. Nessuna compatibilità definitiva dichiarata.

## rc.18 — associazioni album
- [x] Riprodotto il blocco globale su due cataloghi sintetici contro rc.17.
- [x] Album integri isolati; album incerti visibili/bloccati, galleria completa ancora bloccata; conteggi preservati.
- [x] Revisione indipendente, 109 test Apple, 12 importazione galleria e 9 dedicati (7 sovrapposti); UI verificata.
- [x] Pacchetto pubblico riscaricato: hash/CRC, versione/contenuto, tredici smoke e pagina verificati; diciassette archivi precedenti invariati.
- [ ] Confermare se l'album desiderato è selezionabile sull'iPhone reale; quelli con associazioni irrisolte restano da risolvere.
