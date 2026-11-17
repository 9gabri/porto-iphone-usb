# Porto 0.1.0-rc.19 — prestazioni con verifiche conservate

17 novembre 2026. Candidata per una nuova prova iPhone. Nessun telefono, record di abbinamento o file personale delle prove è stato letto o modificato in questo ciclo. Nessuna installazione di servizi/driver, modifica alle protezioni o spesa. Non è una certificazione né una promessa di 800 video/29 GB entro un'ora.

## Ambito

Baseline pubblica rc.18, commit `b63992cb7232145ab562d693201a058f575985cc`. La diagnosi precedente è conservata in PERFORMANCE-RC18-20261117.md. Le sue tre revisioni indipendenti hanno contribuito a scegliere gli interventi. I tentativi di revisione delegata di questa implementazione hanno incontrato il limite di quota; non è stato usato alcun fallback a pagamento. Il diff finale è stato riesaminato localmente con regressioni e controesempi: non va presentato come approvato da tre revisori indipendenti.

## Modifiche

1. `ImportEngine` / `ComparisonBuffer`: in una galleria con copie precedenti, la prima lettura completa usata per il confronto viene conservata in un nuovo temporaneo locale. Se non coincide con una copia riutilizzabile, la copia finale viene alimentata da quel temporaneo. SHA-256 della prima lettura, hash della copia e rilettura locale devono concordare; la verifica strutturale resta. La precedente rilettura USB non era una verifica indipendente obbligatoria: avveniva anche solo per ottenere nuovamente i byte già letti per il confronto. La modalità approfondita continua a imporre una seconda lettura realmente indipendente dalla sorgente.
2. `AppleAfcFileSystem`: limite delle richieste da 64 a 256 KiB, mantenendo scambi seriali, controllo delle lunghezze, numero di sequenza, EOF, timeout, cancellazione e invalidazione delle sessioni malformate. Nessuna modifica TLS, credenziali o operazione di scrittura AFC. L'opzione UI `USB iPhone compatibile` ripristina 64 KiB in una nuova sessione; non c'è ripiego silenzioso su una connessione guasta. Si applica a catalogo e originali.
3. `ImportPerformance` / report: misure aggregate locali, senza nomi, percorsi, identificativi del telefono o contenuti nel nuovo file prestazioni. UI: velocità denominata “Media complessiva”, non velocità fisica USB.

Il temporaneo di confronto usa `CreateNew`, un nome casuale e `DeleteOnClose`: il processo non cancella un percorso preesistente per nome. Viene eliminato alla chiusura anche in caso di arresto del processo. Non è una copia completata o un checkpoint durevole. Il motore mantiene il file pendente fino alla verifica e pubblicazione normali. Per predisporlo servono dimensione nota positiva, spazio per due file e riserva di 64 MiB; con lunghezza ignota o spazio ridotto resta il vecchio percorso. Lo spazio viene ricontrollato durante la lettura. Il costo è una scrittura/lettura locale in più e, temporaneamente, lo spazio di un file aggiuntivo.

Nella ripresa le copie precedenti vengono ancora confrontate per contenuto completo: questa candidata non elimina le letture necessarie a confermare che i byte siano gli stessi. I video distinti con byte uguali conservano molteplicità. Un file corrotto, mancante o estraneo non viene sovrascritto o cancellato. Catalogo di stato, transazioni FULL, flush durevoli, controllo iniziale/finale della selezione e report di incertezza restano invariati. I cataloghi Foto ripetuti non sono stati sostituiti da una cache non verificata. Il trasferimento USB generico e l'importazione da cartella mantengono il precedente comportamento di confronto.

## Misure della candidata

Il benchmark riproducibile è in `tests/Porto.Performance`, risultati locali in `artifacts/rc19-verified/perf-rc19`. Usa lo stesso harness contro la DLL pubblica rc.18 e la DLL candidata, verifica gli SHA-256 effettivamente caricati e i file prodotti. Configurazione: Windows di sviluppo, .NET 10.0.14, NTFS, tre file sintetici da 8 MiB, copia preesistente non corrispondente da 32 KiB, tre esecuzioni per variante. Latenza **artificiale** di 5 ms per blocco sorgente da 64 KiB. Non misura USB, telefono o catalogo Foto reali. Cambia solo la DLL Core; l'aumento dei blocchi AFC non è incluso in questo risultato.

I primi output in `artifacts/perf-rc19` sono conservati ma esclusi: la cache incrementale di compilazione aveva copiato rc.18 in entrambi gli output. Il controllo del digest lo ha rilevato. Le misure valide usano due output nuovi con digest differenti verificati prima dell'esecuzione.

| Misura | rc.18 | candidata rc.19 |
|---|---:|---:|
| Byte sorgente per 24 MiB di nuovi file | 50.331.648 | 25.165.824 |
| Aperture / Read (EOF inclusi) | 6 / 774 | 3 / 387 |
| Tempi delle tre prove, ms | 4.468,26 / 4.443,84 / 4.486,08 | 2.776,53 / 2.942,94 / 2.840,10 |
| Mediana, ms | 4.468,26 | 2.840,10 |
| Attesa artificiale effettiva, ms | 3.845–3.851 | 1.921–1.942 |

Mediana ridotta del 36,4%, con intervalli delle tre esecuzioni separati. L'attesa si dimezza; i costi del nuovo temporaneo locale restano. Nessuna somma automatica con il beneficio dei blocchi AFC. SHA-256 Core caricate: rc.18 `EAA034EAB8D95794C1B360519BF27B804EFADBDBFE9D30247C26A08424C851F6`, candidata pre-tag `82BE5F6E7D43084837138349F8B3285F1EFE093670D0A7ACB3BA5FC535581147`. Il successivo build di release incorpora il commit del tag e ha quindi un digest diverso. Tutti i nuovi contenuti e la copia precedente sono stati verificati per hash.

Le richieste AFC da 256 KiB sono coperte da test di framing, lettura completa/EOF, hash e rifiuto di risposte eccessive. Su 256 KiB + 17 byte, i payload richiedono due richieste anziché cinque, più EOF. Le misure con latenze simulate della diagnosi mostrano un beneficio nei casi dominati dalle attese; non sono misure del telefono e non dimostrano una velocità quadrupla end-to-end.

## Verifiche e controesempi

- Suite ordinaria: 545/553 al primo giro. Otto preparazioni di fixture NTFS (hard link/junction) bloccate dal sandbox; gli stessi otto casi sono poi passati in un ambiente che ne consente la creazione, sempre in cartelle sintetiche isolate. Non sono stati ignorati o allentati i controlli del prodotto.
- Nuovi casi rc.19: 16/16 dopo le ultime modifiche ai contatori. Copie non corrispondenti, riuso, molteplicità e nomi uguali, memoria/letture, lunghezze ignote, troncamento, disco pieno, letture approfondite discordanti, cancellazione, arresto reale del processo in cinque punti e recupero con vecchi byte invariati; tre casi AFC.
- Il test esteso di replay scrive e verifica davvero 4 GiB + 4096 byte con una sola apertura della sorgente. Il precedente file resta identico e il temporaneo scompare. Working set osservato a fine prova circa 60 MiB: il video non è caricato integralmente in RAM. Durata locale 220,21 s; non è una misura USB. Il payload sintetico finale viene conservato nella nuova cartella di prova. Anche il percorso ordinario oltre 4 GiB passa (175,27 s sul PC di sviluppo).
- Catalogo SQLite sintetico da 566.362.112 byte e WAL da 4.227.152 byte: cattura completa, rilettura, integrità e hash invariati passano. 1.140 ms per la cattura da RAM; memoria allocata gestita 571.738.680 byte durante la cattura e picco del processo 1.744.838.656 byte, comprendente fixture e copie SQLite. Non è una misura di trasferimento del catalogo reale.
- Nessun cambiamento ai test per accettare un'integrità inferiore. Restano nella suite casi TLS negativi, cataloghi SQLite/WAL incoerenti, associazioni irrisolte, selezione cambiata tra tentativi, worker bloccato, riconnessioni e file estranei.

Log e risultati del ciclo in TESTS.md e test-results.txt; confezionamento e riscaricamento pubblico in RELEASE-VERIFICATION.md dopo la pubblicazione. Il collaudo fisico rc.18 riportato dall'utente (due video e poi una copia parziale più grande) non è un test hardware della rc.19.

## Interpretazione delle misure locali

Ogni tentativo scrive `performance-<id>.json` accanto al report. `TotalMilliseconds` va dall'ingresso nel motore alla scrittura del report principale, includendo apertura dei cataloghi locali; esclude la precedente scelta dell'album, l'ultima chiusura dei cataloghi e la scrittura del file prestazioni stesso. Per confrontare l'obiettivo di un'ora serve anche il cronometro dall'avvio della scelta album al risultato finale.

`InitialInventoryMilliseconds` e `FinalInventoryMilliseconds` separano le due letture dell'inventario; `MediaOpenMilliseconds` include le aperture e gli eventuali recuperi che esse provocano; `MediaReadMilliseconds` misura il tempo trascorso nei Read, non il solo hashing; `LocalVerificationMilliseconds` misura gli hash locali effettuati nel ciclo principale, non tutti gli hash di recupero o dei file esterni. `ReportMilliseconds` include salvataggio del riepilogo nel catalogo e report JSON/TXT. Sono tempi inclusi nel totale e in parte sovrapposti ai contatori di trasporto: non si sommano. Il riepilogo Performance nel report principale è fotografato prima delle ultime misure; il file performance contiene i valori finali.

`MediaSourceBytes` conta i byte restituiti dalle letture di originali, compresi confronti e verifica approfondita; `LocalReplayBytes` conta i byte preparati nel temporaneo, anche quando poi non servono per una nuova copia. Non è il totale delle scritture fisiche su disco. Il trasporto Apple separa byte payload catalogo/originali, comandi, richieste di lettura/stat/open, connessioni tentate e tempo degli scambi. `FailedExchanges` conta eccezioni di scambio/framing, non tutte le risposte di errore AFC o ogni rifiuto successivo. `ReadBlockBytes` indica il limite richiesto, non che tutte le risposte abbiano quella lunghezza. Byte TLS/header USB e letture incomplete non consegnate all'applicazione non sono inclusi. CPU e memoria sono del processo, non del solo thread di copia. Tutto rimane sul PC.

## Prossima prova iPhone

1. Chiudere Porto dopo pausa confermata. Estrarre la rc.19 in una cartella nuova, mantenendo tutte le versioni e i file della prova.
2. iPhone collegato, sbloccato e autorizzato; modalità `iPhone · libreria Foto via USB`, `Solo video`, stesso album.
3. Scegliere lo stesso Archivio PC contenente le copie precedenti, `.porto` e i report. Non spostare i file per facilitare il confronto.
4. Per la misura ordinaria lasciare `Verifica approfondita` e `USB iPhone compatibile` non selezionate. Avviare una sola importazione; annotare inizio/fine e leggere il risultato, senza dedurre completezza dalla sola barra.
5. Se ci sono errori di lettura, conservare report e file performance; alla successiva prova scegliere `USB iPhone compatibile`. Nessuna modifica alle protezioni Windows o al telefono.

Per valutare i 29 GB/800 video in un'ora servono velocità reale e tempo di cataloghi, confronti e verifiche sul PC della cliente. La riduzione del traffico inutile è dimostrabile con byte e contatori; il tempo complessivo reale e l'assenza di errori hardware restano da confermare. Blocchi maggiori non eliminano limiti di cavo, telefono, disco, cloud o schema Foto non supportato.
