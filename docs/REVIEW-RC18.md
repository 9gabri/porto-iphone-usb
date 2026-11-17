# Revisione rc.18 — associazioni degli album iPhone

## Evidenza e difetto confermato

La schermata reale della rc.17 mostra: «Il catalogo Foto contiene associazioni album non risolte; nessun album viene ampliato automaticamente». Nel percorso del codice questo controllo viene raggiunto dopo acquisizione, confronto delle letture e controllo di integrità SQLite. L'errore indica almeno un riferimento a un album o contenuto non presente nelle tabelle interpretate. Non identifica quale associazione, non dimostra che siano residui di eliminazioni e non prova la compatibilità di ogni parte dello schema iOS.

In `AppleCatalogDecoder.Decode`, il controllo precedente interrompeva la decodifica dell'intero catalogo alla prima associazione irrisolta. Un riferimento irrisolto nell'album A impediva anche la selezione di un album B con associazioni integre. Anche un riferimento a un album non riconosciuto bloccava tutti gli album personali.

Un controesempio minimo è un catalogo con due album personali: B contiene un video presente, mentre A contiene un riferimento a un asset assente. La selezione di B non dipende dall'associazione di A, ma prima non poteva essere raggiunta. Il difetto confermato riguarda l'estensione del blocco; la natura delle associazioni sul telefono reale resta sconosciuta.

## Correzione e confini

La rc.18 conserva un conteggio globale delle associazioni irrisolte e un conteggio per ciascun album personale interessato. Nessuna riga irrisolta viene trasformata in una relazione valida o attribuita a un altro album. Gli album interessati restano visibili, con il numero di associazioni non risolte e i conteggi dei contenuti riconosciuti, ma non sono selezionabili.

Un album personale con associazioni integre rimane selezionabile, mantenendo esattamente i suoi membri. Il parser continua a richiedere un'unica tabella di relazione riconosciuta e a rifiutare i duplicati negli album personali supportati. I contenuti nascosti o eliminati di recente presenti nel catalogo sono riconosciuti prima dell'esclusione e non sono classificati come riferimenti mancanti solo perché esclusi dalla selezione.

La protezione è applicata anche nel modello `AppleGalleryCatalog.Select`, oltre che nel pulsante del selettore. La selezione di un album coinvolto genera `APPLE-ALBUM-ASSOCIATIONS` prima che la sorgente consulti o apra gli originali. La scelta della **galleria completa rimane bloccata in presenza di qualsiasi associazione irrisolta**, anche se alcuni album personali sono utilizzabili. Il comportamento non promette di rendere utilizzabile proprio l'album richiesto dalla cliente.

L'avviso globale resta visibile nel selettore e nella nota di copertura. Una selezione sana può essere verificata come tale; questo non certifica la galleria intera né risolve gli altri album. Le copie precedenti e i contenuti non appartenenti all'album scelto non vengono riorganizzati o eliminati.

## Revisione avversaria

La revisione del diff ha controllato isolamento per chiave album, blocco nel modello, conteggi espliciti, rifiuto dei duplicati e conservazione dei limiti precedenti. Sono stati richiesti controesempi con album sano accanto a un album irrisolto, album sconosciuto, entrambi i riferimenti assenti, selezione apparentemente vuota, contenuti nascosti/eliminati conosciuti e apertura degli originali impedita per selezioni rifiutate.

Il controllo della lista vuota deve mantenere l'avviso di blocco globale, senza suggerire la galleria completa come alternativa praticabile quando anch'essa è bloccata. I risultati dei test e della verifica UI sono registrati in `TESTS.md`; questa revisione non anticipa conteggi o risultati non ancora eseguiti.

## Riservatezza e limiti residui

La revisione usa il codice e dati sintetici; non richiede il database personale, certificati di abbinamento o media del telefono. Non introduce nuove operazioni USB, installazioni, spese o modifiche alle protezioni Windows. I limiti e i controlli di memoria della rc.17 restano applicati.

Sul dispositivo reale occorre verificare quali album siano elencati e se quello desiderato risulti selezionabile. Se appare come non selezionabile, il problema di quell'album rimane aperto: non viene nascosto dietro una copia parziale. Se è selezionabile, il trasferimento di una piccola selezione, i conteggi, gli originali locali e la ripresa restano da collaudare. Il catalogo è uno schema privato osservato, non un'API Apple che garantisce compatibilità universale. Non è una versione certificata o dichiarata priva di bug.
