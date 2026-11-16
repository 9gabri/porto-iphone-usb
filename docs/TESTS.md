# Prove della candidata 0.1.0-rc.7

Windows 11 x64, SDK .NET 10.0.403 e runtime 10.0.14; cartelle NTFS isolate.
Nessun telefono enumerato o aperto e nessun video personale letto durante
questo ciclo. Diagnosi dai soli report e inventari autorizzati.

## Risultati finali

**269 verifiche superate: 265 ordinarie e quattro estese, in due esecuzioni.**
Nel primo run completo un vecchio test imponeva il riuso del worker dopo errore.
Aggiornata quell'aspettativa alla sostituzione rc.7 (numero esatto e limitato
di connessioni), sono stati rieseguiti tutti i test ordinari: 265/265.
I quattro test estesi sono passati nel primo run sullo stesso codice
applicativo. Nessun test escluso né fallimento residuo ignorato.

Build: zero avvisi/errori. Riproduzione: scripts/dev.ps1 test -Large;
senza -Large vengono eseguiti i 265 test ordinari.
Output: [test-results.txt](test-results.txt).

16 nuovi test IPC verificano errori di apertura, flussi troppo lunghi/corti,
hash discordanti a lunghezza uguale, cinque file difettosi seguiti da tre sani,
inventari riordinati/cambiati/incompleti, molteplicità, nuovi elementi finali,
percorso sano, cancellazione, worker non avviabile, Dispose tardivo e
osservazioni estranee. Le copie sane sono confrontate con i byte attesi.
Le prime 14 prove sulla rc.6 isolata danno 1/14: falliscono 13 regressioni,
passa il percorso sano. Mutazione che ignora l'inventario nuovo: 0/1 dopo
compilazione riuscita. Revisione indipendente senza difetti bloccanti
confermati; [dettagli e limiti](REVIEW-RC7.md).
Tutti i precedenti 249 test ordinari e quattro estesi restano nella suite.

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
importate nella suite rc.7 con verifica approfondita e confronto degli hash.
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

Misure del run rc.7 (working set campionato, non picco):

```text
LARGE stream: 4294967313 bytes, 2,56s, working set 65 MiB
LARGE inventory: 50000 items, 250 GB logical, 4,48s, working set 221 MiB
STRESS full loop: 2000 physical files twice verified, 50000 entries, 407,41s, working set 205 MiB
LARGE NTFS: 4294971392 bytes written and reread, 25,78s, working set 87 MiB
```

Non sono benchmark USB. Il solo payload sintetico da 4 GB viene rimosso dopo
la verifica, in una destinazione nuova dedicata. Nessun file della prova reale
è stato modificato o cancellato.

## Revisione, prove negative e interfaccia

La revisione rc.6 conservata copre ABI, stream e recupero, con controesempi riprodotti
e riesame finale. La failure differita del cleanup non deve essere attribuita
al video successivo ancora da aprire: il controesempio iniziale impediva la copia
di B/C/D; dopo la correzione tutti e tre vengono copiati esattamente una volta,
mentre A resta irrisolto. Riscontri in [REVIEW-RC6.md](REVIEW-RC6.md).

Prima delle correzioni: lifetime 0/4, nativi 1/6, motivi ambiguità 0/3, protocollo
cleanup 0/1 e recupero 0/2. Una copia isolata dei sorgenti finali è stata mutata
invertendo la condizione di Cancel: il test fallisce perché B riceve i byte di A
(0/1), non per errori di compilazione. Sorgenti distribuiti non mutati.

Quattro prove separate sullo script di confezionamento verificano compressione
interrotta, ZIP non valido, conservazione di archivio preesistente e successo.
Il nome definitivo viene occupato solo dopo la verifica; ogni tentativo ha il
proprio checksum. Riproduzione: `tests/PackageStagingTests.ps1`.
Queste quattro prove non sono incluse nei 269 casi dell'app.

App compilata avviata senza USB in quattro stati: normale, ridotta con
messaggio di riconnessione lungo, ridotta in pausa e ridotta con esito incompleto.
Tutte le immagini ispezionate visivamente. Controllati automaticamente pulsanti
visibili, stima rimossa dopo pausa, contatori distinti, barra azzerata e callback
tardiva che non può sovrascrivere il risultato finale. Il contenuto scorre quando
la finestra è ridotta. Non certifica ogni DPI o tutte le interazioni manuali.
Smoke e verifica del download pubblico sono registrati separatamente in
[RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).

## Da verificare con hardware

La rc.7 richiede una normale importazione sullo specifico iPhone per confermare
il recupero degli errori reali. La catena di letture contaminate è riprodotta
su processi sintetici; la causa fisica non è dimostrata dai soli metadati.
Usare la stessa destinazione rc.5/rc.6 conservando tutti i file.

Con inventario stabile si sostituisce il worker prima della lettura successiva.
Se ogni riconnessione cambia gli identificativi, oppure persistono timeout
e disconnessioni, i tre passaggi esterni possono non bastare: gli elementi
irrisolti restano dichiarati. Nessuna falsa riuscita e nessun retry infinito.
La differenza rispetto a Foto, le Live Photo, le varianti e gli originali
solo iCloud non sono riconciliati. Nessuna prova fisica da 250 GB o verifica
degli altri dati iOS. Rete, exFAT e ARM non supportati. Eseguibile non firmato.
