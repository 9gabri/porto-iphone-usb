# Prove della candidata 0.1.0-rc.5

Windows 11 x64 (build 26200), .NET SDK 10.0.403 e runtime Microsoft 10.0.14;
destinazioni locali NTFS. Dati generati nel progetto. Durante questo ciclo
nessun iPhone enumerato, aperto o letto automaticamente. La diagnosi parte da
report del collaudo manuale rc.4; nessun video personale aperto o incluso.
La destinazione reale resta in sola lettura: nessuna correzione dei suoi file.

## Risultati finali

**233/233 in un'unica esecuzione finale: 229 ordinari e quattro estesi.**
Build dell'app: zero avvisi e zero errori. Comando riproducibile:
`scripts/dev.ps1 test -Large`; senza `-Large` esegue i 229 test ordinari.
Output sintetico, privo dei percorsi del computer, in [test-results.txt](test-results.txt).
I 41 nuovi casi comprendono 11 regressioni dell'engine sulle dimensioni,
14 prove COM native, nove scambi IPC con processi figli e sette prove di
classificazione e conteggi dell'engine. Le quattro prove visuali sono separate.

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

Sono mantenuti i 68 casi introdotti dalla rc.4: verifica delle copie storiche anche se la sorgente
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

Dalla rc.4 il catalogo viene controllato integralmente prima del recupero:
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
importate nella suite rc.5 con verifica approfondita e confronto degli hash.
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

Misure del run rc.5 (working set campionato, non picco):

```text
LARGE stream: 4294967313 bytes, 3,22s, working set 69 MiB
LARGE inventory: 50000 items, 250 GB logical, 8,54s, working set 218 MiB
STRESS full loop: 2000 physical files twice verified, 50000 entries, 419,84s, working set 211 MiB
LARGE NTFS: 4294971392 bytes written and reread, 23,54s, working set 126 MiB
```

Non sono benchmark USB. Il solo payload sintetico da 4 GB viene rimosso dopo
la verifica, in una destinazione nuova dedicata. Nessun file della prova reale
è stato modificato o cancellato.

## Revisione, prove negative e interfaccia

Revisori indipendenti su classificazione/interfaccia e USB/integrità, con
riesame delle correzioni. Un difetto di riuso trovato nel riesame è stato
riprodotto con due importazioni e corretto. Riscontri e limiti in
[REVIEW-RC5.md](REVIEW-RC5.md); le revisioni precedenti rimangono disponibili.

Le regressioni sulle dimensioni e sugli AAE sono state eseguite prima delle
correzioni. Ricostruita anche rc.4 in isolamento per provare il difetto di EOF
con una vtable nativa sintetica. Una copia dei sorgenti rc.5 è stata mutata
invertendo il riconoscimento di S_FALSE: il test di EOF fallisce (0/1) sulla
lettura aggiuntiva al driver, senza errori di compilazione. Nessuna mutazione
nei sorgenti distribuiti.

Quattro prove separate sullo script di confezionamento verificano compressione
interrotta, ZIP non valido, conservazione di archivio preesistente e successo.
Il nome definitivo viene occupato solo dopo la verifica; ogni tentativo ha il
proprio checksum. Riproduzione: `tests/PackageStagingTests.ps1`.
Queste quattro prove non sono incluse nei 233 casi dell'app.

App compilata avviata senza USB in quattro stati: normale, ridotta con
messaggio di riconnessione lungo, ridotta in pausa e ridotta con esito incompleto.
Tutte le immagini ispezionate visivamente. Controllati automaticamente pulsanti
visibili, stima rimossa dopo pausa, contatori distinti, barra azzerata e callback
tardiva che non può sovrascrivere il risultato finale. Il contenuto scorre quando
la finestra è ridotta. Non certifica ogni DPI o tutte le interazioni manuali.
Smoke e verifica del download pubblico sono registrati separatamente in
[RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).

## Da verificare con hardware

Riconoscimento dello specifico iPhone/iOS, driver Apple, cavo, autorizzazione,
stabilità delle risorse e velocità USB. La rc.4 è stata provata manualmente e
ha prodotto errori: i suoi report non dimostrano la causa di ogni discrepanza
o HRESULT. Le correzioni sintetiche non provano la risoluzione completa sul
dispositivo. Richiesto un nuovo collaudo rc.5 in cartella vuota distinta,
confrontando report e video autonomi attesi senza cancellare la vecchia prova.

Nessun trasferimento reale da 250 GB. Copertura iCloud, originalità Foto e
Live Photos prive di indizi non certificabili. Le risorse sospette rimangono
ambigue; i vecchi debiti Unknown non vengono cancellati retroattivamente per
sola estensione. Nessuna verifica dello stato di Note o degli altri dati iOS.
Rete, exFAT e ARM restano rifiutati. Eseguibile non firmato. Nessuna promessa
di zero bug.
