# Verifiche della candidata diagnostica 0.1.0-rc.16

Questa versione identifica il blocco del catalogo, non lo dichiara risolto. Nessun telefono, catalogo o media reale usato nei test; nessuna modifica ai file delle prove.

## Riproduzione

Otto casi di metadati non validi contro il binario pubblico rc.15: 0/8 test diagnostici superati. Sette condizioni usavano il messaggio indistinto; il caso difensivo WAL negativo raggiungeva un'apertura non prevista (il trasporto reale già respinge lunghezze negative). Non sono otto difetti nuovi di trasferimento.

Dopo la modifica: 11/11 casi dedicati, inclusi limiti esatti, somma delle dimensioni, WAL assente/vuoto, long.MaxValue, nessuna apertura dopo rifiuto e classificazione AFC sintetica di file/cartella/collegamento/tipo sconosciuto. Nessun valore arbitrario del dispositivo nei messaggi.

## Verifica finale pertinente

Build dei test senza avvisi/errori. Eseguiti tramite apphost Porto.Tests.exe: filtro apple 88/88, filtro gallery import 12/12 (100 casi distinti). I casi comprendono autenticazione, protocollo AFC, catalogo SQLite/WAL, acquisizione e importazione galleria. Log in test-results.txt. La suite generale 503/503 della rc.15 e le prove estese precedenti non sono state ripetute in questo ciclo diagnostico.

Build finale app senza avvisi/errori; due smoke UI locali, inclusa la schermata compatta con il messaggio numerico completo, controllata visivamente. Un primo build del nuovo scenario UI sintetico aveva un nome di variabile errato: corretto prima del build finale, nessun errore ignorato. I comandi normali dell'app non eseguono lo scenario sintetico.

Il confezionamento e il download pubblico verificano interop, worker, nove stati UI, versione e integrità senza enumerare telefoni. Resoconto successivo al tag in RELEASE-VERIFICATION.md. Nessun test rimosso o limite di memoria aumentato.

## Limite residuo

L'esecuzione rc.15 dell'utente ha raggiunto i metadati del catalogo dopo TLS; non conosciamo ancora quale dei controlli abbia rifiutato il catalogo. La rc.16 serve a mostrare codice, dimensioni e tipo. Non dimostra apertura degli album o importazione riuscita. Non è pronta per dichiarare completato il caso iPhone. Analisi in REVIEW-RC16.md.
