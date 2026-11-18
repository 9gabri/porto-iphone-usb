# Porto

App locale in italiano per copiare foto e video dal telefono al PC, senza conversioni. **0.1.0-rc.23 candidata**, Windows 11 x64 con runtime incluso. Riduce le riletture del catalogo Foto quando tutti i suoi byte risultano ancora uguali a quelli già verificati. Prestazioni e compatibilità della nuova versione da confermare sul telefono reale.

[Scarica Windows](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.23/Porto-0.1.0-rc.23-win-x64.zip) · [Checksum](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.23/SHA256SUMS.txt) · [Guida](docs/PHONES-AND-ALBUMS.md)

## Riletture del catalogo rc.23

La prima lettura mantiene due acquisizioni complete e la verifica SQLite/WAL. Nei passaggi successivi Porto legge ancora tutto il catalogo corrente una volta: se le impronte SHA-256 di DB e WAL, il dispositivo e i metadati corrispondono alla precedente verifica, riutilizza l'elenco già interpretato. Se qualcosa cambia, ripete anche la seconda lettura e tutti i controlli. Nessun riutilizzo basato soltanto su data o dimensione.

Con catalogo invariato, scelta album + avvio + controllo finale passano da sei letture complete a quattro. Il risparmio riguarda il catalogo, non garantisce la stessa riduzione sul tempo dell'intera importazione. Nessun database conservato su disco; una sola osservazione interpretata in RAM, eliminata alla chiusura o al cambio dispositivo. I metadati dei media vengono riletti e il motore di copia resta invariato. [Prove e limiti](docs/REVIEW-RC23.md).

## Memoria catalogo rc.22

Acquisizione e SQLite usano la stessa immagine in RAM, azzerata e liberata al termine. Eliminata la seconda copia completa; conservati confronto USB integrale, WAL, controllo integrità, lettura sola e margine di 512 MiB. Con le dimensioni dell'ultima schermata la soglia iniziale scende da 3,37 a 1,95 GB liberi. Il WAL può richiedere ulteriore memoria se cambia la dimensione del database. La disponibilità è misurata, non prenotata; restano possibili errori di memoria o catalogo. [Verifiche rc.22](docs/REVIEW-RC22.md).

## Cataloghi grandi rc.21

Corretto il blocco DB-LIMIT osservato con database da 1.416.859.648 byte e WAL da 7.337.752 byte. Il limite DB+WAL è ora 2047 MiB (circa 2 GiB), con spazio per padding e calcoli. Prima dell'acquisizione deve esserci RAM e capacità di allocazione per cattura, copia di verifica e margine: nel caso segnalato circa 3,4 GB liberi. I controlli vengono ripetuti prima delle allocazioni successive. Non salva il catalogo del telefono su disco, non legge una versione parziale e non disattiva verifiche. [Riproduzione e prova completa sintetica](docs/REVIEW-RC21.md).

## Prestazioni iPhone rc.20

Quando trova copie precedenti, Porto conserva temporaneamente sul PC i byte letti per il confronto. Se sono nuovi, li usa per creare la copia senza dover rileggere tutto il file dall'iPhone. La rilettura SHA-256 del file salvato resta obbligatoria. Servono spazio per il temporaneo e per la copia; se manca il margine, viene usato il percorso precedente. I file identici già verificati sono riutilizzati; elementi distinti con gli stessi byte mantengono copie distinte.

Il trasporto Apple richiede fino a 512 KiB per lettura, rispetto ai 256 KiB della rc.19 e ai 64 KiB della rc.18. L'opzione **USB iPhone compatibile** ripristina blocchi da 64 KiB se la prova reale incontra errori. Non attivarla per il primo confronto di velocità. La verifica approfondita continua a richiedere una lettura indipendente aggiuntiva.

I file locali `performance-*.json` in Report Porto registrano byte letti, aperture e tempi per passaggio. Nessun invio automatico. Non garantiamo 29 GB in un'ora: catalogo, connessione, disco e verifiche continuano a richiedere tempo. Nel test AFC con attesa simulata il tempo si dimezza, ma non è una misura dell'importazione reale. Una seconda ottimizzazione locale è stata esclusa perché il guadagno di tempo non era dimostrato. [Modifiche e misure](docs/REVIEW-RC20.md).

## Scegli origine, contenuti e destinazione

- **iPhone · libreria Foto via USB:** nuovo accesso al catalogo Foto tramite il collegamento Apple già installato e abbinato. Se il catalogo è compatibile, scegli galleria visibile o un album personale, poi Solo video / Solo foto / Foto e video. Le Live Photo sono foto: la loro componente animata non entra in Solo video. Nessuna esportazione preliminare. Le ottimizzazioni rc.19 devono ancora essere collaudate con un iPhone reale.
- **Dispositivo USB · cartelle e raccolte:** dispositivi WPD/MTP/PTP esposti da Windows, inclusi Android compatibili. Scegli tutti i file accessibili oppure una cartella fisica o raccolta USB nativa. La scelta legge soltanto il livello aperto; la copia di una cartella resta limitata al suo ramo. Gli album virtuali delle app non sono sempre esposti.
- **Cartella del PC:** opzione accessoria per file già presenti sul computer; confronto e copia verificata nella destinazione scelta.

Premi **Avvia importazione** per copiare in massa il tipo scelto. Non occorre selezionare i singoli file. Pausa e Riprendi conservano il lavoro e ripetono i controlli.

**Non è compatibilità universale:** tutti i modelli di telefono, tutte le app Galleria e qualsiasi raccolta non possono essere dichiarati supportati. In particolare raccolte intelligenti/condivise iPhone e album privati/cloud Android possono non essere accessibili. Porto segnala questi limiti; non sostituisce silenziosamente un album con l'intero telefono. [Capacità e confini](docs/GALLERY-IMPORT.md).

## Associazioni album iPhone rc.18

La prova rc.17 ha raggiunto la lettura delle associazioni album dopo acquisizione e integrità del catalogo. Una singola associazione irrisolta bloccava anche album indipendenti e integri. La rc.18 mantiene il conteggio delle anomalie e blocca gli album coinvolti, ma permette di scegliere quelli con collegamenti verificabili. La galleria completa resta bloccata in presenza di qualsiasi associazione irrisolta. Non interpreta tali collegamenti come contenuti eliminati e non amplia la selezione. L'album desiderato potrebbe essere ancora bloccato: la prossima prova deve confermarlo. La rc.21 aggiorna il limite e il budget di memoria senza cambiare queste regole. [Analisi rc.18](docs/REVIEW-RC18.md).

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
- Il catalogo iPhone usa uno schema privato, non un'API PhotoKit ufficiale per Windows. Letture ripetute, WAL e integrità controllati; nessuno snapshot atomico della libreria certificato. Limite dei dati catalogo+WAL acquisiti: 2047 MiB; RAM e capacità di allocazione devono essere sufficienti per l'immagine, eventuale ridimensionamento WAL e margine. Solo album personali riconosciuti; Nascosti ed Eliminati di recente esclusi con conteggi visibili.
- Originali soltanto su iCloud/Google Foto non vengono scaricati. Gli elementi individuati ma non disponibili restano nel report. Nessun totale prestabilito promesso.
- Windows 11 x64, archivio locale NTFS, percorso massimo 120 caratteri; niente rete, exFAT, Windows ARM, link/junction. Origine locale separata dalla destinazione.
- Nessuna scrittura sul telefono, conversione, upload, telemetria, acquisto o Apple ID. Nessuna installazione o avvio di servizi Windows, driver o riduzione delle protezioni. Abbinamento Apple già autorizzato riutilizzato senza creazione di nuove credenziali; chiavi e database Foto non salvati come file. I report e lo stato locale contengono metadati degli elementi selezionati necessari alla ripresa.
- Programma non firmato commercialmente: le policy Windows possono avvisare o impedirne l'avvio. La destinazione viene ricordata in `%LOCALAPPDATA%\Porto\destination.txt`.

**Candidata per il collaudo, non versione certificata o definitiva.** Le verifiche sintetiche non dimostrano compatibilità hardware. Restano da confermare collegamento Apple reale, schema iOS, album e conteggi sul telefono, trasferimento e ripresa su iPhone/Android.

[Test](docs/TESTS.md) · [Revisione rc.21](docs/REVIEW-RC21.md) · [Architettura](docs/ARCHITECTURE.md) · [Licenze](THIRD-PARTY-NOTICES.md) · [Verifica pubblicazione](docs/RELEASE-VERIFICATION.md).

SDK .NET 10, runtime incluso 10.0.14. `scripts/dev.ps1 test -Large`, `scripts/dev.ps1 build`, `scripts/dev.ps1 publish`. Test isolati; `--ui-smoke <percorso.png>` non cerca telefoni. Nessuna discovery hardware automatica all'avvio.

Licenza MIT. Progetto indipendente, non affiliato ad Apple, Google o Microsoft.
