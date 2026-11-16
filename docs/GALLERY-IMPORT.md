# Gallerie e album — obiettivo e confini

Richiesta del 16 novembre 2026: collegare un telefono, scegliere Solo video / Solo foto / Foto e video, tutta la galleria oppure un album o raccolta, scegliere la destinazione PC e avviare una copia in massa verificata. Nessuna esportazione preliminare richiesta nel flusso principale. Copia, mai spostamento o cancellazione. Le componenti animate delle Live Photo non sono video autonomi della libreria Foto.

## Criteri verificabili

- Una cartella USB si apre leggendo solo i figli immediati, senza inventariare prima tutti i file del telefono.
- Un import limitato a una cartella legge solo il ramo scelto e i metadati necessari a ritrovarlo. La riconnessione non allarga la selezione.
- Un album logico deriva da un elenco esplicito di appartenenze del catalogo del dispositivo; nome cartella, estensione MOV e durata non ne provano l'appartenenza.
- Una selezione di video della galleria esclude gli asset Foto e le loro componenti Live, mantenendo visibili gli asset video non disponibili o non interpretabili.
- Errori, media soltanto nel cloud, formati non supportati e cambi di inventario restano visibili. Non si dichiara verificata l'intera galleria sulla base dei soli file USB.
- Copie precedenti e file estranei restano conservati; il confronto continua a richiedere byte completi e verifica della copia PC.
- Nessun test automatico legge un telefono reale. Nessuna installazione di driver o servizi, pagamento, account cloud, telemetria o modifica delle protezioni.

## Distinzioni che l'interfaccia deve conservare

| Fonte | Selezione possibile | Limite |
| --- | --- | --- |
| Cartelle USB WPD/MTP/PTP | Tutti i file accessibili oppure un ramo fisico | Non equivale necessariamente alla galleria; album privati e cloud possono essere assenti |
| Catalogo Foto iPhone via USB | Asset classificati e appartenenze agli album, se lo schema è riconosciuto | Accessibilità e schema dipendono da iOS; una lettura ripetuta non è uno snapshot transazionale della libreria |
| Cartella locale | File presenti, anche ricorsivamente | Opzione accessoria; non sostituisce l'importazione diretta richiesta |

Non esiste una promessa verificata di compatibilità con qualsiasi cellulare o qualsiasi app Galleria. Android MediaStore è un'API sul telefono; le raccolte virtuali delle diverse app non sono tutte esposte attraverso Windows WPD. Non chiamare una cartella fisica “album della galleria” per nascondere questa differenza.

## Fonti e valutazione del catalogo iPhone

- Microsoft: [EnumObjects](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicecontent-enumobjects) enumera figli immediati; non richiede una scansione ricorsiva preliminare.
- Microsoft: [tipi di contenuto WPD](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/content-types) distingue album astratti e cartelle. Il supporto effettivo è del dispositivo/driver.
- Android: [MediaColumns](https://developer.android.com/reference/android/provider/MediaStore.MediaColumns) documenta BUCKET_ID e BUCKET_DISPLAY_NAME; questi non sono un accesso universale agli album privati delle applicazioni.
- Apple: [pairedVideo](https://developer.apple.com/documentation/photos/phassetresourcetype/pairedvideo) è una risorsa di Live Photo; [video](https://developer.apple.com/documentation/photos/phassetresourcetype/video) ha un ruolo distinto.
- Evidenza di fattibilità indipendente: [iPhone Manager](https://github.com/tmkaio/iphone-manager), in particolare albums.py, usa Photos.sqlite tramite AFC su Windows. Non è una garanzia Apple e il codice GPL non viene incorporato in Porto MIT.
- SQLite: [WAL](https://www.sqlite.org/wal.html). Copiare file live non dà automaticamente uno snapshot coerente. Controlli di integrità e letture ripetute devono avere limiti espliciti; nessun certificato di completezza deriva dalla sola validità sintattica.
- Apple: [importazione su PC](https://support.apple.com/en-us/120267). Gli originali soltanto in iCloud devono essere disponibili sul dispositivo per l'importazione USB.

Stato di verifica e compatibilità reale devono essere registrati nella documentazione della candidata, separatamente da questi criteri.
