# Prove della candidata 0.1.0-rc.12

Windows 11 x64, SDK .NET 10.0.403, runtime 10.0.14. I test automatici usano dati sintetici in cartelle isolate. Separatamente, il controllo strutturale autorizzato delle copie temporanee reali è stato svolto soltanto in lettura: nessun media visualizzato, trasferito altrove o modificato.

## Evidenza della correzione

- Vecchio lettore rc.11: 29/29 file della prova risultano ambigui. Nuovo lettore: 29/29 riconosciuti come JPEG primari validi, con dati aggiuntivi esplicitamente non interpretati. 28 code di 190 byte e una di 1.494 byte. Tutti gli hash prima/dopo coincidono con quelli registrati dal report originale.
- Baseline sintetica: tre integrazioni fallite (0/3) e 3/6 casi JPEG passati prima della correzione. Dopo la correzione: 37/37 test filtro photo, comprendenti i nuovi casi del parser e il recupero.
- Archivio sintetico: 48 copie precedenti + 29 foto rifiutate; recupero di tutte le 29 con 48 riutilizzi, poi 77 riutilizzi senza nuove copie. Vecchi .part, copie precedenti e file estranei conservati byte per byte.
- Coda modificata: non viene riutilizzato un file precedente diverso. Coda contenente un video: rimane una foto singola, esclusa da Solo video. Primarie malformate, limiti dimensionali e cancellazione restano controllati.

Regressione ordinaria finale: **457/457 passati** dopo tutte le modifiche al codice, build con zero avvisi e zero errori. Quattro prove del confezionamento superate. Risultati in test-results.txt; i controlli del pacchetto sono registrati in RELEASE-VERIFICATION.md. La verifica del programma scaricato avviene dopo la pubblicazione; il resoconto viene aggiunto senza sostituire il pacchetto immutabile.

## Ambito e riproduzione

`scripts/dev.ps1 test` esegue la suite ordinaria. Filtri: `--filter=photo`, `--filter=rc12`. `scripts/dev.ps1 test -Large` aggiunge le quattro prove estese già eseguite per rc.11 (50.000 elementi, 2.000 copie e oltre 4 GiB); queste ultime non vengono presentate come nuove esecuzioni rc.12, perché il motore dei trasferimenti non cambia. `tests/PackageStagingTests.ps1` verifica il confezionamento.

Il package richiede tag/versione coerenti e checkout pulito, nuovo staging, manifest e checksum; esegue interop COM, worker e nove stati UI senza cercare dispositivi. Nessuna nuova dipendenza o installazione, nessuna API a consumo, nessuna modifica alle protezioni Windows.

La correzione è del formato, non di una marca. La struttura JPEG primaria viene verificata, mentre i dati dopo EOI vengono conservati senza interpretazione o attestazione del loro formato. Pixel, HDR/Motion e originalità non sono certificati. Nessun nuovo collaudo fisico del collegamento iPhone; album privati/cloud e compatibilità universale restano limiti. Il completamento della prova reale richiede una nuova importazione dell'utente con il telefono collegato e la stessa destinazione.

[Revisione](REVIEW-RC12.md) · [Fonti](SOURCES.md) · [Verifica pubblicazione](https://github.com/9gabri/porto-iphone-usb/blob/main/docs/RELEASE-VERIFICATION.md).
