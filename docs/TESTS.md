# Prove della candidata 0.1.0-rc.13

Windows 11 x64, SDK .NET 10.0.403 e runtime 10.0.14. Tre revisori indipendenti e coordinatore; test esclusivamente sintetici, senza telefoni, credenziali o archivi personali. La precedente prova Oppo dell'utente non è una prova del trasporto Apple.

## Sequenza verificabile

- Baseline compilata rc.12: 37/37 verifiche Apple.
- Difetti riprodotti prima delle correzioni: STATUS AFC malformato lasciava la sessione utilizzabile; selezione ridotta tra tentativi dava falso successo; journal PERSIST realmente prodotto da SQLite veniva rifiutato; stati di visibilità non considerati consentivano copie non giustificate. Il raccordo per gli elementi incerti ha anch'esso fallito prima della modifica al motore.
- Verifiche mirate dopo le correzioni: 49/49 Apple (nove nuovi casi catalogo), 12/12 galleria, cinque AFC e sei TLS. Revisione reciproca Core/catalogo/protocollo senza nuovi difetti confermati nel perimetro.
- Suite integrata con prove estese: 479/479 passati (475 ordinari e quattro estesi). Stream di 4 GiB + 17 byte letto e verificato: 2,61 s; inventario di 50.000 elementi / 250 GB logici senza payload: 5,98 s; 2.000 copie fisiche tra 50.000 elementi e secondo passaggio senza duplicati: 393,96 s; file NTFS di 4 GiB + 4.096 byte scritto, riletto e pubblicato: 40,54 s. Tempi del sistema di prova, non previsioni per USB. Log in test-results.txt.
- Dopo il solo chiarimento testuale che i conteggi GalleryCoverage descrivono l'inventario iniziale: build finale senza errori/avvisi, 16/16 casi filtro report e smoke UI iPhone/USB. Nessun cambiamento alla logica dopo la suite integrata.
- Confezionamento: quattro prove della sezione effettiva dello script (compressione interrotta, archivio invalido, file esistente preservato, pubblicazione riuscita).

I risultati del pacchetto autonomo e del suo download pubblico sono aggiunti a RELEASE-VERIFICATION.md dopo la pubblicazione, senza sostituire lo ZIP immutabile.

## Copertura aggiunta

STATUS AFC di lunghezze errate su stat/open/read, risorse mancanti e accesso negato con risposta valida; certificati sintetici firmati dalla radice, pin errato, autenticazione reciproca, firma handshake debole e cancellazione; SQLite WAL nativo con coda non conclusa, journal PERSIST azzerato e journal attivo/cambiato; visibilità zero/nonzero/null/nuova/assente, membership e filtri, riconnessione e controllo finale; selezioni ridotte/vuote tra tentativi, ritorno al terzo tentativo, nuovo avvio esplicito e storico conservato; elemento incerto mai aperto e fratelli sani copiati.

Restano attive le regressioni precedenti su byte discordanti, file troncati, duplicati distinti, copie corrotte/mancanti, processi interrotti, spazio/permessi, filesystem ostile e migrazioni. Nessun test storico rimosso per ottenere un risultato verde.

## Riproduzione e limiti

`scripts/dev.ps1 test -Large` esegue suite ordinaria più quattro prove estese. Filtri dell'eseguibile: `--filter=apple`, `--filter=gallery`, `--filter=report`; `tests/PackageStagingTests.ps1` verifica il confezionamento. Il package esegue interop COM e worker senza enumerazione hardware, più nove stati UI con dati sintetici.

I test non dimostrano compatibilità con uno specifico iPhone. Restano da confermare servizio Apple/abbinamento, TLS e AFC reali, accessibilità e schema Photos.sqlite, album e originali locali, confronto con la selezione Foto, trasferimento e ripresa. Il limite catalogo+WAL di 512 MiB riguarda i dati acquisiti, non il picco di memoria del processo. Una lettura ripetuta non prova atomicità. TLS o schemi non supportati restano rifiutati; originali solo cloud non vengono scaricati.

La ripresa conserva osservazioni complete: cambiamenti di metadati/disponibilità possono richiedere un nuovo avvio esplicito. Non si promette assenza di bug, velocità universale, un totale fisso di video o copertura dell'intera libreria.

[Revisione e scenari](REVIEW-RC13.md) · [Fonti](SOURCES.md) · [Verifica pubblicazione](https://github.com/9gabri/porto-iphone-usb/blob/main/docs/RELEASE-VERIFICATION.md).
