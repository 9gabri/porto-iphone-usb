# Architettura e sicurezza

## Confini

Il processo UI usa WinForms e avvia un processo figlio dello stesso eseguibile con `--usb-worker`, stdin/stdout reindirizzati. Solo questo processo tocca WPD. COM in MTA, PortableDeviceFTM, `WPD_CLIENT_DESIRED_ACCESS=GENERIC_READ`, `GetStream(WPD_RESOURCE_DEFAULT, STGM_READ)`. Non esistono chiamate a Delete, SetValues, CreateObject, Write o comandi generici sul dispositivo. I setter Values preparano soltanto la struttura locale delle informazioni client.

Il worker risponde a un insieme chiuso di comandi: elenco, connessione, inventario, apertura, lettura, chiusura. Nessun comando shell o rete. Timeout di inattività per frame di 60 secondi; i batch di inventario rinnovano il timer. Pausa/timeout terminano solo il figlio creato dall'app. Non si tenta di uccidere processi Apple o di sistema. Un tentativo per esecuzione, nessun ciclo illimitato di riconnessioni. Riprendi è un nuovo tentativo esplicito.

Inventario massimo un milione di oggetti tentati, profondità 128, 10.000 errori; ID vuoti/ripetuti interrompono l'inventario con esito incompleto. Proprietà non leggibili e tipi non esplorati sono errori registrati, non successi silenziosi. Il driver iPhone non viene considerato equivalente a un Android MTP.

## Transazione e pubblicazione

La destinazione contiene un lock con FileShare.None. SQLite di Windows usa journal DELETE e synchronous FULL, query parametrizzate. I file hanno UUID interni indipendenti dai nomi sorgente. Stati:

`Copying → Ready → Complete`, oppure `Quarantined`, `Missing`, `Damaged`. Le riparazioni collegano l'intento alla copia mancante con `Replaces`: nuova copia e risoluzione `Replaced` si salvano nella stessa transazione. Un tentativo di riparazione perso o corrotto diventa `Abandoned`, conservando un solo elemento logico da recuperare.

1. Registra intento Copying; crea un nuovo `.part` con CreateNew nello stesso volume.
2. Copia a buffer 1 MiB, SHA-256, contatori Int64. Controllo spazio prima di ogni lettura/scrittura, riserva 64 MiB e controllo preventivo dei volumi noti.
3. Flush(true), chiusura, hash di rilettura locale. Confronto dimensioni quando dichiarate; discrepanze causano pausa anche se il driver ha fornito una dimensione sbagliata.
4. Verifica sorgente opzionale e classificazione contenitore. Il parser non decodifica i frame; controlla limiti degli atom, traccia `vide`, `moov` e `mdat`, con limiti a profondità, numero di atom e metadati.
5. Salva Ready con hash, lunghezza e percorso finale; esegue MoveFileEx senza REPLACE_EXISTING e con WRITE_THROUGH; salva Complete.

Rinomina e catalogo non sono un'unica transazione. Alla ripresa, Ready verifica il finale già esistente oppure il temporaneo prima di completare. Una collisione con contenuto diverso conserva il file estraneo e registra un nuovo percorso prima di rinominare il temporaneo verificato. Un temporaneo danneggiato è marcato Damaged e può essere ricopiato. I file completi vengono controllati per presenza; i candidati riutilizzati vengono anche rihashati. Nessuna rimozione automatica.

Le garanzie dipendono dal filesystem e dal dispositivo di archiviazione che rispettano flush/atomicità. Non è stata simulata una reale perdita di alimentazione o corruzione del volume. Non è una difesa contro un processo locale ostile che alteri la destinazione durante l'importazione. Il catalogo non deve essere modificato manualmente.

## Identità e molteplicità

Gli ID WPD non sono chiavi sufficienti per la deduplicazione. Per possibili copie precedenti, si legge tutta la sorgente, si confrontano SHA-256 e lunghezza e si verifica la copia locale. Ogni record può essere reclamato una sola volta per inventario; nuove copie sono immediatamente reclamate. Tre elementi identici nell'inventario producono tre file, non uno. Il confronto considera anche temporanei ambigui già verificati, per non moltiplicarli alle riprese.

La molteplicità è per cartella di destinazione, non un'identità Foto attestata da Apple. Un cambio di iPhone/libreria nella stessa destinazione può riutilizzare contenuti identici: usare una cartella per libreria. Nessuna deduplicazione di file non presenti nel catalogo.

## Percorsi e dati

Nomina locale sanificata, prefisso V_, suffisso UUID; estensione ammessa o dedotta dal contenitore. Le stringhe USB non diventano percorsi. Catalogo con traversal o percorsi assoluti rifiutato; junction/symlink e destinazioni non NTFS/rete rifiutate. Processo senza elevazione. Il catalogo e i report contengono nomi e identificatori solo locali: non pubblicarli senza controllarli.

Nessuna telemetria, aggiornamento automatico, account, credenziale o upload implementato nell'app. Le dipendenze native vengono cercate in System32. L'eseguibile usa librerie runtime presenti nel pacchetto; preservare l'integrità dell'intera cartella estratta.
