# Revisione rc.17 — catalogo Foto oltre 512 MiB

## Evidenza e obiettivo della correzione

La schermata reale della rc.16 mostra `APPLE-CATALOG-DB-LIMIT`, con database di **563425280 byte** e WAL di **4227152 byte**. Il database supera il precedente limite di `536870912` byte. Questo identifica la causa del rifiuto prima della lettura del catalogo; non dimostra ancora la compatibilità del suo schema o la disponibilità di album e originali.

La correzione della rc.17 consente un catalogo di queste dimensioni se il computer dispone della memoria necessaria. Non promette cataloghi arbitrariamente grandi: database, WAL e loro somma restano soggetti al limite di **1 GiB**. La riuscita della prova con l'iPhone della cliente resta da confermare.

## Modifiche e consumo di memoria

In `AppleCatalogCapture`, dentro `src/Porto.Apple/AppleGallerySource.cs`, la prima acquisizione conserva database e WAL in RAM. La seconda lettura confronta direttamente tutti i byte con i dati già acquisiti, usando un buffer di **1 MiB**. Non mantiene più un secondo database e un secondo WAL completi. Il confronto resta esatto, senza sostituire i byte con un confronto del solo nome, della dimensione o di un hash.

Restano richiesti i controlli su lunghezza, fine del file, metadati prima e dopo, WAL e journal inattivo. I metadati delle due acquisizioni devono coincidere. Un cambiamento, una lettura troncata, dati in più o una cancellazione interrompono l'acquisizione. Il limite complessivo della fase di acquisizione è dieci minuti; i limiti interni di SQLite rimangono separati.

`AppleCatalogMemory` controlla la RAM fisica disponibile e la capacità di allocazione disponibile indicate da Windows prima delle allocazioni principali: database/WAL, eventuale immagine risultante dal WAL e buffer nativo SQLite. Entrambe devono coprire la nuova allocazione e un margine di **512 MiB**. Il controllo è una misura momentanea, non una prenotazione di memoria: eventuali errori di allocazione vengono comunque gestiti con il messaggio `APPLE-CATALOG-MEMORY`. Non è garantito che bastino 512 MiB di RAM totale o libera per aprire il catalogo.

Il lettore SQLite conserva la copia nativa con **32 byte di spazio aggiuntivo**, inizializzati a zero, e la modalità di deserializzazione di sola lettura. La documentazione SQLite raccomanda spazio aggiuntivo per gestire input malformati. Non è stato adottato un puntatore al buffer gestito della dimensione esatta, che avrebbe rimosso quel margine.

Se l'applicazione del WAL richiede un nuovo buffer e poi fallisce, il buffer nuovo viene azzerato prima di propagare l'errore. I buffer acquisiti e il buffer di confronto vengono azzerati nei percorsi di uscita previsti. La copia nativa viene azzerata e liberata dopo la chiusura di SQLite.

## Revisione avversaria

La revisione indipendente ha verificato la conservazione del confronto completo, la distinzione fra WAL assente e vuoto, i controlli sui metadati, la dimensione aggiuntiva del buffer nativo, la gestione dei limiti fisici e di allocazione e la pulizia del nuovo buffer WAL in caso di errore. Non sono emersi difetti bloccanti nel diff esaminato.

Il piano iniziale di eliminare la copia nativa con un semplice buffer bloccato in memoria è stato scartato perché avrebbe perso i byte aggiuntivi raccomandati da SQLite. L'aumento del limite è accompagnato dalla riduzione delle copie complete e dai controlli di memoria, non dalla sola modifica di una costante.

Le verifiche devono includere dati sintetici validi oltre 512 MiB, differenze tardive durante la seconda lettura, troncamento/crescita, cambiamenti dei metadati e dei journal, cancellazione e confini del controllo di memoria. Comandi, risultati effettivi e limiti della prova finale sono registrati in `docs/TESTS.md`; questa revisione non anticipa risultati o conteggi non ancora eseguiti.

## Riservatezza e limiti residui

La modifica non introduce salvataggi su disco del catalogo personale, accessi a nuovi percorsi del telefono, collegamenti seguiti automaticamente, installazioni o modifiche alle protezioni Windows. I dati sintetici di prova sono separati dalle cartelle delle prove reali. Non sono stati aperti cataloghi o media personali per questa revisione e non sono stati modificati file delle prove.

Due letture uguali non costituiscono una transazione atomica fornita dall'iPhone. Restano possibili modifiche concorrenti, schemi iOS non riconosciuti e originali presenti soltanto nel cloud. Il limite di dieci minuti per l'acquisizione non elimina il limite SQLite separato né garantisce la velocità del collegamento USB. La correzione del limite dimensionale non certifica il resto del percorso: lista album, selezione, copie, conteggi e ripresa devono essere provati sull'iPhone reale.

## Fonti primarie

- [SQLite, sqlite3_deserialize](https://www.sqlite.org/c3ref/deserialize.html): durata del buffer, sola memoria, compatibilità WAL e spazio aggiuntivo raccomandato per input malformati.
- [SQLite, flag di deserializzazione](https://www.sqlite.org/c3ref/c_deserialize_freeonclose.html): semantica di sola lettura e proprietà del buffer.
- [Microsoft, GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex): disponibilità di memoria fisica e virtuale e volatilità delle misure.
