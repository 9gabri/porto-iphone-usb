# Prove della candidata 0.1.0-rc.2

Windows 11 x64, .NET SDK 10.0.403 e runtime Microsoft 10.0.14; destinazioni locali NTFS.
Dati generati nel progetto. Nessun iPhone enumerato, aperto o letto durante lo sviluppo.
Nessun video o dato personale incluso nei test.

## Risultati

**74 casi verificati: 70 ordinari e 4 estesi**, eseguiti in passaggi separati.
La suite estesa iniziale era 72/72; aggiunti due casi di crash con inventario vuoto,
sono stati rieseguiti tutti i 70 ordinari dopo l'ultima correzione del protocollo.
Le quattro prove di scala esercitano il medesimo core. Log in test-results.txt.
Comando riproducibile: scripts/dev.ps1 test -Large.

Coperti: SHA-256 e rilettura locale, troncamento, dimensione incoerente, corruzione,
seconda lettura divergente, collisioni, percorsi ostili, lock concorrenti, foto
abbinate/Live Photos ambigue, copie mancanti o alterate, disco pieno, inventari
parziali, errori isolati, disconnessioni, tentativi limitati, cancellazione,
inventario finale cambiato, ID variabili e molteplicità degli elementi byte-identici.

Il test di molteplicità riproduce una pausa dopo che B ha reclamato la copia di A,
quando A è ancora nell'inventario corrente. Un successivo elenco ridotto non può
cancellare l'obbligo di conservare la seconda copia.

## Arresti e recupero

Terminazioni reali di processi sintetici con Exit(77) ai confini intento, flush,
verifica, Ready, rename e Complete. Riparazioni interrotte e collisioni preservano
i file esistenti. Dopo un crash, un inventario vuoto conserva i lavori pendenti,
anche se il file era pubblicato ma il risultato non ancora registrato: segnalazione
conservativa, non falso completamento.

Ripresa provata con letture corte/disallineate, divergenza all'ultimo byte, sorgente
più corta, cancellazione prima dell'append, interruzioni ripetute e spazio sufficiente
solo al suffisso. File estranei intatti. Nessuna cancellazione in produzione.

## USB senza dispositivi reali

- Vtable native sintetiche: vero percorso GetDevices, S_OK, S_FALSE, errore dopo
  allocazione, deallocazione e stringhe senza terminatore nel testo UI.
- Inventario nativo simulato: heartbeat tra metadati e conclusione al batch S_FALSE.
- Processi figli reali: timeout, payload parziale, cancellazione, heartbeat oltre
  il timeout, JSON nullo/malformato, frame inattesi e progressi corrotti.
- Il test dei progressi malformati invia poi un risultato valido: deve fallire
  immediatamente, non passare accidentalmente alla chiusura del processo.
- COM smoke: registrazione, GUID, stringhe e UInt64 oltre 4 GB. Gli smoke del
  pacchetto non invocano GetDevices/Open su hardware.

## Prove di scala

| Prova | Operazione reale | Limite |
|---|---|---|
| 4.294.967.313 byte | Generati, letti e sottoposti a SHA-256 verso Stream.Null | Nessun USB/disco |
| 50.000 video, 250 GB logici | Inventario, pendenti, capacità insufficiente, report | Nessun payload da 250 GB scritto |
| 50.000 elementi, 2.000 video | 2.000 file scritti/verificati, 48.000 foto escluse; secondo passaggio senza copie aggiuntive | File piccoli sintetici |
| 4.294.971.392 byte su NTFS | Contenitore esteso, copia, flush, hash indipendente, parser e pubblicazione | Nessun codec, video reale o USB |

Misure: flusso virtuale 2,25 s; inventario logico 1,03 s; 2.000 copie e secondo
passaggio 214,05 s; file NTFS oltre 4 GB 8,72 s. Working set campionato: 79, 210,
158 e 110 MiB, non una misura del picco. Non sono benchmark USB. Il solo payload
sintetico da 4 GB viene rimosso dopo la verifica, in una destinazione nuova dedicata.

## Revisione, mutazione e interfaccia

Due revisioni indipendenti: core e USB, seguite da correzioni e seconda revisione.
Dettagli in REVIEW-RC2.md. Mutazione temporanea del confronto del prefisso: la
regressione fallisce; codice ripristinato prima della build finale. Nessun codice
mutato distribuito.

Pacchetto autonomo avviato in modalità UI senza USB; ispezione visiva normale e
ridotta con messaggio lungo di riconnessione. Azioni ancorate al fondo e controllo
automatico dei loro limiti visibili. Non certifica ogni risoluzione/DPI o tutte
le interazioni manuali.

## Da verificare con hardware

Riconoscimento di uno specifico iPhone/iOS, driver Apple, cavo, autorizzazione,
risorse effettive e velocità USB. La causa della lista vuota della prima prova
non è dimostrata. Nessun trasferimento reale da 250 GB. Copertura iCloud,
originalità Foto e Live Photos prive di indizi non certificabili. Rete, exFAT
e ARM restano rifiutati. Eseguibile non firmato.
