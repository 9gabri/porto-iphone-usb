# Prove della candidata 0.1.0-rc.3

Windows 11 x64 (build 26200), .NET SDK 10.0.403 e runtime Microsoft 10.0.14;
destinazioni locali NTFS. Dati generati nel progetto. Nessun iPhone enumerato,
aperto o letto durante lo sviluppo. Nessun video o dato personale incluso.

## Risultati finali

**124/124 test superati in un'unica esecuzione finale: 120 ordinari e 4 estesi.**
Build dell'app: zero avvisi e zero errori. Comando riproducibile:
`scripts/dev.ps1 test -Large`. Risultati in [test-results.txt](test-results.txt).

Coperti: SHA-256 e rilettura locale, troncamento, dimensione incoerente,
corruzione, seconda lettura divergente, collisioni, percorsi ostili, lock
concorrenti, Live Photos ambigue, copie mancanti o alterate, disco pieno,
inventari parziali, errori isolati, disconnessioni, tentativi limitati,
cancellazione, inventario finale cambiato e identificatori variabili.

Le regressioni avversarie verificano il numero minimo di copie per video
byte-identici osservati insieme, anche dopo pause, alias inversi, catene di
identificatori e ricomparsa contemporanea. Una lettura corrente non completata
non può essere risolta da un vecchio fingerprint. Discordanze della seconda
lettura restano persistenti; il file locale viene verificato anche al momento
del riuso. Le riparazioni riuscite non lasciano errori storici falsamente aperti.
I debiti delle vecchie versioni privi di storia sufficiente restano incerti.

## Arresti, cataloghi e file esistenti

Terminazioni reali di processi sintetici con Exit(77) ai confini intento, flush,
verifica, Ready, rename, Complete e scrittura report. Dopo un crash, un inventario
vuoto conserva i lavori pendenti. Pubblicazione atomica di report e inventari,
senza sovrascrivere file preesistenti.

Ripresa provata con letture corte/disallineate, divergenza all'ultimo byte,
sorgente più corta, cancellazione prima dell'append, interruzioni ripetute e
spazio sufficiente solo al suffisso. File estranei intatti.

Controlli su hard link di lock, DB, journal e temporanei; schema/versione/
application ID SQLite, colonne generate/nascoste, trigger, journal estranei,
troncati e footer di super-journal. Migrazione di cataloghi legacy e recupero
di journal reali prodotti da processi sintetici interrotti. I file esterni di
riferimento restano invariati. Journal non riconoscibili sono conservati e
rifiutati. Non sono prove di reale perdita di alimentazione o autenticazione
crittografica di un catalogo sostituito manualmente.

## USB senza dispositivi reali

- Vtable native sintetiche: GetDevices, S_OK, S_FALSE, errore dopo allocazione,
  deallocazione, metadati e conclusione dell'inventario.
- Processi figli reali: timeout, payload parziale, cancellazione, heartbeat,
  JSON nullo/malformato, frame inattesi e progressi corrotti.
- Pipe comandi saturata: timeout e cancellazione coprono anche la scrittura.
- Morte del worker: il collegamento successivo richiede un inventario nuovo.
- Tre video con errori seguiti da due sani attraverso un worker sintetico:
  i file sani vengono copiati e verificati, senza cambiare dispositivo.
- Cleanup dello stream senza mascherare errore originale o cancellazione;
  10.000 callback a conteggio zero con progressi limitati.
- Smoke COM: registrazione, GUID, stringhe e UInt64 oltre 4 GB. Gli smoke del
  pacchetto non invocano GetDevices/Open su hardware.

## Contenitori e clip codificate

Gerarchia moov/trak/mdia verificata con contenitori validi e malformati.
Tre brevi clip sintetiche MOV/M4V H.264 e MP4 HEVC, generate con FFmpeg già
disponibile, sono state decodificate localmente e importate con verifica
approfondita e confronto degli hash. Dettagli in
`tests/Porto.Tests/Fixtures/README.md`. FFmpeg non viene distribuito né richiesto
da Porto. Queste prove non certificano ogni codec o variante di file iPhone.

## Prove di scala

| Prova | Operazione reale | Limite |
|---|---|---|
| 4.294.967.313 byte | Generati, letti e sottoposti a SHA-256 verso Stream.Null | Nessun USB/disco |
| 50.000 video, 250 GB logici | Inventario, pendenti, capacità insufficiente, report | Nessun payload da 250 GB scritto |
| 50.000 elementi, 2.000 video | 2.000 file scritti/verificati, 48.000 foto escluse; secondo passaggio senza copie aggiuntive | File piccoli sintetici |
| 4.294.971.392 byte su NTFS | Contenitore esteso, copia, flush, hash indipendente, parser e pubblicazione | Contenitore sintetico, nessun USB |

Misure: flusso virtuale 2,40 s; inventario logico 3,48 s; 2.000 copie e secondo
passaggio 296,07 s; file NTFS oltre 4 GB 8,47 s. Working set campionato: 73, 221,
184 e 113 MiB, non una misura del picco. Non sono benchmark USB. Il solo payload
sintetico da 4 GB viene rimosso dopo la verifica, in una destinazione nuova dedicata.

## Revisione, prove negative e interfaccia

Revisori indipendenti su contabilità, USB e storage, seguiti da correzioni e
riesami con contesto nuovo. Riscontri e confini in [REVIEW-RC3.md](REVIEW-RC3.md).

Tre regressioni falliscono sulla sorgente rc.2 compilata in una cartella separata:
falsa completezza dopo alias inversi, errore di riparazione persistente e gerarchia
media non valida. Una mutazione isolata del confronto sul numero di copie fa
fallire la regressione pertinente. Nessuna mutazione nella sorgente distribuita.

App compilata avviata in modalità UI senza USB; ispezione visiva normale e
ridotta con messaggio lungo di riconnessione. Azioni ancorate al fondo e controllo
automatico dei loro limiti visibili. Non certifica ogni risoluzione/DPI o tutte
le interazioni manuali. Smoke e verifica del download pubblico sono registrati
separatamente in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).

## Da verificare con hardware

Riconoscimento di uno specifico iPhone/iOS, driver Apple, cavo, autorizzazione,
risorse effettive e velocità USB. La causa della lista vuota della prima prova
non è dimostrata. Nessun trasferimento reale da 250 GB. Copertura iCloud,
originalità Foto e Live Photos prive di indizi non certificabili. Rete, exFAT
e ARM restano rifiutati. Eseguibile non firmato. Nessuna promessa di zero bug.
