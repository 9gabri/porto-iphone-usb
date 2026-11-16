# Porto

App locale in italiano per copiare foto e video dal telefono al PC, senza conversioni. **0.1.0-rc.16 diagnostica**, Windows 11 x64 con runtime incluso. La lettura degli album sul caso iPhone corrente resta bloccata.

[Scarica Windows](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.16/Porto-0.1.0-rc.16-win-x64.zip) · [Checksum](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.16/SHA256SUMS.txt) · [Guida](docs/PHONES-AND-ALBUMS.md)

## Scegli origine, contenuti e destinazione

- **iPhone · libreria Foto via USB:** nuovo accesso al catalogo Foto tramite il collegamento Apple già installato e abbinato. Se il catalogo è compatibile, scegli galleria visibile o un album personale, poi Solo video / Solo foto / Foto e video. Le Live Photo sono foto: la loro componente animata non entra in Solo video. Nessuna esportazione preliminare. Questa integrazione deve ancora essere collaudata con un iPhone reale.
- **Dispositivo USB · cartelle e raccolte:** dispositivi WPD/MTP/PTP esposti da Windows, inclusi Android compatibili. Scegli tutti i file accessibili oppure una cartella fisica o raccolta USB nativa. La scelta legge soltanto il livello aperto; la copia di una cartella resta limitata al suo ramo. Gli album virtuali delle app non sono sempre esposti.
- **Cartella del PC:** opzione accessoria per file già presenti sul computer; confronto e copia verificata nella destinazione scelta.

Premi **Avvia importazione** per copiare in massa il tipo scelto. Non occorre selezionare i singoli file. Pausa e Riprendi conservano il lavoro e ripetono i controlli.

**Non è compatibilità universale:** tutti i modelli di telefono, tutte le app Galleria e qualsiasi raccolta non possono essere dichiarati supportati. In particolare raccolte intelligenti/condivise iPhone e album privati/cloud Android possono non essere accessibili. Porto segnala questi limiti; non sostituisce silenziosamente un album con l'intero telefono. [Capacità e confini](docs/GALLERY-IMPORT.md).

## Diagnosi del catalogo iPhone rc.16

La rc.15 ha superato il precedente blocco TLS ed è arrivata ai metadati del catalogo Foto. Il nuovo messaggio aggregava tipo di file, dimensione nulla e superamento del limite di 512 MiB. La rc.16 distingue le cause con un codice e le dimensioni numeriche di database e WAL. Non aumenta il limite, non segue collegamenti e non salva il catalogo sul PC. Serve a ottenere l'informazione mancante per la correzione; non è presentata come pronta per completare l'importazione dal caso iPhone corrente. [Analisi rc.16](docs/REVIEW-RC16.md).

Se una copia USB è in corso, attendi la fine o una pausa confermata e chiudi Porto prima di aggiornare. Estrai la candidata in una cartella nuova, conservando tutti i file delle prove.

## Revisione iPhone rc.13

Rafforzati il rifiuto delle risposte Apple malformate e il controllo della selezione tra i tentativi automatici: un elemento scomparso non consente di dichiarare riuscita una selezione più piccola. La correzione JPEG della rc.12 resta inclusa. Il nuovo percorso iPhone richiede ancora il collaudo fisico: prima verifica collegamento e lettura degli album, poi una piccola selezione Solo video, infine l'importazione completa. [Prova iniziale](docs/PHONES-AND-ALBUMS.md#prima-prova-iphone-rc16).

## Correzione JPEG rc.12

Le foto JPEG con dati aggiuntivi dopo la fine dell’immagine principale vengono copiate integralmente. Il report distingue il controllo della struttura JPEG principale dal contenuto aggiuntivo non interpretato. La correzione è comune a tutte le origini e non dipende dalla marca del telefono. I temporanei rifiutati dalla versione precedente possono essere recuperati nella stessa destinazione, mantenendo i vecchi file.

## Copie precedenti

Estrai lo ZIP in una cartella nuova, conservando le vecchie versioni. Usa lo stesso Archivio PC delle prove precedenti, inclusi `.porto` e report. Le copie vengono confrontate per contenuto completo e SHA-256; quelle identiche verificate sono riutilizzate al loro posto. Due elementi selezionati distinti con gli stessi byte mantengono due copie. File danneggiati o estranei non vengono cancellati o sovrascritti; i recuperi ricevono nuovi nomi.

Le nuove foto sono in **Foto Porto**, i video in **Video Porto**. Le vecchie risorse aggiuntive e le copie non riutilizzate restano conservate: questo non le identifica automaticamente come Live Photo. Il percorso USB generico conserva ancora separatamente gli indizi Apple nelle Risorse video aggiuntive; per la classificazione da catalogo usa la modalità iPhone.

## Limiti e privacy

- Video MOV/MP4/M4V; foto JPEG, PNG statico e HEIF/HEIC HEVC nei profili riconosciuti. Altri formati restano segnalati, non dichiarati verificati. Verifica strutturale e dei byte, non decodifica completa.
- Il catalogo iPhone usa uno schema privato, non un'API PhotoKit ufficiale per Windows. Letture ripetute, WAL e integrità controllati; nessuno snapshot atomico della libreria certificato. Limite dei dati catalogo+WAL acquisiti: 512 MiB; le verifiche richiedono ulteriori copie in memoria. Solo album personali riconosciuti; Nascosti ed Eliminati di recente esclusi con conteggi visibili.
- Originali soltanto su iCloud/Google Foto non vengono scaricati. Gli elementi individuati ma non disponibili restano nel report. Nessun totale prestabilito promesso.
- Windows 11 x64, archivio locale NTFS, percorso massimo 120 caratteri; niente rete, exFAT, Windows ARM, link/junction. Origine locale separata dalla destinazione.
- Nessuna scrittura sul telefono, conversione, upload, telemetria, acquisto o Apple ID. Nessuna installazione o avvio di servizi Windows, driver o riduzione delle protezioni. Abbinamento Apple già autorizzato riutilizzato senza creazione di nuove credenziali; chiavi e database Foto non salvati come file. I report e lo stato locale contengono metadati degli elementi selezionati necessari alla ripresa.
- Programma non firmato commercialmente: le policy Windows possono avvisare o impedirne l'avvio. La destinazione viene ricordata in `%LOCALAPPDATA%\Porto\destination.txt`.

**Candidata per il collaudo, non versione certificata o definitiva.** Le verifiche sintetiche non dimostrano compatibilità hardware. Restano da confermare collegamento Apple reale, schema iOS, album e conteggi sul telefono, trasferimento e ripresa su iPhone/Android.

[Test](docs/TESTS.md) · [Revisione rc.16](docs/REVIEW-RC16.md) · [Architettura](docs/ARCHITECTURE.md) · [Licenze](THIRD-PARTY-NOTICES.md) · [Verifica pubblicazione](docs/RELEASE-VERIFICATION.md).

SDK .NET 10, runtime incluso 10.0.14. `scripts/dev.ps1 test -Large`, `scripts/dev.ps1 build`, `scripts/dev.ps1 publish`. Test isolati; `--ui-smoke <percorso.png>` non cerca telefoni. Nessuna discovery hardware automatica all'avvio.

Licenza MIT. Progetto indipendente, non affiliato ad Apple, Google o Microsoft.
