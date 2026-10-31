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
