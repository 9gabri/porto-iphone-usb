# Porto rc.18: diagnosi delle prestazioni e obiettivo 800 video in un'ora

Data: 17 novembre 2026. Stato: **diagnosi, nessuna modifica o nuova distribuzione dell'app**.

## Risposta all'obiettivo

Il trasferimento di circa 800 video per 29 GB entro un'ora **non è dimostrato sull'iPhone della cliente**. Esistono costi software misurabili e opportunità concrete, ma le sei ore osservate non sono attribuibili con certezza al cavo, al telefono o a una singola funzione di Porto senza misure di campo.

Le prove qui descritte usano DLL pubbliche rc.18 e contenuti sintetici. Non sono stati letti telefoni, credenziali, foto, video o archivi personali. Nessuna installazione, modifica alle protezioni, API a consumo o spesa. Sorgenti applicativi, versioni precedenti e distribuzione restano invariati. I nuovi strumenti e risultati sono conservati sotto `artifacts/perf-deep-*-20261117`; nessun benchmark va lanciato su una destinazione personale.

## Baseline, ambiente e riproducibilità

- Sorgente di riferimento: `b63992cb7232145ab562d693201a058f575985cc`; repository all'inizio `f310273`.
- DLL Core pubblica SHA-256: `EAA034EAB8D95794C1B360519BF27B804EFADBDBFE9D30247C26A08424C851F6`.
- DLL Apple pubblica SHA-256: `45FF7BBEF9899224F1CE1BAE41252B0F614A35E5C2C00BC86C0FD9F218277791`.
- .NET 10.0.14, Windows locale, 12 processori logici; il computer non è quello della cliente. Il dettaglio hardware CIM non era accessibile: non è stato aggirato il limite.
- Le prime prove di trasporto/catalogo/core sono state concorrenti: i tempi brevi CPU/memoria possono risentire della contesa. I byte e i conteggi verificati non dipendono da questa contesa. Non si assume cache fredda, assenza di antivirus o SSD equivalente a quello della cliente.
- Il test core usa sorgente RAM a blocchi massimi di 64 KiB, senza latenza USB, e spazio libero simulato. Il test catalogo usa un SQLite valido ingrandito con padding, non la complessità di una libreria reale. I file video sintetici esercitano contenitore e byte, non decodifica audiovisiva o riproduzione.

### Strumenti e risultati

| Ambito | Sorgente dello strumento | Risultati autorevoli |
|---|---|---|
| Trasporto AFC | `artifacts/perf-deep-transport-20261117/Program.cs` | `results-verified.jsonl`, `results.json`, `summary.json`, `findings.md` |
| Copia, confronto, ripresa | `artifacts/perf-deep-core-20261117/Program.cs` | JSON e rapporto nella stessa directory |
| Catalogo >563 MB | `artifacts/perf-deep-catalog-20261117/Program.cs` | `results.jsonl`, `summary.json`, `findings.md` |
| Pipeline locale, flush, file >4 GiB, transazioni | `artifacts/perf-deep-local-20261117/Program.cs` | `results.json` |
| Report 800 esiti e controllo spazio | `artifacts/perf-deep-extra-20261117/Program.cs` | `results.json` |
| Regressioni selezione e recupero | build esistente `artifacts/rc18-tests/Porto.Tests.exe` | `artifacts/pdr17/*-tests.txt` |

Compilazione offline con SDK e cache esistenti, per esempio:

```powershell
$env:DOTNET_ROOT = Join-Path (Get-Location) '.tools/dotnet-curl'
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.tools/cli'
& '.tools/dotnet-curl/dotnet.exe' build 'artifacts/perf-deep-local-20261117/PerfLocal.csproj' -c Release -m:1 --disable-build-servers -p:UseSharedCompilation=false --source '.tools/packages' -o 'artifacts/perf-deep-local-20261117/out'
```

Gli harness rifiutano di riutilizzare archivi dei benchmark già eseguiti. Per ripeterli creare nuovi percorsi isolati e adeguare il controllo esplicito del nome; non cancellare risultati o destinazioni precedenti. Gli eseguibili sono strumenti di prova, non candidati da inviare alla cliente.

## Budget di un'ora: byte e tempo, non solo numero di video

Assunzione dichiarata: **29 GB decimali = 29.000.000.000 byte**, circa 36,25 MB medi per 800 file. Se il numero fosse invece 29 GiB, il solo carico video crescerebbe del 7,4%. I conteggi 330/800 non consentono di conoscere la frazione dei byte già copiata: i video non hanno tutti la stessa durata o dimensione.

La sola copia di 29 GB in 3.600 s richiede **8,06 MB/s**, senza alcun inventario, confronto, verifica, errore o scrittura di stato. Non è una previsione del tempo reale.

Per il catalogo osservato nella schermata: DB 563.425.280 + WAL 4.227.152 = **567.652.432 byte**. Un'acquisizione verificata legge entrambi due volte. L'avvio e il controllo finale leggono quattro volte il totale; scegliere l'album aggiunge altre due letture.

| Percorso sano modellato | Byte fonte complessivi | MB/s minimi, 60 min tutti disponibili | MB/s minimi, 10 min riservati alle altre operazioni |
|---|---:|---:|---:|
| Una lettura dei video + catalogo iniziale/finale | 31,271 GB | 8,69 | 10,42 |
| Come sopra, includendo scelta album | 32,406 GB | 9,00 | **10,80** |
| Tutti i video preletti per confronto e poi copiati + scelta album | 61,406 GB | 17,06 | **20,47** |

Questi sono **requisiti medi ipotetici sul traffico della sorgente**, non prestazioni dell'iPhone. Il margine di dieci minuti è un budget di progetto da validare, non una durata misurata universale. Non sommare due volte lavoro che si sovrappone (hash eseguito durante copia, I/O bufferizzato). Per un modello conservativo si contabilizzano esplicitamente byte extra e fasi seriali; ogni errore può aggiungere altre letture e attese.

## Cause e costi confermati, ordinati per potenziale impatto

### 1. Confronto seguito da seconda lettura del nuovo video

**Confermato e riprodotto.** `src/Porto.Core/ImportEngine.cs:215–218` prelegge il contenuto quando rimangono copie precedenti disponibili per confronto. Se il nuovo video non corrisponde, `:300–309` lo legge di nuovo per copiarlo. Succede anche con verifica approfondita disattivata e con una copia precedente di dimensione diversa.

La prima riproduzione su DLL immutata: tre video, 37.748.988 byte. Destinazione vuota: 3 aperture e 37.748.988 byte fonte. Un vecchio video estraneo: 6 aperture e 75.497.976 byte. Tre copie nuove verificate in entrambi i casi; file precedente invariato. I test a 800 elementi verificano nuovamente il comportamento su scala maggiore.

Questo dimostra un **raddoppio del traffico fonte in quel caso**, non un raddoppio del tempo totale. Con sorgente RAM il costo locale domina: la prima prova con copia estranea ha impiegato circa 113 s, quella vuota circa 121 s; la differenza non dimostra un miglioramento. Sul telefono dipende da quanto costano effettivamente quelle letture aggiuntive.

La ripresa non equivale a leggere soltanto i video mancanti: le copie già presenti sono ricontrollate localmente e confrontate con la sorgente. Non è corretto promettere che il tempo residuo sia proporzionale ai soli file mancanti. Una nuova cartella eviterebbe il confronto ma perderebbe il riuso delle copie: non è proposta come soluzione.

### 2. Una risposta attesa per ogni blocco da 64 KiB

**Meccanismo confermato; impatto sull'iPhone da misurare.** `AppleAfcFileSystem.cs:16,81,117` limita a 64 KiB e serializza ogni scambio. Circa 442.505 richieste di dati sono necessarie per 29 GB, esclusi EOF, metadati e riletture. Il socket ha già `NoDelay=true` (`AppleUsbTransport.cs:90`), quindi aggiungerlo non sarebbe una correzione.

33 benchmark sintetici finali, dopo warmup, tre repliche per condizione; SHA-256 identico in tutte le copie complete:

| 16 MiB, stesso payload | 64 KiB | 256 KiB | 512 KiB |
|---|---:|---:|---:|
| READ incluso EOF | 257 | 65 | 33 |
| Mediana senza attesa | 17,21 ms | 26,51 ms | 21,60 ms |
| Mediana con attesa artificiale | 4.001 ms | 1.007 ms | 506 ms |

L'attesa richiesta era `Sleep(1)`; quella effettiva, misurata, circa 14–15 ms per richiesta per effetto dello scheduler. **Non è la latenza dell'iPhone.** I blocchi maggiori riducono le attese nel simulatore, ma senza attesa risultano più lenti: non emerge un vantaggio generale che si possa trasferire automaticamente all'app.

Il prototipo isolato che combina header e payload dimezza le write del comando (2.054→1.027 su 64 MiB), mantenendo uguali i byte. Tempi mediani 102,13→101,19 ms con intervalli sovrapposti: **nessun guadagno dimostrato**, prototipo non promosso.

La sorgente primaria [libimobiledevice, afc.c](https://raw.githubusercontent.com/libimobiledevice/libimobiledevice/master/src/afc.c), funzioni `afc_file_read`, `afc_dispatch_packet`, `afc_receive_data`, inoltra la lunghezza richiesta senza un limite interno 64 KiB in quella funzione e invia header/readinfo insieme. Ciò distingue una scelta di Porto da un limite AFC dimostrato; non certifica blocchi maggiorati sull'iPhone della cliente. Il limite Porto di 1 MiB comprende l'header di 40 byte: un payload pieno da 1 MiB sarebbe rifiutato.

Con 256/512 KiB sono passati 12 controlli negativi: troncamento, disconnessione, sequenza errata, risposta eccessiva, EOF anticipato, annullamento. Annullamento dopo 50 ms osservato dopo circa 69/53 ms. Nessuna protezione è stata rimossa. La simulazione non misura TLS reale, servizio Apple o USB fisica.

### 3. Costo per file e persistenza locale

**Confermato sul computer di prova, non attribuito alla cliente.** A parità di 25 MiB, la prima misura ha richiesto circa 121 s per 800 file da 32 KiB e 2,28 s per otto file da 3,125 MiB. La sorgente è in RAM, senza latenza USB: il numero dei file e i salvataggi/verifiche locali hanno un peso autonomo.

Per la prima prova da 800 file: circa 47,45 s CPU, 26,21 MB finali e 211,08 MB di write registrate per il processo. Quest'ultimo contatore comprende stato SQLite/report/altri I/O del processo e non è la quantità fisica scritta sulla memoria flash.

I confini `FaultPoint` mostrano intervalli aggregati: copia+flush circa 8,05 s; hash locale+ledger circa 35,61 s; ispezione+Ready circa 11,70 s; pubblicazione circa 17,19 s; Complete+riconciliazione circa 12,44 s. **Non sono tempi isolati di hash, query o syscall**. Il residuo comprende inventari, registrazione esiti e altri passaggi. La sezione risultati finali registra le repliche e misure locali aggiuntive.

`Sqlite.cs:40–42` usa journal DELETE e synchronous FULL; `Catalog.Save/AppendResult` effettuano transazioni durevoli; `ImportEngine.cs:297,312,336,338,406` conserva intent, flush, Ready, pubblicazione, Complete ed esito. `Destination.Publish` usa spostamento senza sovrascrittura e WRITE_THROUGH. Queste operazioni sostengono il recupero dopo arresto: eliminarle per ottenere un numero migliore cambierebbe le garanzie.

`RetryOrder.AfterFailure` scandisce la lista anche per ogni successo; vi sono costi quadratici di ordinamento su liste grandi. Sono da profilare, ma nessuna prova li identifica da soli come causa delle sei ore. Preparazione SQL ripetuta, serializzazioni e controlli filesystem restano candidati da distinguere con strumenti più fini.

### 4. Traffico del catalogo e costo delle riconnessioni

**Byte confermati; durata USB sconosciuta.** `AppleGallerySource.cs:148–151` effettua due letture concordanti per cattura; inventario iniziale/finale in `ImportEngine.cs:51,364`; scelta album in `AppleUsbTransport.cs:26–29`. Totale osservato: circa 2,271 GB per passaggio, 3,406 GB includendo scelta album.

Il recupero in `AppleGallerySource.cs:92–101` può riaprire la sessione e acquisire nuovamente il catalogo. Non avviene per ogni file sano. Il numero effettivo di recuperi della cliente è sconosciuto. `ImportSession` attende 15 s tra tentativi; ulteriori timeout e acquisizioni possono pesare molto. Non esiste una pausa artificiale equivalente nel normale ciclo di lettura sano.

Tre acquisizioni reali della DLL con sorgente SQLite sintetica: DB 566.362.112 byte, WAL 4.227.152; tempi 1.305/1.091/928 ms. Ciascuna legge 1.141.178.528 byte, con quattro aperture e dodici stat. Conteggi e selezione corretti; hash sorgente invariati.

| Fase isolata del catalogo | Mediana |
|---|---:|
| Prima lettura e metadati | 256 ms |
| Seconda lettura e confronto | 163 ms |
| Applicazione WAL | 2,66 ms |
| Decoder, copia nativa, integrity check, query e rilascio | 497 ms |
| Azzeramento buffer catturati | 195 ms |

Le misure isolate non vanno sommate al tempo della cattura completa. Allocazioni managed circa 571,7 MB; picco catture complete circa 1,76 GB, **inclusa sorgente sintetica residente in RAM**. Picco diagnostico 2,33 GB contiene referenze extra del test separato, non rappresenta l'app ordinaria. Il DB ha padding: la complessità delle query reali resta da misurare.

Quando `db` e `merged` sono la stessa referenza, il cleanup li azzera due volte (`AppleGallerySource.cs:157`): opportunità minore da circa frazioni di secondo, non spiegazione delle ore. Una cache basata solo su dimensione, mtime o durata prestabilita non garantisce coerenza.

### 5. La velocità visualizzata non isola il trasporto

**Confermato.** Il timer parte prima dell'inventario (`ImportEngine.cs:44–46`); `:549–550` divide byte di copia per tutto il tempo trascorso. Il denominatore include catalogo, confronto e verifiche, il numeratore non include tutto il traffico fonte. Durante riuso di copie può rimanere a zero pur leggendo molti dati.

Il valore 0,6 MiB/s della prova da due video non consente di stimare il massimo del collegamento. Il report registra inizio/fine, esiti e riavvii ma non le durate di ciascuna fase o il traffico completo. Anche ricevere il report non basterebbe necessariamente a identificare il collo di bottiglia fisico.

## Risultati finali delle prove locali

### Pipeline locale: separare copia, flush e verifica

Sei copie indipendenti da 64 MiB, sorgente generata senza telefono, SHA-256 ricevuto/salvato identico e dimensione finale esatta. Tre repliche per dimensione di lettura, mantenendo uguali byte, controlli e destinazione NTFS. Il blocco da 1 MiB riguarda **solo questo stream locale**, non è una proposta di payload AFC da 1 MiB.

| Fase, mediana di tre esecuzioni | Lettura 64 KiB | Lettura 1 MiB |
|---|---:|---:|
| Copia, generazione sorgente e hash durante la copia | 59,16 ms | 51,18 ms |
| Sole chiamate Write bufferizzate, già incluse nella copia | 17,74 ms | 15,61 ms |
| Flush(true), dopo la copia | 48,26 ms | 45,07 ms |
| Rilettura locale + SHA-256 | 66,33 ms | 81,87 ms |
| Totale copia + flush + verifica, mediana | 173,75 ms | 178,12 ms |
| Intervallo dei tre totali | 164,62–184,53 ms | 159,10–186,02 ms |

La differenza totale non supera la variabilità: **nessun miglioramento dimostrato** da questo esperimento locale. Le Write sono bufferizzate e il loro tempo non equivale al tempo fisico del disco. La verifica misura insieme rilettura e hash: cache Windows e CPU non sono separabili dal solo cronometro. Non sommare le Write al tempo copia, che già le contiene. La generazione della sorgente richiede 1,1–7,7 ms, compresi nel tempo copia; CPU processo per prova 93,75–140,63 ms.

### File oltre 4 GiB nel motore completo

Un file sintetico di **4.294.971.392 byte (4 GiB + 4 KiB)** è stato importato con il vero ImportEngine, destinazione NTFS e controllo spazio reale: **46,94 s**, un'apertura fonte, 65.538 Read compreso EOF, due inventari, una nuova copia verificata, dimensione finale esatta. CPU processo 16,19 s. Nessuna conversione; file e report preservati.

Il tratto tra intent persistito e flush completato occupa 42,55 s; quello tra flush e verifica registrata 3,53 s. Comprendono più operazioni e non vanno presentati come soli tempi USB o hash. La fixture genera il payload senza tenere 4 GiB in RAM; picco working set cumulativo dell'intero harness locale circa 68,4 MB. Questa è **una prova funzionale grande, non un benchmark ripetuto dell'iPhone**: nessuna estrapolazione a 29 GB reali o video riproducibili. Il confronto con la pipeline breve non modifica una sola variabile, quindi non misura un miglioramento.

### Aggiornamenti durevoli isolati

Tre archivi sintetici nuovi, 800 righe ciascuno, nessun video trasferito:

| Operazione | Mediana | Minimo–massimo |
|---|---:|---:|
| WorkLedger.Observe, 800 elementi | 84 ms | 42–95 ms |
| 800 Catalog.Save, transazioni durevoli | 20,67 s | 9,75–23,34 s |
| 800 Catalog.AppendResult, transazioni durevoli | 13,86 s | 13,85–21,59 s |

Questa misura conferma un costo locale sostanziale e una variabilità alta. Non isola il solo flush di SQLite dalle validazioni del percorso e dalle altre attività del metodo; non equivale a una misurazione del PC della cliente. I tempi **non si aggiungono** a quelli del motore completo, che contiene già queste operazioni. L'harness locale registra anche 78–84 ms per un report senza righe: è un controllo di base, non il tempo del report di 800 video.

### Matrice del motore e controlli di regressione

**28 esecuzioni concluse con gli esiti attesi**, inclusi gli avvii preparatori. I casi di guasto devono risultare incompleti: il loro esito atteso non è un'importazione riuscita. Il sorgente delle fixture, i risultati e `summary.json` sono in `artifacts/perf-deep-core-20261117`.

| Scenario | Repliche | Tempo: mediana [min–max] | Byte fonte / aperture | Nuove / già verificate |
|---|---:|---:|---:|---:|
| Destinazione vuota, 800 × 32 KiB | 3 | 115,51 s [105,41–121,27] | 26.214.400 / 800 | 800 / 0 |
| Destinazione vuota, 8 × 3,125 MiB, stessi byte totali | 3 | 2,28 s [2,11–3,86] | 26.214.400 / 8 | 8 / 0 |
| Una copia estranea precedente, poi 800 elementi | 1 | 113,22 s | 52.428.800 / 1.600 | 800 / 0 |
| Stesso caso estraneo, scala 32 elementi | 2 | 6,66 s [4,53–8,79] | 2.097.152 / 64 | 32 / 0 |
| Selezione prima di 330, poi ampliata a 800 | 1 | 103,95 s per la ripresa | 26.214.400 / 800 | 470 / 330 |
| Inventario iniziale di 800, annullamento dopo 330 copie | 1 | 42,99 s | 10.813.440 / 330 | 330 / 0, altri 470 pendenti |
| Ripresa di quell'inventario interrotto di 800 | 1 | 113,51 s | 26.214.400 / 800 | 470 / 330 |
| Selezione prima di 13, poi di 32, controllo ridotto | 2 | 4,96 s [4,47–5,45] | 1.048.576 / 32 | 19 / 13 |

Le repliche ridotte confermano i conteggi di lettura; non sono repliche temporali della prova a 800 elementi. La separazione 800 piccoli/8 grandi supera nettamente gli intervalli osservati, ma **non rappresenta un'ottimizzazione applicabile a 800 video distinti**: misura il peso del numero di file, a parità di byte. L'ordine delle repliche non è randomizzato, il sistema non è in isolamento assoluto e non si presume cache fredda. Non estrapolare 25 MiB a 29 GB mantenendo costante il tempo per file.

Nelle tre prove vuote da 800: CPU processo 44,23–47,45 s, allocazioni managed cumulative 869,8–872,1 MB, working set campionato 135,2–141,7 MB. Le allocazioni cumulative non sono memoria residente. In ciascuna: 1.600 Read compreso EOF, due inventari; scritture processo circa 211,1 MB per 26,2 MB di file finali. La prova di ripresa completa legge tutti gli 800 elementi ma aggiunge soltanto **15.400.960 byte finali** per 470 nuovi file; hash dei 330 precedenti invariati. Le sorgenti ordinate della fixture esauriscono prima la coda delle copie corrispondenti; un ordine diverso con copie estranee ancora disponibili può comportare altre preletture.

Altri otto casi funzionali, una esecuzione ciascuno:

| Caso | Evidenza |
|---|---|
| Annullamento durante file da 8 MiB | 64 KiB ricevuti, temporaneo conservato, zero file pubblicati, un pendente |
| Ripresa dello stesso parziale | Tutti gli 8 MiB riletti dalla fonte; solo il suffisso mancante aggiunto; una copia verificata |
| Quattro elementi con nomi uguali e tre contenuti identici ma distinti | Quattro copie fisiche, molteplicità degli hash preservata |
| Ripetizione dei quattro | Zero copie nuove, quattro già presenti verificate; hash precedenti invariati |
| Errore transitorio dopo 16 KiB nel primo di tre file | Gli altri due copiati; errore mantenuto; nessun falso completamento |
| Nuovo tentativo dopo quell'errore | Una nuova copia e due già verificate; 131.072 byte fonte per 98.304 byte selezionati, perché il file non corrispondente viene preletto e poi ripreso |
| Disconnessione simulata dopo 16 KiB | Zero pubblicati, un errore e due pendenti; selezione incompleta |
| Riconnessione simulata con stessa selezione | Tre copie verificate, il parziale recuperato |

Il retry dei casi core è un nuovo Run esplicito; non misura il servizio Apple né la pausa automatica di 15 s. Le prove Apple specifiche di recupero, elencate sotto, esercitano separatamente la ricreazione della sorgente e la validazione della selezione.

Il primo processo di benchmark è stato interrotto intenzionalmente **dopo sei risultati completi**, per separare le scritture su disco dagli esperimenti del root. La cartella `r2-small` parziale è conservata ed esclusa dalle statistiche; i successivi tentativi hanno nomi nuovi. Non è un crash di Porto durante una prova riuscita.

Controlli mirati della build rc.18 esistente: **34 test distinti passati** (12 `gallery import`, 2 `apple source recovery`, 2 `apple final recovery`, 9 `apple streaming`, 9 `apple rc13`). Coprono elementi mancanti fra tentativi, letture discordanti, cataloghi cambiati/troncati, visibilità incerta, recupero e cancellazione. Due test rc13 inizialmente non raggiungevano il caso da provare perché il percorso del test superava il limite prodotto: rieseguiti in una nuova cartella più breve, entrambi passati. Log originali conservati. Sono verifiche mirate, non una dichiarazione di esecuzione dell'intera suite.

A questi si aggiungono i 12 controlli negativi del trasporto maggiorato e gli esiti/assert dei benchmark. Non sommare benchmark, repliche e controlli come fossero tutti test indipendenti di compatibilità iPhone.

### Report completo e controllo spazio per blocco

Tre report sintetici da **800 esiti**: mediana 54,04 ms, intervallo 47,16–313,50 ms. Ogni volta 283.922 byte JSON e 80.746 byte TXT, conteggio JSON riletto e verificato a 800. Questo campione ha nomi e dettagli sintetici brevi; il primo passaggio è più lento, e tre prove non dimostrano una latenza costante. Non emerge un costo del report paragonabile alle ore osservate.

`src/Porto.Core/VerifiedCopy.cs:48–60` richiama il controllo spazio ad ogni iterazione di copia; `ImportEngine.cs:539–541` interroga `LocalDiskSpace` (`Models.cs:108–110`). Con letture da 64 KiB questo moltiplica le interrogazioni a Windows. Tre prove di 1.000 interrogazioni reali, senza scrivere payload: **47,77–51,22 ms**, mediana 50,21 ms; controllo con provider costante 0,012–0,041 ms. Il costo è confermato, ma piccolo su questo host. Applicare linearmente la mediana a 442.505 richieste darebbe circa 22 s: è solo un ordine di grandezza ipotetico, non un tempo iPhone misurato. Non spiega le sei ore né tutto il costo del file >4 GiB. Il provider costante è esclusivamente un controllo diagnostico, non una proposta di rimozione del controllo disco pieno.

## Ipotesi aperte e completezza delle misure

| Questione | Classificazione ed evidenza mancante |
|---|---|
| Cavo/porta/servizio Apple o dispositivo limitano la velocità | **Limite esterno possibile, non dimostrato**: manca velocità negoziata e distribuzione dei tempi delle risposte reali |
| Letture doppie spiegano la maggior parte delle sei ore | **Ipotesi**: il meccanismo è riprodotto, ma mancano byte per ruolo e stato delle copie durante quella sessione |
| Recuperi/timeout hanno ripetuto molte volte il catalogo | **Ipotesi**: servono conteggi dei tentativi e durata delle riacquisizioni reali; i report attuali contengono solo parte di queste informazioni |
| Disco/antivirus/contesa o memoria del PC della cliente | **Ipotesi**: il costo locale esiste sul PC di sviluppo, ma non è misurato su quello della cliente; nessuna protezione da disabilitare |
| Catalogo con molte righe/join rallenta molto il decoder | **Ipotesi**: il catalogo sintetico è grande soprattutto per padding, non riproduce distribuzione e complessità di quello reale |
| Hashing è il limite principale | **Non dimostrato**: la verifica locale rapida in questi campioni non caratterizza CPU/cache/disco della cliente |

Le misure raccolte distinguono wall time, CPU, memoria, byte fonte e byte finali, I/O aggregato del processo, READ/aperture e intervalli di verifica/persistenza. Il microbenchmark catalogo separa acquisizione, confronto, WAL e decode; quello locale separa copy, flush e read+hash; il trasporto misura l'attesa **iniettata** e le richieste. **Non sono stati misurati pacchetti fisici USB, TLS reale, tempi di risposta del telefono, attività del driver o ogni query SQL individuale.** Aperture del mock non equivalgono a connessioni fisiche. Le fasi del motore delimitate da FaultPoint sono aggregate; la risoluzione dei progressi è 200 ms. Non tutti i tempi possono essere sommati e non esiste ancora un profilo completo dei 29 GB reali.

Tre revisori hanno completato le analisi indipendenti iniziali di trasporto, core e catalogo; una revisione indipendente del documento intermedio non ha rilevato errori sostanziali. Il limite di quota ha impedito ulteriori follow-up degli agenti: nessun acquisto o fallback a pagamento. Il root ha raccolto i processi già terminati, completato i microbenchmark aggiuntivi e verificato i risultati e il documento. Non è stata svolta un'ulteriore revisione indipendente dell'intero documento finale.

## Piano di ottimizzazione, benefici e protezioni

| Priorità | Intervento proposto | Beneficio verificabile/atteso | Rischi e prove obbligatorie |
|---|---|---|---|
| 0 | Misure aggregate per fase, ruolo dei byte, READ, attese, errori, sessioni, hash/flush/database | Attribuire il costo reale e validare un confronto prima/dopo | Nessun nome personale, contenuto o credenziale nei nuovi contatori; overhead misurato; clock monotono e fasi senza doppio conteggio |
| 1 | Esaminare una sola lettura con staging temporaneo e confronto SHA completo dei nuovi file | Nel caso confermato può evitare la seconda lettura dei video non corrispondenti | Le copie già presenti possono invece causare scritture temporanee extra. Non saltare in base a nome/ID/dimensione. Preservare molteplicità, parziali, fonti discordanti, deep verification, disco pieno, file estranei e tutti i punti di crash |
| 2 | Sperimentare 256 KiB, poi 512 KiB per AFC, con soglie/framing coerenti | Meno scambi per stessi byte; beneficio solo se le attese sono rilevanti | Test hardware necessario; timeout/cancellazione, short reads, memoria, EOF e poisoning. Eventuale fallback esplicito su sessione nuova, mai continuare una sessione fuori framing |
| 3 | Profilare e ridurre operazioni locali ripetute; preparazione statement e raggruppamenti transazionali limitati | Ridurre costo per file e scritture di stato | Non disabilitare FULL, flush o controlli per ottenere velocità. Verificare arresti in ogni confine intent/Ready/Complete, migrazione e recupero vecchie versioni |
| 4 | Migliorare il trasporto del catalogo mantenendo confronti e controllo finale | Ridurre il tempo necessario ai circa 2,27–3,41 GB di metadati | Non usare una cache stale come prova di completezza. Eventuale riuso decodifica soltanto su immagine byte-identica e stesso dispositivo/ambito |
| 5 | Ridurre copie/allocazioni e azzeramenti duplicati | Guadagni locali più piccoli; pressione memoria inferiore | Ownership, azzeramento effettivo di ogni buffer, cancellazione, nessun dato personale persistito |

Il prototipo di write unificata rimane un esperimento: il test eseguito non ha mostrato un guadagno oltre la variabilità. Non è una correzione approvata o distribuita. In questo ciclo non è stata implementata la strategia di staging nel motore: i rischi di ripresa e deduplicazione richiedono una modifica separata e revisionata.

Non moltiplicare il raddoppio dei byte del caso di confronto per il guadagno del simulatore AFC per promettere un'accelerazione complessiva: sono scenari distinti, con costi diversi e nessuna misura congiunta sul dispositivo reale.

## Prova reale necessaria e criterio di riuscita

Prima di affermare il raggiungimento dell'ora servono, con autorizzazione specifica, misure sullo stesso iPhone/PC/cavo/destinazione: GB e conteggi locali selezionati, tempi per fase, byte fonte per ruolo, distribuzione della durata dei READ, errori/riavvii, scritture e CPU/disco. Confrontare più esecuzioni equivalenti per stato archivio e cache. Usare lo stesso album senza modifiche durante la prova; non eliminare copie per creare artificialmente un benchmark più favorevole.

Tutti gli 800 video devono essere accessibili localmente, selezionabili e verificabili. Originali soltanto su iCloud, file mancanti, visibilità incerta, album non risolti o selezioni mutate impediscono di certificare l'obiettivo indipendentemente dalla velocità. Ogni elemento deve rimanere contato, con esito visibile.

Il successo richiede: tempo totale <=3.600 s nel confine dichiarato; tutti i video selezionati copiati o già presenti verificati; zero pendenti, errori o ambiguità rilevanti; hash e byte coerenti; inventario finale valido; nessuna perdita, sovrascrittura o modifica della sorgente. Questi criteri non sono soddisfatti dalle sole simulazioni e non è stato dichiarato che lo siano.

**Valutazione:** obiettivo tecnicamente plausibile solo con throughput sostenuto e margine sufficienti, ma fattibilità sull'hardware della cliente ancora aperta. Non esiste evidenza per promettere il passaggio da 15 ore a 50 minuti. La diagnosi giustifica una fase di ottimizzazione mirata e una misura reale successiva; non giustifica attribuire il limite all'USB senza prova né dichiarare l'app definitiva.
