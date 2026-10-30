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
