# Revisione iPhone rc.13 — 16 novembre 2026

## Metodo e perimetro

Revisione del codice effettivo della rc.12, non una conferma dei resoconti precedenti. Tre revisori separati hanno esaminato protocollo Apple, catalogo/selezione e integrità/ripresa; hanno poi riesaminato reciprocamente le correzioni. Il coordinatore ha controllato integrazione, messaggi, regressioni e confezionamento. Due cicli sostanziali per area, entro il limite di tre. Preferenze stilistiche e ipotesi non riprodotte non sono state trasformate in correzioni speculative.

Nessun telefono reale, archivio personale, report della cliente o abbinamento reale consultato. I test usano byte, credenziali e database sintetici in cartelle isolate. Nessuna installazione di driver/servizi, API a consumo o dipendenza aggiuntiva. Le prove Oppo riferite dall'utente non sono attribuite al percorso Apple.

## Difetti confermati

### 1. P1: successo della selezione ridotta dopo una scomparsa fra tentativi

Percorso: `ImportSession.Run`, `ImportEngine.Run/AddOutstanding`. Primo inventario con A e B: A fallisce e B viene copiato; al tentativo successivo il catalogo presenta soltanto B. In rc.12 A diventava storico e il motore poteva dichiarare verificate le copie della selezione ridotta. Anche un elemento già copiato poteva scomparire senza segnalare il cambiamento della selezione tra tentativi.

Correzione: l'unione delle osservazioni della galleria rimane nella singola sessione automatica. Le assenze generano un errore d'inventario, il lavoro irrisolto resta corrente e non si dichiara successo. Sono incluse le osservazioni apparse soltanto nel controllo finale. Un nuovo avvio esplicito può lavorare su una selezione cambiata, mantenendo separato lo storico. Nessuna nuova chiave di identità serializzata.

Riproduzione: due nuovi casi fallivano prima della modifica. Verificati anche inventario successivo vuoto, osservazione soltanto finale, ritorno al terzo tentativo e nuova ImportSession indipendente. Il riutilizzo delle copie precedenti resta subordinato alla verifica dei byte.

### 2. P2: risposta AFC malformata lasciava utilizzabile la sessione

Percorso: `AppleAfcFileSystem.Status`. Un payload STATUS di lunghezza diversa da otto byte generava un errore ma non invalidava la connessione; l'inventario poteva continuare dopo una violazione del protocollo.

Correzione: il payload malformato chiude e invalida la sessione. Uno status ben formato di file mancante o accesso negato resta un errore della risorsa, senza essere confuso con corruzione del protocollo. Test precedente fallito; dopo, cinque casi AFC superati, comprese nove combinazioni di operazioni e lunghezze malformate. Nessuna nuova operazione di scrittura sul telefono.

### 3. P2: journal PERSIST inattivo bloccava un catalogo sano

Percorso: `AppleCatalogCapture.Capture`. SQLite può concludere una transazione PERSIST azzerando l'intestazione del journal e lasciando il file non vuoto. Rc.12 trattava ogni journal non vuoto come attivo. Difetto riprodotto con un database prodotto da SQLite, senza confezionare a mano il caso positivo.

Correzione: accettata soltanto l'intestazione completa di 28 byte tutti zero, ricontrollata prima/dopo ciascuna lettura del database e WAL; metadati del journal confrontati anche tra le acquisizioni. File non regolari, intestazioni brevi, nonzero, attive o mutevoli restano rifiutati. Nessun rollback e nessuna scrittura sul telefono. Aggiunta una prova WAL nativa con un commit valido seguito da una coda non conclusa: solo il commit deve essere applicato.

### 4. P2: visibilità privata ignorata nella selezione

Percorso: `AppleCatalogDecoder`, `AppleGallerySource`, raccordo `IGallerySource.SelectionUncertainty`. Le righe non nascoste e non eliminate erano considerate automaticamente selezionabili, ignorando `ZVISIBILITYSTATE`. Le fonti mostrano che possono esistere righe non visualizzabili, ad esempio membri di raffiche.

Correzione conservativa: quando il campo è presente, solo zero ammette il percorso normale; valori nonzero, null o nuovi conservano tipo, appartenenza e conteggio ma restano incerti. Nessuno stat o apertura dell'originale. Il motore registra Ambiguous con motivo esplicito, non una falsa indicazione di originale mancante/iCloud. Foto escluse dal filtro Solo video non bloccano la selezione. Se il campo manca, il profilo precedente resta supportato con avviso esplicito di visibilità non verificabile.

Non viene assegnato un significato universale ai valori privati e nessuna riga viene eliminata silenziosamente. Test su 0, 2, 999, null, campo assente, filtri, album, riconnessione e variazioni nell'inventario finale. La riproduzione sulle DLL rc.12 falliva nei casi interessati prima della correzione.

## Verifiche del protocollo senza modifiche speculative

Confrontati con implementazioni primarie: framing e endian di usbmux/Lockdown, porta del servizio, operazioni AFC, modalità di apertura in sola lettura e credenziali della sessione. Non sono state copiate implementazioni soggette a licenze incompatibili.

I test TLS ora usano una foglia sintetica firmata dalla radice dell'abbinamento. Un certificato con firma SHA-1, riconosciuto tramite pin esatto, non viene confuso con un handshake SHA-1: il primo caso può usare l'handshake SHA-256 ammesso, il secondo viene rifiutato. Sei prove TLS passate; politica di produzione invariata, nessun salvataggio delle chiavi su disco e nessun abbassamento degli algoritmi ammessi.

## Prestazioni

Misurato il riordino dei tentativi prima di proporre cambiamenti: circa 153 ms per 5.000 chiamate e 300 ms per 10.000 su nomi sintetici distinti nel sistema di prova. Il percorso contiene una scansione ripetuta, ma questi risultati non giustificano una riscrittura in questa release. Nessuna velocità USB stimata da questo microbenchmark e nessuna verifica SHA-256 rimossa. Le prove estese della versione integrata sono registrate separatamente in TESTS.md.

## Copertura e limiti residui

| Area | Evidenza automatica / limite |
| --- | --- |
| Autorizzazione, servizio assente, telefono bloccato | Errori e timeout del trasporto; disponibilità reale di Apple Devices/servizio e pairing da verificare sul PC della cliente |
| Collegamento, frammentazione, risposte invalide, cancellazione | Stream sintetici sul parser di produzione, TLS e AFC; interruzioni/riconnessioni e recupero del catalogo |
| Album, Live Photo, tipi sconosciuti, cloud | Cataloghi sintetici e filtri sul motore effettivo; schema iOS e accesso agli originali reali non attestati |
| Conteggi e inventari cambiati | Osservazioni iniziali/finali e tra tentativi, elementi assenti e ritorni, selezioni nuove e storico distinto |
| Copie precedenti, duplicati, corruzione | Regressioni di catalogo/ledger, SHA-256, molteplicità, copie estranee e recuperi senza sovrascrittura |
| Disco, filesystem, arresti | Suite sintetica su spazio/permessi, percorsi ostili, link, crash nei punti critici e cataloghi interrotti |
| File grandi e librerie numerose | Prove estese eseguite sulla versione integrata; dimensioni e tempi effettivi in TESTS.md/test-results.txt |
| Interfaccia e distribuzione | Compilazione e smoke UI con dati sintetici, pacchetto autonomo e verifica del download pubblico; non un collaudo interattivo del telefono |

Il campo GalleryCoverage descrive l'inventario iniziale: testo del report e UI lo esplicitano. Variazioni successive sono errori/pendenti del controllo finale e non una nuova attestazione di copertura. Le chiavi di sessione rappresentano l'intera osservazione: un cambio di metadati o disponibilità può richiedere un nuovo avvio esplicito. Questo limite conservativo è dichiarato, non sostituito da confronti deboli.

Restano da confermare su hardware: servizio e abbinamento Apple, negoziazione TLS, lettura AFC di Photos.sqlite, schema iOS, corrispondenza agli album reali, disponibilità degli originali, trasferimento e ripresa. Originali solo cloud, raccolte intelligenti/condivise e formati non supportati non sono risolti da queste correzioni. Il limite catalogo+WAL di 512 MiB riguarda i dati acquisiti: controlli e copie in memoria richiedono ulteriore RAM. La lettura ripetuta non è uno snapshot atomico. `EntireLibraryVerified` e `AtomicSnapshotVerified` restano falsi.

Nessun difetto bloccante noto rimasto nei casi riprodotti; compatibilità reale non ancora dimostrata. Esiti numerici finali in [TESTS.md](TESTS.md), fonti in [SOURCES.md](SOURCES.md), verifica della release in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).
