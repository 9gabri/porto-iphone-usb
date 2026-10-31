# Revisione avversaria rc.4

Mandato in tasks/FINAL-AUDIT-RC4.md. Baseline ea5021f/rc.3, con 120 prove ordinarie
rieseguite prima delle modifiche. Tre ambiti indipendenti e tre cicli massimi:
contabilità/ripresa, USB/interfaccia, storage/sicurezza. Nessun accesso hardware,
file personale, chiamata a modelli esterni, costo o modifica alle protezioni.

## Difetti e risoluzioni

| Controesempio | Risoluzione |
|---|---|
| Una copia storica corrotta non veniva verificata se il video mancava dall'inventario corrente | Audit dei gruppi locali anche senza sorgente corrente; payload alterato conservato e risultato incompleto |
| Un elemento Unknown non candidato scompariva dai debiti al passaggio successivo | Registrazione iniziale/finale e risultato persistente; una riclassificazione con metadati simili non prova identità |
| Un video bloccato era riprovato per primo in ogni sessione, impedendo le copie sane | Suggerimenti d'ordine dopo errore fatale, inventario nuovo e priorità agli elementi non ancora falliti |
| I suggerimenti per nome/dimensione lasciavano davanti i fallimenti in gruppi omonimi o in gruppi diversi | Posizione originale per ogni occorrenza e priorità globale: tutti i candidati non ancora falliti precedono i fallimenti conosciuti, senza saltare elementi |
| Errori nativi di sessione persa e timeout di Read venivano trattati come errori del file | Codici WPD/COM espliciti, frame di sessione distinto con HRESULT, propagazione attraverso i metadati e stop immediato degli ID invalidati |
| Una riga di catalogo poteva indicare un temporaneo estraneo o un ID con traversal | Validazione integrale di dati, nomi generati, percorsi e riferimenti prima dell'uso |
| DDL con CHECK o indici UNIQUE/filtrati/collazioni diverse veniva adottato | Confronto delle definizioni SQL con le forme effettivamente generate dalle candidate precedenti |
| Validazione journal lasciava una copia del DB a ogni tentativo | Rimozione in finally esclusivamente dei due file creati dall'operazione e della relativa cartella vuota |
| Un collegamento in una riga successiva veniva rilevato dopo il recupero della prima | Preflight di tutti i percorsi fisici prima delle scritture, con e senza journal |
| Relazioni di sostituzione circolari erano ammesse | Controllo iterativo lineare dei cicli, con prova positiva delle catene di recupero valide |
| Il file delle preferenze poteva seguire un hard link verso un file estraneo | Controlli dei collegamenti prima di leggere/scrivere; prove mediante percorsi iniettati, senza profilo reale |
| La stima del tempo restava visibile dopo pausa o fine lavoro | Rimozione della stima a trasferimento fermo; smoke dell'interfaccia con pausa a copia parziale |
| ZIP incompleto occupava il nome definitivo impedendo un nuovo tentativo | Compressione e verifica nello staging, poi spostamento senza sostituzione; checksum per tentativo |

La prova di preflight rafforza il controllo dell'intero catalogo: il primo payload
che veniva spostato era appartenente all'app, non un file estraneo. Il difetto di
percorso non validato nella prima riga, invece, poteva realmente rinominare o
modificare una sentinella estranea nella destinazione sintetica.

## Percorsi, gravità e riproduzione

I riscontri della tabella sono difetti confermati, non preferenze stilistiche.
P1 indica falsa completezza, ostacolo alle copie sane o modifica di file estranei;
P2 indica un recupero non affidabile, risorse accumulate o diagnostica fuorviante.
Le gravità descrivono il comportamento prima della correzione. Le regressioni
sono riproducibili con `scripts/dev.ps1 test`; nomi e predisposizione completa
sono nei file di test indicati. Tutti i payload sono sintetici.

| Gravità | Percorso interessato | Scenario e conseguenza prima della correzione | Regressione |
|---|---|---|---|
| P1 | `src/Porto.Core/ImportEngine.cs`, audit dei gruppi | Copiare A/B, alterare A, presentare solo B: possibile completamento pur con A corrotto | `FinalLedgerTests.cs`, historical corruption |
| P1 | `src/Porto.Core/ImportEngine.cs`, Observe/SaveProgress | Osservare Unknown non leggibile, poi ometterlo o riclassificarlo: l'obbligo poteva sparire | `FinalLedgerTests.cs`, unknown initial/final/reclassified |
| P1 | `src/Porto.Core/ImportSession.cs`, `RetryOrder.cs` | Bloccare Open/Read del primo video: tre sessioni ripetevano lo stesso errore prima dei video sani | `FinalUsbTests.cs`, hanging first open/read |
| P1 | `src/Porto.Core/RetryOrder.cs` | Due gruppi con nomi/dimensioni uguali e due errori fatali: un gruppo fallito precedeva un fratello sano dell'altro gruppo | `FinalUsbTests.cs`, tied metadata mixed-singleton/mixed-pairs |
| P1 | `src/Porto.Usb/Com.cs`, `WpdSource.cs`, `WorkerProtocol.cs`, `WorkerClient.cs` | Simulare HRESULT fatale, EOF o timeout durante Read: ID della sessione persa ancora utilizzabili e errore attribuito al file successivo | `FinalUsbTests.cs`, native session/optional metadata/eof |
| P2 | `src/Porto.Usb/Com.cs` | Registrazione WPD mancante: messaggio generico che indirizzava verso autorizzazione del telefono | `FinalUsbTests.cs`, missing WPD registration |
| P1 | `src/Porto.Core/Catalog.cs`, `ImportEngine.Recover`, nuovo `CatalogValidation.cs` | DB Ready punta a una sentinella o Copying contiene traversal: rinomina o append su file estraneo; righe tarde non valide potevano seguire un recupero già iniziato | `FinalStorageTests.cs`, Ready/Copying/Complete/invalid source/NUL/duplicate final |
| P2 | `src/Porto.Core/SqliteOwnership.cs`, ValidateSchema | Aggiungere CHECK o indice UNIQUE/parziale/collazione: schema estraneo adottato con semantica diversa | `FinalStorageTests.cs`, foreign index/added CHECK |
| P2 | `src/Porto.Core/SqliteOwnership.cs`, Inspect | Ripetere validazione con journal: una copia del DB rimaneva per tentativo, accumulando spazio | `FinalStorageTests.cs`, journal validation leaves no redundant snapshots |
| P2 | `src/Porto.Core/CatalogValidation.cs`, Read | Riga valida Ready seguita da hard link: primo file dell'app spostato prima del rifiuto della seconda riga, anche con journal | `FinalStorageTests.cs`, preflights every physical path |
| P2 | `src/Porto.Core/CatalogValidation.cs`, relazioni Replaces | Due righe si sostituiscono reciprocamente: storia incoerente ammessa dal catalogo | `FinalStorageTests.cs`, replacement cycles/acyclic history |
| P1 | `src/Porto.App/AppPreferences.cs` | Preferenza hard-linked a sentinella: salvataggio sovrascrive il file estraneo | `FinalPreferenceTests.cs`, hard link/linked settings |
| P2 | `src/Porto.App/MainForm.cs`, SetBusy/ShowProgress | Pausa a copia parziale: resta una stima di trasferimento attivo | Smoke `--ui-smoke` con nome `compact-paused.png` |
| P2 | `scripts/package.ps1` | Compressione interrotta occupa il nome definitivo dello ZIP e impedisce nuovo tentativo | Quattro prove sintetiche del confezionamento, descritte in TESTS.md |

I file di test citati si trovano in `tests/Porto.Tests`. I controlli positivi
preservano i cataloghi e i nomi effettivamente prodotti dalle candidate precedenti,
le catene valide e gli errori ordinari del singolo file. Nessuna riscrittura
generale o modifica motivata soltanto da preferenze stilistiche.

## Prove negative e riesame

Sulla logica rc.3, i primi test hanno riprodotto 31 fallimenti; sette controlli
nuovi passavano già. Il filtro includeva anche quattro prove precedenti: 11/42.
Due ulteriori prove delle preferenze fallivano dopo la sola introduzione di un
percorso iniettabile per il test, prima dei controlli dei collegamenti.

Il secondo riesame ha riprodotto dieci ulteriori controesempi (60/70 nel filtro),
poi corretti. Nel terzo e ultimo ciclo sono stati riprodotti due casi con gruppi
diversi di file bloccati (0/2), corretti con priorità globale alle occorrenze non
ancora fallite. Le correzioni conclusive sono verificate dalla suite finale e
dall'ispezione del coordinatore; non è stato avviato un quarto ciclo di revisione.

Un dubbio su junction pendenti è stato controllato in una cartella sintetica:
su questo Windows il collegamento rimane rilevabile come Directory/ReparsePoint
anche senza la destinazione. Non è stato modificato codice per un difetto non
dimostrato. Nessun permesso o impostazione di Windows cambiato per la prova.

## Confini della consegna

- Le prove USB usano processi figli e ABI COM sintetici; nessun iPhone fisico.
- Gli indizi d'ordine non provano identità. Un inventario riordinato o più blocchi
  può esaurire i tre tentativi: l'app deve restare esplicitamente incompleta.
- Non esiste autenticazione crittografica del catalogo. Non modificarlo o unire
  cartelle di telefoni diversi; un processo locale ostile concorrente resta escluso.
- Le letture locali non consentite interrompono in sicurezza la verifica; non
  vengono convertite in successi. Perdita reale di alimentazione non validata.
- Originali solo iCloud, originalità Foto e ogni possibile variante di codec non
  sono certificati. Eseguibile non firmato, nessuna promessa di zero bug.

Risultati finali in TESTS.md/test-results.txt; verifica pubblicazione in
RELEASE-VERIFICATION.md. I resoconti precedenti sono conservati.
