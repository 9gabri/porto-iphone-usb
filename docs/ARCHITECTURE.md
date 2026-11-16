# Architettura e sicurezza

## Cartella selezionata — rc.9

`FolderVideoSource` implementa `ISelectedVideoSource`: è una selezione dell’utente, non un inventario PhotoKit. Prima di creare Destination verifica radici locali NTFS, percorsi canonici e assenza di sovrapposizione. Inventario ricorsivo senza scrittura, senza attraversare reparse point o file cloud offline. Handle aperti in sola lettura con FileShare.Read; percorso effettivo, numero di hard link, identità NTFS, lunghezza e ultima modifica devono coincidere. ID derivati da radice/percorso; la versione dei metadati entra in WorkLedger.Key. Una sostituzione con stessi byte e stessa data ma altra identità NTFS viene rilevata.

L’engine conserva i controlli byte/container, ma non usa indizi IMG_E, foto omonime o metadati temporizzati per escludere o separare file della selezione. Formati video comuni non supportati sono Unknown e ambigui, non esclusioni invisibili. Il controllo finale rileva aggiunte, rimozioni e variazioni di metadati; non è uno snapshot atomico della libreria Foto né impedisce modifiche successive.

`FolderArchiveComparison` esamina MOV/MP4/M4V non registrati nell’archivio, esclusi gli alberi di stato/report. Hash con lettura sola e progressi; errori di confronto non vengono ignorati. Le descrizioni esterne non possono essere salvate nel catalogo, diventare replacement target o causare modifiche al file. Cataloghi validi continuano a gestire copie proprie e ripresa.

Ogni coda per contenuto assegna una copia fisica precedente al massimo una volta per inventario. Nella modalità cartella una selezione ridotta può riutilizzare copie integre anche se resta un debito di molteplicità USB storico; il debito resta visibile e non provoca copie extra né retry automatici della selezione. Le copie nuove non entrano nella coda di riuso nello stesso passaggio. Per file convertiti o modificati non esiste associazione semantica basata soltanto su nome/durata.

Il report aggiunge SelectedFolder, SourceDescription, SelectedFolderVerified e PreviousCopies (percorso, byte, SHA-256 atteso, ReusedForSelection, IntegrityVerified, motivazione). ReusedForSelection indica assegnazione a un elemento corrente, non l’unica presenza di quel contenuto. Per file non registrati IntegrityVerified indica concordanza delle riletture locali: non prova integrità storica rispetto a un originale; il testo lo chiarisce. Solo il riuso aggiunge un confronto con la sorgente corrente. Include anche file pubblicati precedenti Replaced/Abandoned conservati; stato non più attivo non implica cancellazione. I campi storici UsbEnumerationComplete/UsbItemCount rimangono per compatibilità del JSON e, in modalità cartella, rappresentano inventario/elementi della sorgente locale. UI e TXT usano etichette di cartella.

Un singolo passaggio da cartella, riprendibile dall’utente; nessun worker USB o discovery automatico. Cartella verificata richiede tutti i candidati attuali risolti e inventario stabile, mentre difetti dell’archivio e osservazioni USB storiche restano distinti. Byte dei video esistenti mai sovrascritti; database propri aggiornati e temporanei associabili appendibili dopo confronto completo del prefisso.

## USB e processi

La rc.7 aggiunge IReadFailureRecovery sul solo adapter ReconnectingUsbSource.
L’engine invalida la lettura dopo un errore, inclusi flusso corto o troppo lungo
e discordanza SHA-256 rilevati dopo EOF. Il segnale non effettua I/O e non
maschera l’errore primario. Prima dell’apertura seguente viene terminato solo
il processo figlio posseduto da Porto, poi avviato un worker nuovo sullo stesso
dispositivo. Un inventario completo, senza errori, deve coincidere con quello
precedente in tutti i campi e nella molteplicità; l’elemento richiesto deve
appartenervi. Nessuna associazione euristica per solo nome, dimensione o PUID.
Se il confronto fallisce, il video non ancora aperto rimane pendente e si
passa al recupero esterno. La richiesta esplicita Inventory dopo un errore
restituisce invece l’elenco nuovo, che il controllo finale confronta e registra.
Con inventario stabile il recupero è limitato a un riavvio per errore, senza
ripetere il file nello stesso passaggio: cinque file difettosi non impediscono
ai sani successivi di essere copiati. Timeout, disconnessioni e inventari
cambiati richiedono nuovi passaggi esterni con il budget limitato rc.8. I report registrano
versione applicativa e riavvii USB tentati nel passaggio.


La rc.6 possiede esplicitamente lo stream nativo attivo. Un'operazione che non
ha raggiunto EOF viene annullata con Resources.Cancel prima del rilascio;
GetStream fallito viene bonificato allo stesso modo. Una bonifica fallita viene
memorizzata senza mascherare l'errore primario e impedisce ulteriori operazioni
sulla connessione. Il worker trasmette il flag PreviousOperation: l'engine
mantiene pendente la risorsa successiva non ancora aperta e rinvia i soli file
realmente falliti. Ogni nuovo collegamento richiede comunque inventario nuovo.
La generazione locale di WorkerClient rifiuta letture da wrapper sostituiti.
Un blocco nel cleanup è coperto dal timeout dell'intero comando; in quel caso
la fase esatta può non essere distinguibile e i tentativi restano limitati.

WinForms avvia un processo figlio dello stesso eseguibile con --usb-worker e pipe stdin/stdout. Solo il worker usa WPD: COM in MTA, PortableDeviceFTM, GENERIC_READ e STGM_READ. Non esistono comandi per eliminare, rinominare o modificare contenuti del telefono.

I comandi ammessi sono fissi: ricerca/diagnosi, connessione, inventario, apertura, lettura e chiusura. Timeout di 60 secondi e cancellazione coprono invio e ricezione, anche con pipe satura. Frame invalidi, EOF e timeout terminano il worker; un errore remoto valido del singolo file viene registrato e richiede il recupero sopra descritto prima della lettura seguente. I progressi sono limitati a circa quattro al secondo, anche con metadati illeggibili e conteggio zero.

Gli errori nativi espliciti di sessione persa vengono propagati anche dalle letture dei metadati. Il frame 4 conserva il codice HRESULT e invalida il worker; il frame 3 riguarda gli errori del singolo elemento. EOF e timeout durante Read appartengono al file corrente, evitando di accusare quello successivo.

Ogni apertura può restituire la dimensione WPD_RESOURCE_ATTRIBUTE_TOTAL_SIZE della risorsa predefinita. È un Int64 non negativo opzionale trasportato nel descrittore del worker; un descrittore malformato invalida la sessione. Proprietà non disponibile: controllo sulla dimensione dell'inventario. Una dimensione di risorsa diversa dall'inventario obbliga a due letture USB complete concordanti, oltre alla verifica locale. Ogni lettura deve rispettare la propria dimensione dichiarata: non si ignorano i troncamenti. IStream.Read con S_FALSE conserva gli eventuali ultimi byte e memorizza EOF, evitando un'altra chiamata al driver; un blocco corto con S_OK prosegue. Viene rispettato anche un buffer ottimale inferiore a 4096 byte.

Gli ID oggetto non vengono usati su una nuova connessione senza un nuovo inventario completo. Il recupero interno richiede coincidenza esatta; altrimenti l’engine riparte dal nuovo elenco. ImportSession crea tre passaggi ordinari e continuazioni rc.8 limitate sullo stesso ID dispositivo, con attesa cancellabile di 15 secondi. Nessun cambio automatico di telefono. Disco pieno, permessi mancanti e pausa interrompono il recupero. Il worker possiede al massimo uno stream, liberato prima dell'operazione seguente o alla terminazione; il cleanup del client non può sostituire un errore di copia con un errore di close.

Un'interruzione conserva un suggerimento d'ordine: nome, dimensione e posizione nel gruppo dell'inventario originale. Al passaggio successivo tutti gli elementi non ancora falliti, in tutti i gruppi, precedono quelli che hanno già interrotto una sessione. Ogni elemento viene comunque tentato e verificato per contenuto. Gli ID possono cambiare; i suggerimenti non riconciliano identità o debiti. Con un ordine stabile, due elementi bloccati non impediscono di raggiungere quelli sani entro tre passaggi. Nella rc.8 anche gli elementi già verificati vengono ordinati dopo quelli non ancora raggiunti. Inventari riordinati o ulteriori blocchi possono esaurire il limite e restano nel report.

GetDevices S_FALSE provoca tentativi limitati, poi una diagnosi di elenco incompleto. L'inventario ammette al massimo un milione di oggetti tentati, profondità 128 e 10.000 errori. ID vuoti/ripetuti, proprietà illeggibili e rami non esplorati restano errori, non successi silenziosi. COM sintetico e processi figli di test esercitano i confini nativi senza enumerare hardware.

## Destinazione e persistenza

Destinazioni locali NTFS, percorso breve, nessuna junction/symlink. Un lock esclusivo impedisce due importatori simultanei nella stessa cartella. I percorsi del catalogo non possono uscire dalla destinazione. I file esistenti controllati devono avere un solo collegamento NTFS; un hard link viene rifiutato prima di append o apertura SQLite.

SQLite di Windows usa query parametrizzate, journal DELETE e synchronous FULL. Catalogo e registro lavori hanno application_id e versione di schema distinti. Prima di aprire un DB esistente in scrittura vengono verificati header, schema completo (incluse colonne nascoste/generate), indici, assenza di trigger/view e compatibilità della versione. Le vecchie strutture Porto riconosciute vengono migrate.

Le definizioni SQL devono corrispondere alle forme effettivamente prodotte da Porto, ammettendo differenze di spazi/case. Nel catalogo tutte le righe vengono controllate prima dell'uso: UUID, percorsi generati immediati, hash, stati, JSON sorgente e relazioni senza cicli. I percorsi fisici vengono verificati prima delle scritture, usando la destinazione originale anche quando il DB è ispezionato su snapshot. Una rinomina della sorgente non obbliga a rinominare una copia già verificata.

Un journal richiede struttura riconoscibile, dimensioni coerenti e primo record verificabile; i casi troncati o non attribuibili vengono conservati e rifiutati. Porto non usa ATTACH: i riferimenti a super/master-journal vengono respinti prima di aprire SQLite. Un journal recuperabile viene prima provato su una copia locale isolata. Questo è controllo strutturale, non autenticazione crittografica: database e journal non devono essere modificati, sostituiti o mescolati manualmente. Non è una difesa contro un processo locale ostile concorrente. La reale perdita di alimentazione/corruzione del volume resta fuori dalle prove.

Inventari e report vengono scritti in nuovi temporanei, flushati e pubblicati tramite rinomina senza sostituzione. Un'interruzione può lasciare un file chiaramente temporaneo, non un JSON finale troncato. Il catalogo conserva anche i risultati delle singole operazioni e dei passaggi.

La copia privata di validazione del DB/journal viene chiusa e rimossa in finally, limitandosi ai due nomi creati dall'operazione e alla relativa directory vuota. Nessuna pulizia ricorsiva o di cartelle preesistenti.

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

Anche gli elementi Unknown privi di un formato video riconosciuto vengono registrati, nell'inventario iniziale e finale. Una successiva classificazione con metadati simili non prova da sola che il vecchio elemento ignoto sia risolto. I gruppi di copie storiche vengono verificati anche quando il contenuto non ricompare nella sorgente corrente.

Dalla rc.5, gli AAE di tipo Other/Unknown senza indizi video sono accessori esclusi con motivazione. Non generano nuovi debiti Unknown; quelli già registrati dalle versioni precedenti restano conservati. Un tipo o formato video prevale sull'estensione. L'inventario continua a esaminare i metadati di tutte le risorse esposte via USB, senza copiare le fotografie. I conteggi distinguono queste risorse dai video autonomi e dalla libreria Foto; le associazioni sospette vengono tentate e poi classificate come risorse aggiuntive nella rc.8.

Il riuso richiede lettura completa della sorgente, hash e lunghezza coincidenti e rilettura della copia locale scelta. Ogni copia può essere assegnata una sola volta nell'inventario corrente. Le copie aggiuntive necessarie ai conteggi storici non vengono rimesse nella coda dello stesso passaggio. I deficit provati possono attivare ulteriori passaggi limitati. Due inventari vengono confrontati includendo la molteplicità, ma non costituiscono uno snapshot atomico di Foto.

La ricerca di copie riutilizzabili non viene saltata sulla sola base della dimensione inventariata: potrebbe essere diversa da quella della risorsa. L'identità richiede sempre il contenuto letto e verificato. Nessun cambiamento allo schema dei cataloghi nella rc.5.

Usare una cartella per telefono/libreria. L'identità tra librerie differenti, gli originali Foto e gli elementi solo iCloud non sono certificabili da questi confronti. I cataloghi non devono essere rimossi o modificati manualmente.

## Formati, interfaccia e privacy

Il parser controlla limiti degli atom, gerarchia moov/trak/mdia/hdlr, traccia vide e mdat, con limiti di profondità, numero di atom e metadati. Non decodifica i fotogrammi e non certifica riproducibilità o originalità. Nella rc.8 le indicazioni Live Photo/varianti non impediscono la lettura: le nuove copie valide vengono pubblicate nella cartella aggiuntiva.

La cartella scelta viene ricordata solo nel profilo locale. Durante il lavoro viene richiesta la prevenzione della sospensione inattiva; blocco schermo e sospensione manuale restano invariati. Nessuna elevazione, installazione di driver, telemetria, account, upload o aggiornamento automatico. Cataloghi e report contengono nomi/ID locali: non pubblicarli senza controllarli.

Le dipendenze native vengono cercate in System32. Il pacchetto include il runtime Microsoft e viene costruito in uno staging nuovo per ogni release; non incorpora residui delle build precedenti. L'eseguibile è non firmato. La compatibilità con un iPhone fisico deve ancora essere provata.

Il nome definitivo dello ZIP viene assegnato soltanto dopo compressione e controlli nello staging, con spostamento senza sostituzione. Il checksum rimane nello staging di quello specifico tentativo. Anche il file delle preferenze rifiuta collegamenti NTFS/reparse; i test delle preferenze usano percorsi sintetici, senza leggere il profilo reale.

## rc.8: copertura, classificazione e continuazioni

Nessuno skip pre-lettura basato su foto omonima o IMG_E. CandidateCount e
TransferCandidateCount coprono tutte le risorse candidate; le foto/AAE non vengono
lette. Un video valido con indizi viene pubblicato in Risorse video aggiuntive
con esito Additional/AdditionalAlreadyPresent, terminale anche nel ledger.
Le copie Complete precedenti mantengono il percorso; il risultato può essere
classificato aggiuntivo quando emergono indizi nuovi. Il catalogo ammette solo
le due cartelle esatte, con gli stessi controlli su UUID, estensioni e collegamenti.

Il parser riconosce still-image-time nella gerarchia della traccia meta:
stsd/mebx/keys/(ID locale)/keyd con namespace mdta. I local ID non sono tipi atom
ordinari. Una chiave registrata non dimostra che un campione abbia scritto il
valore: AdditionalResource è solo un indizio. Stringhe in free e content.identifier
isolato non bastano. Restano limiti di profondità/dimensioni e controlli dei parent.

Un Quarantined già presente viene verificato contro sorgente e file locale,
poi riclassificato. Se valido: nuovo intento Copying, copia locale nuova e flush,
SHA-256, Ready con Replaces, publish senza overwrite, transazione Complete+Replaced.
Il vecchio .part rimane fisicamente invariato. Recover gestisce Ready e collisioni
anche nella cartella aggiuntiva. Nessuna perdita del numero minimo di copie distinte.

SourceUnavailable dopo lettura può continuare nello stesso engine se il source
supporta IReadFailureRecovery; PreviousReadCleanup ferma sempre prima di aprire
il prossimo file. ReadRecoveryInterrupted distingue queste interruzioni dai
cambi del solo inventario finale. Oltre tre passaggi, continuazione solo se il
passaggio interrotto raggiunge un nuovo RetryHint. Budget fissato una volta a
min(CandidateCount del primo inventario completo,128); gli ID nuovi non lo aumentano.
RetryVisited evita di rileggere ogni volta il prefisso sano prima di raggiungere
le risorse nuove. Questi dati sono suggerimenti d'ordine, mai prove di identità.

ReportItem.Historical separa osservazioni precedenti irrisolte dalle risorse del
passaggio attuale. Non elimina debiti né permette il completamento in loro presenza.
Count mantiene il totale; CurrentCount alimenta l'interfaccia e la sezione corrente
del report. Le osservazioni storiche non si possono sommare come video distinti.


## Dispositivi e selezione media — rc.10

DeviceDiscovery accetta dispositivi WPD con ID univoci validi; il produttore Apple è un indizio separato, non un filtro di inclusione. Audio/documenti/file generici WPD riconosciuti non rendono da soli incompleto l’inventario; metadati contrari video/foto restano candidati. Tipi ignoti continuano a rendere visibile l’incertezza.

UsbImportSource racchiude ReconnectingUsbSource, conserva le verifiche del suo inventario completo e aggiunge SourceNamespace derivato da SHA-256 dell’ID dispositivo. OpenRead ammette solo oggetti osservati nella selezione corrente e inoltra al worker l’oggetto originale. I vecchi SourceItem con namespace nullo mantengono esattamente la serializzazione e le chiavi storiche.

UsbFolders ricostruisce percorsi da relazioni padre/figlio, respinge cicli, ID duplicati, genitori mancanti e scelte ambigue. La selezione conserva catena completa e persistent ID quando disponibili. Si risolve a ogni inventario; non ripiega mai sul telefono intero. Non implementa album virtuali o riferimenti WPD astratti.

ImportMedia governa candidatura e verifica in Engine/Session. Video resta il valore predefinito per retrocompatibilità. Foto Porto è una destinazione aggiuntiva validata dal catalogo; nessun ampliamento a percorsi arbitrari. PhotoInspector verifica struttura JPEG/PNG/HEIF HEVC, con limiti di dimensione/complessità, senza ricodifica. Report JSON e testo espongono tipo scelto, fonte/ambito e candidati separati foto/video. Il confronto delle copie pregresse conserva tutti i percorsi.
