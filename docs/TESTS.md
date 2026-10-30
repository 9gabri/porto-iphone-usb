# Prove della candidata 0.1.0-rc.1

Ambiente: Windows 11 x64, build OS 26200; .NET SDK 10.0.403, runtime Microsoft 10.0.14; destinazione sintetica locale NTFS. Nessun iPhone è stato cercato, aperto o usato durante lo sviluppo. Nessun file personale è stato letto. Data: 30 ottobre 2026.

## Prove automatiche

**Esito finale: 44/44 test superati**, più avvio UI e smoke del worker sull'eseguibile self-contained. I test includono anche riparazioni interrotte con perdita o corruzione del nuovo temporaneo, senza duplicare i mancanti logici.

La suite console non richiede pacchetti di test esterni. `scripts/dev.ps1 test` compila ed esegue i test ordinari. `dotnet run --project tests/Porto.Tests -c Release -- --large` aggiunge le prove di scala. Esito dettagliato in [test-results.txt](test-results.txt).

Coperti: SHA-256 e rilettura, sorgente troncata/estesa, cancellazione, SQLite reale, cambiamento degli ID, molteplicità di tre video identici, nomi collisionanti, copie rimosse o corrotte, seconda lettura USB differente, spazio iniziale insufficiente e riempimento durante copia, disconnessione/riconnessione, percorsi ostili, lock concorrente, Live Photo/varianti ambigue, foto abbinate, MOV/MP4/M4V, finta estensione senza video, inventario incompleto e mancante offline sintetico.

Arresti **reali del processo di test** tramite Environment.Exit prima/dopo sei confini: intento, flush, verifica, Ready, rinomina, Complete. Nuovo processo recupera senza duplicati definitivi. Inoltre: collisione del finale dopo Ready, temporaneo danneggiato, quarantena riutilizzata senza creare nuovi `.part`, inventario ripetitivo/fallito limitato.

Worker: processi sintetici bloccati prima della risposta e a metà payload; timeout e cancellazione devono terminarli. COM smoke: creazione degli oggetti WPD, scambio proprietà locali stringa, GUID e UInt64 oltre 4 GiB. **Questa prova non chiama GetDevices o Open** e non dimostra una connessione a iPhone.

## Dimensioni effettive e simulate

| Prova | Cosa viene eseguito | Cosa non dimostra |
|---|---|---|
| Copie ordinarie | File sintetici piccoli realmente scritti, riletti e pubblicati su NTFS | Video reali, decodifica o USB fisica |
| Flusso oltre 4 GiB | 4.294.967.313 byte generati, letti e sottoposti a SHA-256; output Stream.Null | Un file oltre 4 GiB scritto su disco o trasferito da iPhone |
| 50.000 elementi, 250 GB | Inventario completo sintetico serializzato, classificazione e controllo di capacità; payload non scritto | Trasferimento reale di 250 GB |
| Crash recovery | Terminazione processo ai confini e riavvio reale | Perdita di alimentazione, guasto controller/disco o corruzione del filesystem |

I tempi e il working set osservati sono nel log; non sono benchmark USB. Nella prima prova estesa il flusso oltre 4 GiB ha richiesto circa 2,7 secondi con working set 54 MiB; l'inventario circa 0,13 secondi con working set 69 MiB. Sono misure su questo ambiente, non promesse di prestazioni.

## Revisione e artefatto

Revisione indipendente di correttezza/sicurezza del core e dell'ABI USB, confrontata con gli header Microsoft. Corrette le criticità emerse: errori InvalidDataException non classificati, recovery bloccata su collisione/corruzione, falsi positivi Live Photo, riscrittura quadratica dei report, duplicazione dei temporanei ambigui e limite inventario aggirabile. Il report ora persiste risultati incrementali.

Prova di mutazione: inversione temporanea del confronto d'integrità, suite attesa in errore, poi ripristino del codice e nuova esecuzione corretta. Nessun codice mutato distribuito.

Avvio effettivo della UI compilata in `--ui-smoke` senza USB e ispezione della sua immagine. Avvio della distribuzione self-contained con runtime di sistema .NET 10 non installato. Smoke `--interop-smoke` e `--worker-smoke` sul vero eseguibile verificano anche l'avvio del processo figlio, il protocollo e l'appartamento COM MTA, senza enumerazione dispositivi. Queste prove hanno rilevato e fatto correggere una inizializzazione COM STA/MTA incompatibile nell'entry point. La prova UI verifica l'avvio, non un trasferimento fisico né tutte le interazioni tramite mouse.

L'archivio pubblico viene riscaricato e confrontato tramite SHA-256 con l'archivio locale. Questo verifica la corrispondenza dell'artefatto, non sostituisce una firma Authenticode. L'eseguibile Porto è non firmato.

## Compatibilità non verificata

iPhone 16/17, qualsiasi versione iOS, driver Apple effettivi, velocità dei cavi USB, originali solo iCloud, comportamento delle risorse modificate, riconoscimento completo di Live Photos. Nessuna verifica Windows ARM, exFAT o cartelle di rete (rifiutati nella candidata). Nessun trasferimento fisico da 250 GB. Il sito e l'app riportano questi limiti prima/durante l'uso.
