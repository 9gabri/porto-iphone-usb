# Revisione rc.12 — JPEG con coda e recupero

## Difetto confermato

Il lettore JPEG della rc.11 richiedeva che il marcatore EOI dell'immagine principale coincidesse con la fine del file. Una prova autorizzata aveva 29 foto JPG rifiutate dopo trasferimento e verifica locale SHA-256. Il primo campione contiene una struttura JPEG baseline completa seguita da 190 byte. La diagnostica non ha visualizzato o esportato le immagini.

Il lettore corretto è stato verificato in sola lettura sulle 29 copie temporanee indicate dal report: tutte sono riconosciute, 28 con 190 byte aggiuntivi e una con 1.494. Hash prima/dopo e confronto con l'hash registrato dal report coincidono per tutte. Lo stesso strumento collegato alla DLL rc.11 le rifiutava tutte. Non è stata eseguita un'importazione sull'archivio personale, non sono stati aperti cataloghi in scrittura e non sono stati spostati o rinominati temporanei.

Nessun file, nome personale, percorso, hash dei media o metadato privato della prova viene distribuito. Le fixture pubbliche sono sintetiche e indipendenti dalle immagini reali.

## Contratto della correzione

EOI termina l'immagine primaria, non necessariamente l'intero file. La correzione rimuove esclusivamente il vincolo EOI==EOF: restano i controlli di frame, tabelle, componenti, scansioni, limiti e cancellazione. La coda viene conservata integralmente e conteggiata, senza interpretarla o dichiararla conforme a un formato ausiliario. Il report mantiene questa informazione anche quando riutilizza una copia precedente.

Non è un'eccezione per un produttore. Il controllo è comune a tutte le sorgenti che importano foto. JPEG con dati HDR, Motion Photo o metadati possono essere conservati come un singolo file; non vengono estratti video o certificate le funzioni aggiuntive. Si verificano struttura primaria e integrità dei byte, non decodifica dei pixel o assenza di contenuti arbitrari nella coda. Fonti e motivazione in SOURCES.md.

## Riproduzione e revisione indipendente

- Cinque nuovi test del parser: coda sintetica di 190 byte; code varie incluse firme JPEG/video; primarie invalide ancora rifiutate; diagnosi del marcatore finale; limite totale di 512 MiB/cancellazione. Il precedente test che imponeva EOF viene aggiornato secondo il contratto documentato, mantenendo tutte le altre asserzioni.
- Prima della correzione: 3/6 casi JPEG passati; fallimenti di accettazione e dettaglio riprodotti. Dopo: 37/37 casi filtro photo superati, incluse integrazioni del recupero.
- Tre regressioni del motore riprodotte prima della correzione: 0/3. Un archivio sintetico con 36 foto, 12 video e 29 foto in quarantena passa a 65 foto e 12 video; 29 nuove pubblicazioni, 48 riutilizzi, poi 77 riutilizzi e zero nuove copie. I vecchi temporanei e un file estraneo restano invariati. Una modifica della sola coda cambia l'identità del contenuto; una coda video non trasforma la foto in video autonomo.
- Un revisore indipendente ha controllato contratto e diff finale: nessun difetto confermato entro il perimetro. Ha segnalato che il ramo AlreadyPresent ometteva il dettaglio dell'ispezione; la correzione e la relativa asserzione sono incluse.

Il recupero della quarantena era già previsto: riesamina la copia completa, confronta la sorgente, crea un nuovo file, verifica SHA-256 e pubblica senza rinominare il vecchio .part. Le prove non abbassano i requisiti di integrità né trasformano tutte le ambiguità in successi.

## Limiti residui

La copia reale con rc.12 deve ancora essere completata dall'utente. Le 29 strutture sono state verificate localmente, ma non è stata ripetuta la lettura dal telefono. Altri formati, cataloghi, album, cloud, profili JPEG non supportati e varianti non riconosciute restano soggetti ai limiti precedenti. Non è una garanzia universale di compatibilità o di assenza di bug.
