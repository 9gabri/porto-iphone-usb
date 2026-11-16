# Porto

App locale in italiano per copiare e verificare **foto e video dal telefono al PC**, senza conversioni. **0.1.0-rc.10**, candidata Windows 11 x64 con runtime incluso.

[Scarica Windows](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.10/Porto-0.1.0-rc.10-win-x64.zip) · [Checksum](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.10/SHA256SUMS.txt) · [Guida semplice](docs/PHONES-AND-ALBUMS.md)

## Scegli sorgente e contenuti

- **Telefono / dispositivo USB:** iPhone e altri dispositivi portatili esposti da Windows tramite WPD, normalmente MTP/PTP. Cerca il dispositivo esplicitamente, poi scegli tutte le cartelle accessibili o una cartella specifica con le sue sottocartelle. Su Android seleziona Trasferimento file; su iPhone autorizza il computer e usa Mantieni originali.
- **Cartella / album esportato:** scegli una cartella sul PC contenente gli originali esportati dall’album desiderato. La verifica riguarda questa cartella, non l’intera libreria del telefono.
- In entrambi i casi scegli **Solo video**, **Solo foto** oppure **Foto e video**. Scegli l’archivio PC e premi **Avvia importazione**. Non serve selezionare ogni file. Pausa e Riprendi ripetono i confronti preservando il lavoro.

Gli **album delle app Foto o Google Foto non coincidono necessariamente con cartelle USB**. Porto non inventa la loro struttura: per album non esposti serve prima l’esportazione. Nessuna compatibilità universale con tutti i telefoni dichiarata. [Passi e limiti](docs/PHONES-AND-ALBUMS.md).

## Conservazione e confronto

Chiudi la vecchia app ed estrai tutto il nuovo ZIP in una cartella separata. Conserva l’intero archivio precedente, inclusi .porto e report. Usa la sua radice come Archivio PC per confrontare le copie già presenti.

Le copie vengono confrontate per contenuto completo e SHA-256; quelle identiche verificate sono riutilizzate senza essere spostate. In modalità cartella vengono cercati anche file supportati non registrati nel catalogo. Due elementi selezionati distinti con byte uguali mantengono due copie. File danneggiati restano conservati; i recuperi hanno un nuovo nome. Versioni con byte diversi restano distinte.

Nuove foto in **Foto Porto**, video in **Video Porto**. Gli indizi Apple di Live Photo/modifiche sono conservati in **Risorse video aggiuntive**, senza identificarli con certezza. Le copie precedenti non riutilizzate non sono automaticamente Live Photo. Cataloghi e report registrano avanzamento ed errori; temporanei associabili riprendono solo dopo confronto completo del prefisso. Nessun file esistente cancellato o sovrascritto.

## Copertura e requisiti

- Video MOV/MP4/M4V; foto JPEG, PNG statico e HEIF/HEIC HEVC nei profili riconosciuti. Controlli strutturali, non decodifica completa. Formati foto/video non supportati e letture incerte restano nel report come non verificabili.
- File nel cloud non esposti localmente possono mancare. L’elenco USB non dimostra l’intera libreria Foto/Google Foto. Non è garantito un totale prestabilito.
- Windows 11 x64, destinazione locale NTFS con percorso massimo 120 caratteri. Origine locale separata dall’archivio, nessuna cartella dentro l’altra. Rete, exFAT, Windows ARM, link/junction e placeholder cloud non disponibili localmente non supportati.
- Senza conversione dei byte ricevuti. Originalità rispetto all’app fotografica non certificata indipendentemente. La riproduzione HEVC dipende dai codec del PC; la copia non richiede acquisti.

La candidata usa dati sintetici per i controlli automatici. **Il trasferimento fisico con iPhone e Android e l’esportazione dei veri album devono ancora essere confermati sui dispositivi.**

## Privacy e distribuzione

Nessuna scrittura o cancellazione sul telefono, conversione, upload, telemetria, pagamento o Apple ID. Nessuna installazione di driver o servizi, condivisione di rete automatica o riduzione delle protezioni Windows. Eseguibile non firmato commercialmente: Windows può mostrare avvisi o impedirne l’avvio secondo le proprie policy. Dopo l’aggiornamento usa la nuova app per l’archivio aggiornato.

La destinazione scelta viene ricordata localmente in `%LOCALAPPDATA%\Porto\destination.txt`. Vecchie versioni e archivi conservati.

[Test eseguiti](docs/TESTS.md) · [Revisione rc.10](docs/REVIEW-RC10.md) · [Architettura](docs/ARCHITECTURE.md) · [Fonti](docs/SOURCES.md) · [Verifica pubblicazione](docs/RELEASE-VERIFICATION.md).

SDK .NET 10, runtime incluso 10.0.14. `scripts/dev.ps1 test -Large`, `scripts/dev.ps1 build`, `scripts/dev.ps1 publish`. Test sintetici isolati. `--ui-smoke <percorso.png>` verifica la UI senza cercare telefoni. Non avviare discovery senza autorizzazione al dispositivo.

Licenza MIT. Progetto indipendente, non affiliato ad Apple, Google o Microsoft.
