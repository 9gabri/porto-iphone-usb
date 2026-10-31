# Architettura e sicurezza

## Confini

Il processo UI usa WinForms e avvia un processo figlio dello stesso eseguibile con `--usb-worker`, stdin/stdout reindirizzati. Solo questo processo tocca WPD. COM in MTA, PortableDeviceFTM, `WPD_CLIENT_DESIRED_ACCESS=GENERIC_READ`, `GetStream(WPD_RESOURCE_DEFAULT, STGM_READ)`. Non esistono chiamate a Delete, SetValues, CreateObject, Write o comandi generici sul dispositivo. I setter Values preparano soltanto la struttura locale delle informazioni client.

Il worker risponde a un insieme chiuso di comandi: elenco/diagnosi, connessione, inventario, apertura, lettura, chiusura. Nessun comando shell o rete. Timeout di inattività per frame di 60 secondi; progressi tra i singoli oggetti rinnovano il timer, con emissione limitata a circa 4/s. Pausa/timeout terminano solo il figlio creato dall'app. Nessun arresto di processi Apple o di sistema. Fino a tre passaggi con 15 secondi d'attesa cancellabile; worker nuovi solo per lo stesso ID di dispositivo. Errori isolati proseguono con gli altri file; connessione indisponibile, disco pieno e accesso negato sospendono il passaggio.

GetDevices S_FALSE indica inventario troncato: tre tentativi limitati, poi errore, mai una lista completa silenziosamente. La diagnosi distingue zero dispositivi WPD, dispositivi non Apple e metadati insufficienti. Firme native, HRESULT, deallocazione e heartbeat sono provati anche con vtable COM sintetiche.

Inventario massimo un milione di oggetti tentati, profondità 128, 10.000 errori; ID vuoti/ripetuti interrompono l'inventario con esito incompleto. Proprietà non leggibili e tipi non esplorati sono errori registrati, non successi silenziosi. Il driver iPhone non viene considerato equivalente a un Android MTP.

## Transazione e pubblicazione

La destinazione contiene un lock con FileShare.None. SQLite di Windows usa journal DELETE e synchronous FULL, query parametrizzate. I file hanno UUID interni indipendenti dai nomi sorgente. Stati:

`Copying → Ready → Complete`, oppure `Quarantined`, `Missing`, `Damaged`. Le riparazioni collegano l'intento alla copia mancante con `Replaces`: nuova copia e risoluzione `Replaced` si salvano nella stessa transazione. Un tentativo di riparazione perso o corrotto diventa `Abandoned`, conservando un solo elemento logico da recuperare.

1. Registra intento Copying; crea un nuovo `.part` con CreateNew nello stesso volume oppure riapre esclusivamente un temporaneo Copying appartenente al catalogo. Legge USB dall'inizio e confronta ogni byte esistente sotto lock prima dell'append. Nessun troncamento o riscrittura del prefisso. Divergenze conservano il temporaneo come Abandoned; il nuovo tentativo è separato.
2. Copia a buffer 1 MiB, SHA-256, contatori Int64. Controllo spazio prima di ogni lettura/scrittura, riserva 64 MiB e controllo preventivo dei volumi noti.
3. Flush(true), chiusura, hash di rilettura locale. Confronto dimensioni quando dichiarate; discrepanze causano pausa anche se il driver ha fornito una dimensione sbagliata.
4. Verifica sorgente opzionale e classificazione contenitore. Il parser non decodifica i frame; controlla limiti degli atom, traccia `vide`, `moov` e `mdat`, con limiti a profondità, numero di atom e metadati.
5. Salva Ready con hash, lunghezza e percorso finale; esegue MoveFileEx senza REPLACE_EXISTING e con WRITE_THROUGH; salva Complete.

Rinomina e catalogo non sono un'unica transazione. Alla ripresa, Ready verifica il finale già esistente oppure il temporaneo prima di completare. Una collisione con contenuto diverso conserva il file estraneo e registra un nuovo percorso prima di rinominare il temporaneo verificato. Un temporaneo danneggiato è marcato Damaged e può essere ricopiato. I file completi vengono controllati per presenza; i candidati riutilizzati vengono anche rihashati. Nessuna rimozione automatica.

Le garanzie dipendono dal filesystem e dal dispositivo di archiviazione che rispettano flush/atomicità. Non è stata simulata una reale perdita di alimentazione o corruzione del volume. Non è una difesa contro un processo locale ostile che alteri la destinazione durante l'importazione. Il catalogo non deve essere modificato manualmente.

## Identità e molteplicità

`work.db` registra transazionalmente tutti i candidati prima della copia, con esiti Pending, Missing, Ambiguous ed Error che sopravvivono ai passaggi. Migrazione conservativa di Copying/Quarantined da rc.1. Una chiave descrittiva serve alla contabilità, mai a saltare il confronto dei contenuti. ID cambiati prima della verifica possono lasciare debito storico esplicito. Una copia verificata reclamata viene associata al descrittore corrente prima della riconciliazione: non si cancella il debito di un altro elemento presente nello stesso inventario. Nessun vero originale Foto viene certificato da questa contabilità.

Due inventari sono confrontati indipendentemente dall'ordine, includendo la molteplicità. Un secondo inventario diverso o con errori provoca un altro passaggio limitato, senza dichiarare il lavoro completo. Non è uno snapshot atomico del telefono. Le copie con la stessa dimensione non causano una lettura USB preventiva quando non ci sono più copie locali reclamabili. Riconciliazione e riparazione usano code per hash/lunghezza, senza ricerche quadratiche per video identici.

Gli ID WPD non sono chiavi sufficienti per la deduplicazione. Per possibili copie precedenti, si legge tutta la sorgente, si confrontano SHA-256 e lunghezza e si verifica la copia locale. Ogni record può essere reclamato una sola volta per inventario; nuove copie sono immediatamente reclamate. Tre elementi identici nell'inventario producono tre file, non uno. Il confronto considera anche temporanei ambigui già verificati, per non moltiplicarli alle riprese.

La molteplicità è per cartella di destinazione, non un'identità Foto attestata da Apple. Un cambio di iPhone/libreria nella stessa destinazione può riutilizzare contenuti identici: usare una cartella per libreria. Nessuna deduplicazione di file non presenti nel catalogo.

## Percorsi e dati

Nomina locale sanificata, prefisso V_, suffisso UUID; estensione ammessa o dedotta dal contenitore. Le stringhe USB non diventano percorsi. Catalogo con traversal o percorsi assoluti rifiutato; junction/symlink e destinazioni non NTFS/rete rifiutate. Processo senza elevazione. Il catalogo e i report contengono nomi e identificatori solo locali: non pubblicarli senza controllarli.

Nessuna telemetria, aggiornamento automatico, account, credenziale o upload implementato nell'app. Le dipendenze native vengono cercate in System32. L'eseguibile usa librerie runtime presenti nel pacchetto; preservare l'integrità dell'intera cartella estratta.
