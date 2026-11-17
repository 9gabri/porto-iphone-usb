# Porto 0.1.0-rc.20 — riduzione delle attese Apple

17 novembre 2026. Candidata Windows per una prova misurata sull'iPhone. Obiettivo: circa 800 elementi, 29 GB, entro 60 minuti inclusi inventario, confronto, copia, verifiche e report. Questo ciclo non dispone di un telefono autorizzato né dei nuovi report reali: **l'obiettivo di un'ora non è dimostrato**. Nessun file personale delle prove, telefono o record di abbinamento letto o modificato. Nessun costo, servizio/driver installato o protezione ridotta.

## Baseline e modifica mantenuta

Baseline pubblica rc.19, tag `fef8d242136bcd191d04f5171095d92dc67533f2`. La nuova versione cambia solo il limite delle richieste di lettura Apple: 256 -> 512 KiB in `AppleAfcFileSystem.ReadLimit` e nella sessione creata da `AppleUsbTransport.Open`. Si applica agli originali e alle letture del catalogo che richiedono blocchi sufficientemente grandi. Le richieste restano seriali; nessun parallelismo su handle/contenuti o pipeline introdotto senza prove. Il limite di pacchetto resta 1 MiB, e 512 KiB più header vi rientrano. Una risposta maggiore della richiesta viene rifiutata; non si accettano più byte sulla fiducia.

L'opzione `USB iPhone compatibile` mantiene 64 KiB su una nuova sessione; non c'è ripiego silenzioso dopo errori. Non si presume che ogni modello/iOS accetti le stesse dimensioni. Il nuovo limite richiede una prova reale. Nessuna nuova dipendenza.

Il motore Core resta quello di rc.19: confronto completo delle copie precedenti, riuso locale della prima lettura quando possibile, rilettura SHA-256 del file salvato, struttura, EOF, dimensioni e verifica approfondita indipendente opzionale. Molteplicità, inventario finale, cataloghi SQLite/WAL, contabilità degli elementi pendenti, transazioni e flush durevoli non sono eliminati. Autenticazione TLS, timeout, cancellazione e invalidazione delle sessioni malformate invariati. Il percorso Android/WPD non cambia.

## Benchmark AFC mantenuto

Harness riproducibile in `tests/Porto.TransportPerformance`, osservazioni in `docs/performance-rc20/transport-results.json`. Windows di sviluppo, .NET 10.0.14, contenuti pseudocasuali sintetici; nessuna USB reale. Ogni variante è preriscaldata; tre ripetizioni alternate per ciascuna dimensione e latenza. Cambia una sola variabile per gruppo. Ogni lettura verifica hash, byte ricevuti e numero delle richieste. Payload 16/64 MiB, destinazione Stream.Null: include hashing del flusso, non disco, catalogo o importazione completa.

| 16 MiB con attesa simulata | rc.19: 256 KiB | rc.20: 512 KiB |
|---|---:|---:|
| Richieste di lettura, EOF incluso | 65 | 33 |
| Tre durate, ms | 1.019,52 / 1.008,80 / 1.004,12 | 505,08 / 503,06 / 504,85 |
| Mediana, ms | 1.008,80 | 504,85 |
| Attesa artificiale effettiva complessiva, ms | 968–983 | 473–478 |

Intervalli separati e riduzione della mediana circa 50%. Il codice richiede `Thread.Sleep(1)` per risposta payload, ma il timer Windows ha prodotto circa 15 ms effettivi: NON è una misura della latenza dell'iPhone. Nessuna estrapolazione automatica alle sei ore della cliente. Con latenza zero il blocco maggiore non è uniformemente più veloce; il beneficio riguarda le attese fra richieste. Il test a 64 KiB serve da riferimento di compatibilità (257 richieste, circa 4 s nello stesso scenario).

Dodici controlli negativi: payload troncato, scollegamento, sequenza errata, risposta eccessiva, EOF prematuro e cancellazione, ciascuno a 256/512 KiB. Tutti rifiutati senza pubblicare una copia; sessioni malformate invalidate. EOF con framing valido resta un errore del file se non coincide con la lunghezza attesa. Cancellazione sintetica circa 50–62 ms, non promessa di tempo su un driver reale.

Ripetizione sul build finale rc.20 (`transport-final-results.json`): tre tempi a 256 KiB 911,42 / 1.011,85 / 994,65 ms; a 512 KiB 460,39 / 497,39 / 502,12 ms. Mediane 994,65 -> 497,39 ms, intervalli ancora separati. Tutti i dodici guasti nuovamente passati. La seconda esecuzione conferma il risultato del simulatore, non sostituisce la prova hardware.

## Prototipo escluso, evidenza conservata

È stato provato un percorso diretto per nuovi file la cui lunghezza della risorsa aperta non coincide con nessuna copia precedente. Non deduplicava per dimensione: preservava hash, EOF, verifiche indipendenti e rifiuto di lunghezze false. Dodici nuovi controesempi (incluse quattro morti reali del processo) e sedici regressioni rc.19 passavano. Riduceva a zero il replay locale e l'hashing superfluo, ma non ha dimostrato un guadagno nel tempo totale.

- Primo confronto: 3 file da 64 MiB, tre prove senza attese artificiali. Baseline 1,35–1,93 s, prototipo 1,50–1,64 s: variabilità sovrapposta, nessun guadagno dimostrato.
- Secondo confronto: flusso sintetico da 512 MiB, una copia precedente da 32 KiB, NTFS reale, memoria limitata, warmup escluso. Tre tempi baseline 3,68 / 17,25 / 9,03 s; prototipo 14,64 / 20,76 / 15,72 s. Una sola apertura e 512 MiB letti dalla sorgente in entrambe le varianti. Replay 512 MiB -> 0, CPU del motore circa 2,86–2,94 -> 1,75–2,03 s. Il tempo totale non migliora: il prototipo è stato rimosso dal prodotto, anche se usava meno risorse. Non si attribuisce la variabilità a una causa non misurata.

Risultati completi del secondo esperimento in `performance-rc20/discarded-direct-*.json`; `cpuMs` esterno include controlli aggiuntivi del harness, usare `Performance.ProcessCpuMilliseconds` per il motore. Harness, patch e file sintetici sono conservati localmente in `artifacts/rc20-streambench`, `artifacts/rc20-bench`, `artifacts/rc20-discarded-direct`. Nessuno di quei prototipi è nel pacchetto. Anche l'accorpamento delle due scritture di comando resta solo un esperimento, senza vantaggio sufficiente per inserirlo.

## Budget dell'ora e misura hardware necessaria

Assumendo 29 GB decimali, servono **8,06 MB/s (7,68 MiB/s) medi sull'intera operazione**. Se inventario, confronto e verifiche occupano 10 minuti non sovrapposti alla copia, i byte dei nuovi file devono passare in 50 minuti: almeno **9,67 MB/s**. Con 20 minuti residui di altre fasi: **12,08 MB/s** nei 40 minuti di copia. Se 29 indica GiB, il fabbisogno è maggiore. Questi sono budget matematici, non stime della velocità raggiunta.

Il traffico dal telefono può superare 29 GB per cataloghi, confronto con copie precedenti, tentativi e modalità approfondita. La ripresa delle 323 copie richiede ancora verificarle e confrontarne il contenuto: non viene considerata gratuita. File con dimensioni diverse non richiedono lo stesso tempo. Una versione nuova non consente di dedurre quanto dureranno quei 323 video dal solo numero.

Per il prossimo confronto: stessa selezione e destinazione, stesso PC/cavo/porta, modalità libreria Foto, compatibilità non selezionata. Conservare impostazione della verifica approfondita fra confronti; per la prova standard può restare disattivata, con rilettura locale sempre obbligatoria. Annotare ora di avvio e completamento inclusi scelta album e report, byte selezionati, già presenti, nuovi, pendenti e tentativi. I `performance-*.json` distinguono byte/attese AFC e media complessiva; non contengono contenuti né vengono inviati. Il loro tempo per passaggio esclude scelta album precedente e piccole chiusure finali: per l'obiettivo utente serve anche il tempo a parete completo, includendo tutti i tentativi.

Occorre confermare sul dispositivo che 512 KiB siano accettati senza errori, la velocità media sostenuta e quale fase domini. Se domina il trasferimento seriale, c'è margine; se dominano disco, catalogo o limiti del telefono, il guadagno può essere molto minore o assente. Non si promettono 50 minuti, assenza di blocchi fisici o compatibilità universale. I file delle prove e le versioni precedenti sono conservati.

## Verifica della candidata

La revisione indipendente delegata non è stata disponibile per limite di quota. Nessun fallback a pagamento. Diff riesaminato localmente, test di regressione e guasti sintetici eseguiti; non presentati come approvazione di revisori indipendenti. Esiti finali, controlli di avvio e pacchetto pubblico: `TESTS.md`, `test-results.txt` e resoconto successivo al tag in `RELEASE-VERIFICATION.md`.

Suite finale 555/555, catalogo grande e flusso oltre 4 GiB passati. La ripetizione della copia NTFS oltre 4 GiB è stata interrotta per lentezza delle scritture e non viene dichiarata superata: dettagli in TESTS.md. La prova completa precedente rc.19 resta documentata; Core non cambia nella rc.20. Questo limite rafforza la necessità di misurare il disco e l'intera operazione sul PC reale, senza trasformare il risultato AFC in una promessa sui tempi totali.
