# Revisione rc.16 — diagnosi del blocco del catalogo Foto

## Esito e limite della consegna

La rc.16 è una **candidata diagnostica**, non una soluzione già verificata del blocco della libreria Foto. Il nuovo messaggio della prova reale rc.15 dimostra che quell'esecuzione ha superato la fase TLS ed è arrivata al controllo dei metadati AFC del catalogo. Non dimostra che il catalogo sia stato letto, che gli album siano disponibili o che la copia dei media funzioni.

Il blocco resta da identificare con precisione sul computer della cliente. La candidata non va presentata come pronta per importare un album o l'intera libreria iPhone. Non è disponibile un nuovo risultato di copia reale su cui basare conteggi o promesse di completezza.

## Evidenza e difetto confermato

La schermata rc.15 mostra:

> Catalogo Foto oltre il limite di 512 MiB in memoria o non leggibile come file regolare.

In `AppleCatalogCapture.Capture`, dentro `src/Porto.Apple/AppleGallerySource.cs`, lo stesso messaggio copriva sei condizioni: database non regolare, database vuoto, database oltre il limite, WAL non regolare, WAL oltre il limite e somma di database e WAL oltre il limite. Il difetto confermato è l'ambiguità diagnostica. La schermata da sola non identifica quale condizione sia vera; non prova una dimensione superiore a 512 MiB e non prova la presenza di un collegamento.

Il controllo del rollback journal usa un messaggio distinto. Il messaggio osservato non proviene da quel controllo.

## Modifica circoscritta

`AppleCatalogCapture.ValidateMetadata` produce un codice distinto prima di allocare e leggere il database e il WAL. Mantiene il limite di `536870912` byte (512 MiB), aggiungendo un controllo difensivo per una lunghezza WAL negativa.

| Codice | Condizione segnalata |
| --- | --- |
| `APPLE-CATALOG-DB-TYPE` | Il database non è esposto come file regolare. |
| `APPLE-CATALOG-DB-EMPTY` | La dimensione del database è nulla o non valida. |
| `APPLE-CATALOG-DB-LIMIT` | Il database da solo supera 512 MiB. |
| `APPLE-CATALOG-WAL-TYPE` | Il WAL presente non è esposto come file regolare. |
| `APPLE-CATALOG-WAL-LENGTH` | La lunghezza del WAL è negativa. |
| `APPLE-CATALOG-WAL-LIMIT` | Il WAL da solo supera 512 MiB. |
| `APPLE-CATALOG-TOTAL-LIMIT` | Database e WAL insieme superano 512 MiB. |

Il messaggio riporta le dimensioni numeriche di database e WAL, oppure WAL assente. Per i tipi non regolari usa soltanto descrizioni fisse: cartella, collegamento o tipo non regolare non riconosciuto. Non espone percorsi, identificativi del telefono, nomi di album, contenuti del catalogo o certificati. Se sono presenti più condizioni, viene segnalata la prima secondo l'ordine del controllo.

La distinzione del tipo è basata sui metadati già richiesti ad AFC. Non autorizza a seguire collegamenti, ad ampliare i percorsi leggibili o ad accedere ad altri dati del telefono. La lettura dei media non viene avviata dal comando **Scegli album**.

## Perché il limite non viene alzato alla cieca

L'acquisizione attuale mantiene database e WAL in memoria, li rilegge per verificarne la stabilità, applica il WAL e apre l'immagine SQLite. Le copie e le allocazioni native possono richiedere molta più memoria della sola dimensione del database. Aumentare una costante non dimostrerebbe né sostenibilità né correttezza.

Esistono implementazioni pubbliche che documentano cataloghi Foto molto grandi, ma questo non misura il catalogo della cliente. Un eventuale supporto a cataloghi più grandi richiede una modifica separata, con verifica della coerenza tra pagine e WAL, limiti di risorse e test dedicati. Questa versione non introduce una cache su disco del database e non allenta i controlli su file e collegamenti.

## Verifiche e limiti dell'evidenza

La verifica di questa modifica riguarda le risposte ai metadati sintetici: distinzione delle condizioni, valori al confine del limite, WAL assente o vuoto, metadati non validi e messaggi privi di dati personali. La registrazione dei comandi e dei risultati effettivi della versione consegnata è in `docs/TESTS.md`; questa revisione non anticipa conteggi di test o risultati di hardware non eseguiti.

Per l'analisi sono stati usati codice, schermate fornite dall'utente e fonti pubbliche. Non sono stati acquisiti un database Foto reale, certificati di abbinamento o media personali; i file delle prove non sono stati modificati. La riuscita dell'importazione iPhone resta da verificare dopo aver identificato e risolto il blocco.

## Prossima prova minima

Chiudere Porto, estrarre rc.16 in una cartella nuova e controllarne il titolo. Con l'iPhone già collegato, sbloccato e autorizzato, scegliere **iPhone · libreria Foto via USB → Cerca dispositivi**, selezionare il telefono, poi **Un album della libreria Foto → Scegli album**. Conservare una foto del messaggio completo, con codice e dimensioni, oppure segnalare se appare l'elenco degli album.

Non avviare una grande importazione per questa diagnosi. Non cambiare le protezioni di Windows, installare servizi o ripetere tentativi identici. Non è necessario inviare database, certificati o media. Le copie precedenti, i report e le cartelle delle prove vanno conservati.

## Fonti tecniche consultate

- [pymobiledevice3, implementazione AFC](https://github.com/doronz88/pymobiledevice3/blob/master/pymobiledevice3/services/afc.py): gestisce `st_ifmt` con i valori `S_IFREG`, `S_IFDIR` e `S_IFLNK`; i collegamenti sono un caso distinto. Supporta la lettura del formato dei metadati, non una conclusione sul telefono della cliente.
- [libimobiledevice, client AFC](https://github.com/libimobiledevice/libimobiledevice/blob/master/tools/afcclient.c): interpreta separatamente i tipi regolare, cartella e collegamento. Non giustifica accettare automaticamente un tipo sconosciuto.
- [iphone-manager, accesso SQLite remoto](https://github.com/tmkaio/iphone-manager/blob/main/app/sqlite_remote.py): documenta l'esperienza dell'autore con un catalogo Foto di grandi dimensioni e lettura per pagine. È evidenza della plausibilità di cataloghi grandi, non della causa del caso presente né della correttezza di una nuova implementazione Porto.
