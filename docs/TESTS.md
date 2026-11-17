# Verifiche 0.1.0-rc.17

Solo dati sintetici: nessun telefono, catalogo personale o file delle prove aperto o modificato. Il problema identificato nella foto rc.16 è il limite di dimensione, non ancora la compatibilità dello schema iOS.

## Riproduzione e correzione

- Contro la DLL pubblica immutabile rc.16: entrambe le prove con DB 563425280 byte (con WAL 4227152 byte oppure assente) rifiutate con APPLE-CATALOG-DB-LIMIT, 0/2 accettate.
- Sempre contro rc.16: test del buffer di confronto fallito, 0/1, perché la seconda lettura allocava l'intero catalogo.
- Con la correzione: 9/9 test dedicati di confronto a blocchi, incluse letture corte, differenze, troncamenti/crescita, WAL apparso/scomparso/modificato, metadati mutati dopo EOF, journal attivo e cancellazione; controllo buffer massimo 1 MiB e azzeramento.
- Tre test del controllo memoria: soglia esatta, RAM o capacità di allocazione insufficienti, input negativi/enormi e lettura della disponibilità Windows. Nessun limite o controllo di integrità viene disabilitato.

## Prova grande effettiva

Comando sul build finale: `Porto.Tests.exe --large-apple "--filter=apple large catalog"`, 3/3 passati. Include le due dimensioni riportate e una acquisizione completa con DB SQLite valido di **566362112 byte**, oltre quello della cliente, e WAL valido di **4227152 byte**. I dati sono generati in una cartella temporanea isolata e rimossi dal fixture; non sono cataloghi del telefono.

Verificati due passaggi integrali su DB e WAL, integrità SQLite, conteggi e appartenenza all'album, sorgenti sintetiche invariate mediante SHA-256. Allocazioni managed durante la cattura: **571731568 byte**, inferiori alla soglia DB+128 MiB che rileverebbe una seconda copia completa. Picco working set del processo di test: **1746190336 byte**, comprendente anche la sorgente sintetica in RAM. Cattura/decodifica in memoria: 786 ms; questo valore NON misura la velocità USB reale. La verifica strutturale mantiene i limiti SQLite esistenti.

## Build e controlli finali

Build dei test e dell'app senza avvisi/errori. Due smoke UI locali, inclusa la schermata compatta con diagnostica numerica completa controllata visivamente. Suite ordinaria finale **528/528 superata**, eseguita tramite apphost Porto.Tests.exe sul build finale. Log `test-results.txt`; verifiche del pacchetto pubblicato in `RELEASE-VERIFICATION.md`, registrate dopo il tag.

La prova grande Apple è stata eseguita in questo ciclo; le precedenti prove estese con file video oltre 4 GB e inventari di grandi dimensioni non sono state ripetute. La suite ordinaria continua a includere copia/ripresa, errori, arresti di processo, conservazione dei file estranei, cataloghi, USB e TLS.

## Limiti della verifica

Un controllo della disponibilità RAM/commit è una misura momentanea, non una prenotazione. Porto mantiene un margine di 512 MiB e gestisce anche errori di allocazione. Non è stato aumentato il limite del parser senza accompagnarlo a confronto a blocchi e controlli sulle allocazioni. Il buffer nativo conserva 32 byte aggiuntivi secondo SQLite.

Sull'iPhone reale restano da confermare lettura coerente del catalogo, schema iOS, album, originali locali, copia e ripresa. Le prove sintetiche non attestano riuscita della prossima importazione o assenza universale di bug. Dettagli e revisione indipendente in `REVIEW-RC17.md`.
