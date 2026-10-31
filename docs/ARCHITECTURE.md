# Architettura e sicurezza

## USB e processi

WinForms avvia un processo figlio dello stesso eseguibile con --usb-worker e pipe stdin/stdout. Solo il worker usa WPD: COM in MTA, PortableDeviceFTM, GENERIC_READ e STGM_READ. Non esistono comandi per eliminare, rinominare o modificare contenuti del telefono.

I comandi ammessi sono fissi: ricerca/diagnosi, connessione, inventario, apertura, lettura e chiusura. Timeout di 60 secondi e cancellazione coprono invio e ricezione, anche con pipe satura. Frame invalidi, EOF e timeout terminano il worker; un errore remoto valido del singolo file permette di raggiungere gli altri nella stessa sessione. I progressi sono limitati a circa quattro al secondo, anche con metadati illeggibili e conteggio zero.

Gli ID oggetto non attraversano connessioni WPD: dopo una sessione morta serve un nuovo inventario. ImportSession crea fino a tre passaggi sullo stesso ID dispositivo, con attesa cancellabile di 15 secondi. Nessun cambio automatico di telefono. Disco pieno, permessi mancanti e pausa interrompono il recupero. Il worker possiede al massimo uno stream, liberato prima dell'operazione seguente o alla terminazione; il cleanup del client non può sostituire un errore di copia con un errore di close.

GetDevices S_FALSE provoca tentativi limitati, poi una diagnosi di elenco incompleto. L'inventario ammette al massimo un milione di oggetti tentati, profondità 128 e 10.000 errori. ID vuoti/ripetuti, proprietà illeggibili e rami non esplorati restano errori, non successi silenziosi. COM sintetico e processi figli di test esercitano i confini nativi senza enumerare hardware.

## Destinazione e persistenza

Destinazioni locali NTFS, percorso breve, nessuna junction/symlink. Un lock esclusivo impedisce due importatori simultanei nella stessa cartella. I percorsi del catalogo non possono uscire dalla destinazione. I file esistenti controllati devono avere un solo collegamento NTFS; un hard link viene rifiutato prima di append o apertura SQLite.

SQLite di Windows usa query parametrizzate, journal DELETE e synchronous FULL. Catalogo e registro lavori hanno application_id e versione di schema distinti. Prima di aprire un DB esistente in scrittura vengono verificati header, schema completo (incluse colonne nascoste/generate), indici, assenza di trigger/view e compatibilità della versione. Le vecchie strutture Porto riconosciute vengono migrate.

Un journal richiede struttura riconoscibile, dimensioni coerenti e primo record verificabile; i casi troncati o non attribuibili vengono conservati e rifiutati. Porto non usa ATTACH: i riferimenti a super/master-journal vengono respinti prima di aprire SQLite. Un journal recuperabile viene prima provato su una copia locale isolata. Questo è controllo strutturale, non autenticazione crittografica: database e journal non devono essere modificati, sostituiti o mescolati manualmente. Non è una difesa contro un processo locale ostile concorrente. La reale perdita di alimentazione/corruzione del volume resta fuori dalle prove.

Inventari e report vengono scritti in nuovi temporanei, flushati e pubblicati tramite rinomina senza sostituzione. Un'interruzione può lasciare un file chiaramente temporaneo, non un JSON finale troncato. Il catalogo conserva anche i risultati delle singole operazioni e dei passaggi.

## Copia e recupero

Stati principali: Copying → Ready → Complete; Quarantined per contenuti ambigui, Missing/Damaged per copie da riparare, Replaced per intenti risolti da una nuova copia. Nessuna cancellazione automatica di video o temporanei di trasferimento.

1. Registrare l'intento. Creare un .part con nome UUID oppure riaprire solo il temporaneo Copying associato dal catalogo.
2. Per la ripresa, leggere USB dall'inizio e confrontare ogni byte del prefisso sotto lock. Aggiungere soltanto il suffisso; mai troncare o riscrivere il prefisso. Divergenze conservano il temporaneo come Abandoned.
3. Copia a buffer 1 MiB, contatori Int64, SHA-256 e controllo spazio durante il lavoro. Riserva di 64 MiB e stima iniziale delle dimensioni note.
4. Flush(true), chiusura e hash tramite rilettura locale. Confrontare la dimensione dichiarata, quando disponibile. Verifica approfondita con seconda lettura USB; letture discordanti diventano incertezza persistente.
5. Classificare il contenitore; pubblicare soltanto dopo i controlli. MoveFileEx senza REPLACE_EXISTING e con WRITE_THROUGH. I nomi sorgente vengono sanificati e ricevono un suffisso UUID.

Rinomina e SQLite non sono un'unica transazione. Alla ripresa, Ready verifica il finale o il temporaneo prima di completare. Collisioni conservano il file estraneo e registrano un nuovo percorso. Le riparazioni collegano copia nuova e intento Missing/Damaged in una transazione; la copia danneggiata rimane conservata. L'errore di rilevamento resta nel report del relativo passaggio, ma non è riproposto per sempre dopo una riparazione riuscita.

## Contabilità dei video

La chiave descrittiva non prova identità. work.db mantiene osservazioni, inventari, fingerprint e conteggi per contenuto: si conserva un limite prudenziale alla molteplicità storica, invece di considerare permanentemente equivalenti ID che hanno restituito gli stessi byte. Snapshot e contatori vengono aggiornati transazionalmente, senza materializzare coppie quadratiche.

Un elemento appena osservato è anche unread. Solo una lettura attuale completa con le verifiche richieste può cancellare tale stato; un fingerprint storico o un alias successivo non può far sparire un elemento non riletto. Identità con byte differenti e debiti legacy senza una storia sufficiente restano non verificabili. Non si inventa una prova per trasformare il risultato in successo.

Il riuso richiede lettura completa della sorgente, hash e lunghezza coincidenti e rilettura della copia locale scelta. Ogni copia può essere assegnata una sola volta nell'inventario corrente. Le copie aggiuntive necessarie ai conteggi storici non vengono rimesse nella coda dello stesso passaggio. I deficit provati possono attivare ulteriori passaggi limitati. Due inventari vengono confrontati includendo la molteplicità, ma non costituiscono uno snapshot atomico di Foto.

Usare una cartella per telefono/libreria. L'identità tra librerie differenti, gli originali Foto e gli elementi solo iCloud non sono certificabili da questi confronti. I cataloghi non devono essere rimossi o modificati manualmente.

## Formati, interfaccia e privacy

Il parser controlla limiti degli atom, gerarchia moov/trak/mdia/hdlr, traccia vide e mdat, con limiti di profondità, numero di atom e metadati. Non decodifica i fotogrammi e non certifica riproducibilità o originalità. Le indicazioni Live Photo/varianti ambigue impediscono la pubblicazione automatica fra i video autonomi.

La cartella scelta viene ricordata solo nel profilo locale. Durante il lavoro viene richiesta la prevenzione della sospensione inattiva; blocco schermo e sospensione manuale restano invariati. Nessuna elevazione, installazione di driver, telemetria, account, upload o aggiornamento automatico. Cataloghi e report contengono nomi/ID locali: non pubblicarli senza controllarli.

Le dipendenze native vengono cercate in System32. Il pacchetto include il runtime Microsoft e viene costruito in uno staging nuovo per ogni release; non incorpora residui delle build precedenti. L'eseguibile è non firmato. La compatibilità con un iPhone fisico deve ancora essere provata.
