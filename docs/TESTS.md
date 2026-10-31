# Prove della candidata 0.1.0-rc.4

Windows 11 x64 (build 26200), .NET SDK 10.0.403 e runtime Microsoft 10.0.14;
destinazioni locali NTFS. Dati generati nel progetto. Nessun iPhone enumerato,
aperto o letto durante lo sviluppo. Nessun video o dato personale incluso.

## Risultati finali

**192 casi distinti verificati: 188 ordinari e quattro estesi.**
I risultati provengono da due esecuzioni sulla stessa logica finale: quattro
prove estese e successiva suite ordinaria 188/188. Nella prima esecuzione un
vecchio test si aspettava un report dopo il rifiuto di un hard link: ora il
preflight rifiuta il catalogo prima di leggere la sorgente. Aggiornata solo
l'aspettativa del test, verificando anche che catalogo e file estraneo restino
immutati; poi rieseguiti tutti i test ordinari. Non è un singolo run 192/192.
Build dell'app: zero avvisi e zero errori. Comandi riproducibili:
`scripts/dev.ps1 test` e `scripts/dev.ps1 test -Large`.
Risultati consolidati in [test-results.txt](test-results.txt).

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

La rc.4 aggiunge 68 casi: verifica delle copie storiche anche se la sorgente
è assente, osservazioni Unknown persistenti, errori COM di sessione distinti
dagli errori del file, blocchi con metadati uguali e in gruppi diversi, percorsi
e contenuti di catalogo ostili, preflight fisico, cleanup degli snapshot,
compatibilità con i nomi generati dalle versioni precedenti e preferenze con
collegamenti. Nessuna identità viene dedotta soltanto da nome o dimensione.

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

Nella rc.4 il catalogo viene controllato integralmente prima del recupero:
stati, tipi SQLite, NUL incorporati, JSON, UUID, percorsi, hash, finali duplicati,
relazioni cicliche e tutti i collegamenti fisici. Provati schema reale a sette
e otto colonne, CHECK e indici con semantica estranea, journal con riga tardiva
ostile, pulizia dei soli snapshot creati dalla verifica e catene valide.
I test delle preferenze usano percorsi iniettati nelle cartelle sintetiche,
senza leggere o scrivere il profilo dell'utente.

## USB senza dispositivi reali

- Vtable native sintetiche: GetDevices, S_OK, S_FALSE, errore dopo allocazione,
  deallocazione, metadati e conclusione dell'inventario.
- Processi figli reali: timeout, payload parziale, cancellazione, heartbeat,
  JSON nullo/malformato, frame inattesi e progressi corrotti.
- Pipe comandi saturata: timeout e cancellazione coprono anche la scrittura.
- Morte del worker: il collegamento successivo richiede un inventario nuovo.
- Tre video con errori seguiti da due sani attraverso un worker sintetico:
  i file sani vengono copiati e verificati, senza cambiare dispositivo.
- Primo Open/Read bloccato, perdita nativa della sessione e ID rimappati:
  il tentativo successivo richiede un nuovo inventario e prova prima gli altri.
  Coperti gruppi omonimi con uno o due blocchi, anche in gruppi diversi; tutte
  le occorrenze restano candidate. Più blocchi o riordini possono esaurire i tre
  tentativi: il risultato deve rimanere incompleto.
- Cleanup dello stream senza mascherare errore originale o cancellazione;
  10.000 callback a conteggio zero con progressi limitati.
- Smoke COM: registrazione, GUID, stringhe e UInt64 oltre 4 GB. Gli smoke del
  pacchetto non invocano GetDevices/Open su hardware.

## Contenitori e clip codificate

Gerarchia moov/trak/mdia verificata con contenitori validi e malformati.
Tre brevi clip sintetiche MOV/M4V H.264 e MP4 HEVC, generate e decodificate
localmente durante rc.3 con FFmpeg già disponibile, sono state nuovamente
importate nella suite rc.4 con verifica approfondita e confronto degli hash.
La decodifica non è stata ripetuta in questo ciclo. Dettagli in
`tests/Porto.Tests/Fixtures/README.md`. FFmpeg non viene distribuito né richiesto
da Porto. Queste prove non certificano ogni codec o variante di file iPhone.

## Prove di scala

| Prova | Operazione reale | Limite |
|---|---|---|
| 4.294.967.313 byte | Generati, letti e sottoposti a SHA-256 verso Stream.Null | Nessun USB/disco |
| 50.000 video, 250 GB logici | Inventario, pendenti, capacità insufficiente, report | Nessun payload da 250 GB scritto |
| 50.000 elementi, 2.000 video | 2.000 file scritti/verificati, 48.000 foto escluse; secondo passaggio senza copie aggiuntive | File piccoli sintetici |
| 4.294.971.392 byte su NTFS | Contenitore esteso, copia, flush, hash indipendente, parser e pubblicazione | Contenitore sintetico, nessun USB |

Misure rc.4: flusso virtuale 2,42 s; inventario logico 3,84 s; 2.000 copie e secondo
passaggio 316,11 s; file NTFS oltre 4 GB 12,47 s. Working set campionato: 71, 209,
189 e 126 MiB, non una misura del picco. Non sono benchmark USB. Il solo payload
sintetico da 4 GB viene rimosso dopo la verifica, in una destinazione nuova dedicata.

## Revisione, prove negative e interfaccia

Revisori indipendenti su contabilità, USB e storage, seguiti da correzioni e
riesami con contesto nuovo, massimo tre cicli per area. Riscontri e confini in
[REVIEW-RC4.md](REVIEW-RC4.md). Le revisioni precedenti rimangono disponibili.

Le regressioni sono state eseguite prima delle rispettive correzioni. Il ciclo
finale ha riprodotto due casi di monopolizzazione dei tentativi con gruppi diversi
di file bloccati. Una copia isolata dei sorgenti finali è stata poi mutata
invertendo la priorità di RetryOrder: entrambi i test falliscono sull'asserzione
di copia dei video sani (0/2), non per problemi di compilazione o predisposizione.
Nessuna mutazione nella sorgente distribuita.

Quattro prove separate sullo script di confezionamento verificano compressione
interrotta, ZIP non valido, conservazione di archivio preesistente e successo.
Il nome definitivo viene occupato solo dopo la verifica; ogni tentativo ha il
proprio checksum. Riproduzione: `tests/PackageStagingTests.ps1`.
Queste quattro prove non sono incluse nei 192 casi dell'app.

App compilata avviata in modalità UI senza USB; ispezione visiva normale e
ridotta con messaggio lungo di riconnessione, più stato di pausa a copia parziale.
La stima non rimane attiva dopo la pausa. Azioni ancorate al fondo e controllo
automatico dei loro limiti visibili. Non certifica ogni risoluzione/DPI o tutte
le interazioni manuali. Smoke e verifica del download pubblico sono registrati
separatamente in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).

## Da verificare con hardware

Riconoscimento di uno specifico iPhone/iOS, driver Apple, cavo, autorizzazione,
risorse effettive e velocità USB. La causa della lista vuota della prima prova
non è dimostrata. Nessun trasferimento reale da 250 GB. Copertura iCloud,
originalità Foto e Live Photos prive di indizi non certificabili. Rete, exFAT
e ARM restano rifiutati. Eseguibile non firmato. Nessuna promessa di zero bug.
