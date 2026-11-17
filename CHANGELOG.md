# Novità

## 0.1.0-rc.22

- Eliminata la seconda copia completa del catalogo durante la verifica SQLite: acquisizione, WAL e lettura sola condividono un buffer RAM con rilascio deterministico e azzeramento.
- Riprodotto il blocco con i valori della schermata: 3.371.982.864 byte richiesti contro 2.641.379.328 disponibili. Con la nuova gestione servono inizialmente 1.954.426.904 byte, mantenendo il margine di 512 MiB e il controllo della capacità di allocazione.
- Conservati padding protettivo, integrità SQLite/WAL, confronto integrale di due letture, timeout, cancellazione, contatori e ripresa. Crescita WAL ancora verificata separatamente.
- Verifiche sintetiche su catalogo oltre 1,42 GB e letture consecutive. Nessuna promessa su schema iOS, intera libreria o durata reale del trasferimento. Nessuna modifica ai file delle prove.

## 0.1.0-rc.21

- Catalogo iPhone: limite DB+WAL da 1 GiB a 2047 MiB, sotto i limiti degli array e della copia nativa con padding.
- Budget iniziale di RAM e capacità di allocazione include anche la successiva copia di verifica. Crescita del WAL e copia SQLite mantengono controlli dedicati; nessun uso illimitato della memoria.
- Riprodotto il DB-LIMIT con le dimensioni riportate. Verificato un catalogo sintetico valido da 1.426.288.640 byte con WAL da 7.337.752 byte e letture consecutive complete. Non è una prova sull'iPhone reale.
- Nessuna modifica al telefono, ai file delle prove, ai controlli d'integrità, all'autenticazione o al motore di copia. Nessuna promessa di un'ora o compatibilità universale.

## 0.1.0-rc.20

- Richieste Apple AFC fino a 512 KiB, mantenendo l'opzione esplicita di compatibilità a 64 KiB e tutti i controlli del protocollo.
- Benchmark riproducibile con tre ripetizioni per variante e sei guasti sintetici a 256 e 512 KiB. Nel caso dominato dall'attesa simulata, mediana 1.008,80 -> 504,85 ms per 16 MiB. Nessuna equivalenza con la velocità dell'iPhone reale.
- Escluso un prototipo che evitava confronti fra dimensioni diverse: meno I/O e CPU, ma nessun guadagno dimostrato nel tempo totale sul PC di prova. Motore di copia rc.19 conservato.
- Nessuna modifica a hash, copie precedenti, molteplicità, cataloghi, autenticazione o protezioni. Candidata per misurare l'obiettivo 29 GB/ora; risultato hardware ancora da confermare.

## 0.1.0-rc.19

- Importazione galleria: riuso locale della prima lettura per il confronto dei nuovi file, evitando una seconda lettura completa della sorgente quando è disponibile spazio sufficiente.
- Richieste AFC fino a 256 KiB, con opzione esplicita da 64 KiB per compatibilità. Controlli di protocollo e autenticazione invariati.
- Contatori locali per tempi, letture, aperture e trasporto; etichetta della velocità chiarita come media complessiva.
- Rilettura locale SHA-256, verifica approfondita opzionale, selezione, molteplicità e salvataggi durevoli conservati. Nuove prove di crash, cancellazione, disco pieno e file grandi.
- Nessuna promessa sui tempi dell'iPhone reale; candidata per un confronto misurato. Dettagli in REVIEW-RC19.md.

## 0.1.0-rc.9

- Importazione da cartella esportata da Foto, senza Mac o componente iOS.
- Confronto completo SHA-256 con copie precedenti, anche non registrate nel catalogo.
- File già presenti riutilizzati senza spostamenti; copie non riutilizzate conservate e riportate, senza attribuirle automaticamente a Live Photo.
- Molteplicità dei file identici, errori storici e ripresa mantenuti.
- Sorgente locale in sola lettura, sovrapposizioni/link rifiutati, inventario finale stabile obbligatorio.
- Interfaccia e report distinguono cartella selezionata da USB e dall’intera libreria Foto.
- Esportazione reale da iPhone e confronto con archivio personale ancora da confermare.


## 0.1.0-rc.8

- Tentativo di tutte le risorse video USB, senza esclusioni basate solo su nome o foto omonima.
- Copie verificate aggiuntive separate e parser dei metadati temporizzati strutturato.
- Riclassificazione dei temporanei precedenti preservandoli, con nuova copia e recupero transazionale.
- Continuazioni USB limitate oltre tre passaggi quando raggiungono nuovi ostacoli; precedenza ai file non ancora raggiunti.
- Conteggi correnti distinti dalle osservazioni storiche; file precedenti ricontrollati senza spostarli.
- Prove sintetiche e revisione indipendente; recupero fisico e totale Foto ancora da confermare.


## 0.1.0-rc.7 — recupero su un processo USB nuovo

- Dopo un errore di lettura il video successivo viene aperto su un nuovo worker, previo controllo dell’intero inventario. Non si presume che un annullamento riuscito abbia ripristinato il driver.
- Dimensione errata o SHA-256 discordante invalidano anche flussi già arrivati a EOF.
- Inventario diverso o incompleto: video successivo ancora pendente, nessun uso degli identificativi precedenti e nessuna falsa conferma di copia.
- Recupero automatico con più file difettosi consecutivi e inventario stabile, mantenendo verifica locale, seconda lettura e conservazione dei file esistenti.
- Versione e numero dei riavvii USB nei report, senza cambiamenti ai cataloghi.

Il recupero dei video della prova reale resta da confermare. Se ogni
riconnessione cambia gli identificativi, oppure persistono timeout e
disconnessioni, possono non bastare i tre passaggi esterni. Restano i limiti
USB rispetto a Foto, Live Photo, varianti e contenuti solo iCloud.

## 0.1.0-rc.6 — isolamento delle letture incomplete e recupero

- Annullate sullo stesso handle WPD le operazioni incomplete prima del rilascio dello stream; coperto anche GetStream fallito. EOF normale non provoca cancellazioni.
- Una bonifica fallita invalida la sessione senza mascherare l'errore primario. Il video successivo non ancora aperto resta pendente; i file realmente falliti vengono rimandati dopo quelli sani nei passaggi successivi.
- Wrapper di lettura obsoleti rifiutati prima di inviare comandi al worker.
- Contatori espliciti sull'ultimo passaggio e motivi distinti per foto omonime e nomi IMG_E; nessuna esclusione nascosta o indebolimento delle verifiche.
- Recupero compatibile con la destinazione rc.5, conservando copie, report, cataloghi e temporanei.

Regressioni sintetiche e revisione indipendente non dimostrano la risoluzione
di tutti gli errori fisici. Conteggi rispetto a Foto ancora da riconciliare.
Un cleanup bloccato può esaurire i tre tentativi: l'esito rimane incompleto.

## 0.1.0-rc.5 — correzioni dopo la prova iPhone

- Rispettata la fine del flusso USB anche quando l'ultimo blocco contiene ancora dati. Nessuna ulteriore lettura del driver dopo S_FALSE; una lettura corta con S_OK continua normalmente.
- Verificata la dimensione della risorsa aperta, quando fornita dal driver. Se differisce dall'inventario, due letture USB concordanti diventano obbligatorie; troncamenti e contenuti discordanti restano errori.
- Evitate copie aggiuntive nei passaggi successivi quando la dimensione dell'inventario è inesatta. Restano il confronto SHA-256 e la rilettura della copia locale.
- File accessori AAE riconosciuti ed esclusi con motivazione, senza scartarli quando i metadati indicano invece un video. Altri elementi ignoti restano espliciti.
- Contatori separati per risorse USB, immagini, accessori, possibili Live Photo/modifiche e candidati alla copia. La barra non resta piena dopo un risultato incompleto.
- Diagnostica con byte attesi/ricevuti e posizione degli errori USB; protocollo worker verificato anche con dimensioni oltre 4 GB e risposte malformate.

La rc.4 ha fallito una prova manuale; queste correzioni sono verificate con dati
sintetici e richiedono una nuova prova fisica. Conservare la vecchia prova e usare
una cartella nuova per il collaudo rc.5. Nessuna modifica retroattiva ai suoi file.

## 0.1.0-rc.4 — verifica conclusiva e recupero dei file bloccati

- Un video che blocca la sessione viene rimandato dopo gli altri nei passaggi successivi. Il riordino gestisce anche nomi e dimensioni uguali, senza usarli come prova di identità; restano tre tentativi massimi.
- Gli errori nativi di connessione persa conservano il codice diagnostico e obbligano a un inventario nuovo. Un timeout durante la lettura non viene più attribuito al video successivo.
- Verificate anche le copie storiche il cui video non appare più sul telefono. Le osservazioni di tipo ignoto restano esplicite anche se scompaiono dagli inventari successivi.
- Cataloghi: controllati dati, percorsi, collegamenti, relazioni di sostituzione e definizioni SQL prima delle scritture; mantenuta la compatibilità con le strutture generate dalle candidate precedenti.
- Le copie temporanee usate per validare un journal vengono rimosse dopo l'uso, preservando sorgenti e file estranei.
- Le impostazioni non seguono collegamenti verso altri file. La stima del tempo viene rimossa quando il trasferimento si ferma.
- ZIP preparato e controllato nello staging prima di assegnargli il nome definitivo. Checksum separato per ogni tentativo, senza sovrascrivere gli artefatti precedenti.

La candidata resta non firmata e non validata con un iPhone fisico. Intera libreria
Foto/iCloud e originalità delle risorse USB non sono certificabili.

## 0.1.0-rc.3 — revisione avversaria

- Conservata la molteplicità storica dei video identici attraverso pause, cambi di ID, alias che ricompaiono insieme e variazioni dell'inventario finale.
- Recupero dei file danneggiati senza cancellarli e senza riproporre per sempre un errore già riparato.
- Sessione USB persa: nuovo inventario obbligatorio. Errori per file distinti da morte del worker; quelli sani proseguono.
- Timeout e Pausa coprono anche l'invio di comandi; progressi iniziali limitati per evitare code eccessive nell'interfaccia.
- Controlli dei collegamenti NTFS e della provenienza strutturale di cataloghi e journal; report e inventari pubblicati atomicamente.
- Gerarchia della traccia MOV/MP4/M4V verificata; prove anche con clip H.264 e HEVC generate e decodificate localmente.
- Pacchetto costruito in una cartella nuova per escludere residui delle build precedenti.

Restano da verificare iPhone, iOS, cavo e driver reali. La revisione non certifica
assenza di bug, riproducibilità di qualsiasi video o copertura dell'intera libreria.
I debiti legacy senza storia sufficiente e le identità con contenuti discordanti
rimangono esplicitamente incerti. Nessun nuovo servizio, acquisto o permesso.

## 0.1.0-rc.2 — revisione di affidabilità

### Corretto
- Ricerca USB: un elenco troncato non viene più accettato come completo; diagnosi distinta dei motivi di lista vuota.
- Inventario: progressi per singolo elemento evitano falsi timeout su telefoni lenti ma ancora responsivi.
- Errori di singoli video non impediscono la copia di quelli sani successivi.
- Video non ancora raggiunti e ambiguità precedenti non spariscono dai report successivi.
- Corretta la contabilità di elementi byte-identici dopo una pausa durante la riconciliazione.
- Evitate letture USB preventive inutili e ricerche quadratiche sulle copie riparabili.

### Aggiunto
- Fino a tre passaggi automatici con 15 secondi d'attesa cancellabile e collegamento allo stesso telefono.
- Ripresa con confronto integrale del prefisso e append, senza cancellare o sovrascrivere temporanei.
- Secondo inventario a fine passaggio e registro persistente dei candidati ancora da completare.
- Cartella ricordata localmente, richiesta temporanea contro la sospensione inattiva del PC e pulsanti sempre visibili.
- Test nativi con COM simulato, errori di protocollo, tentativi automatici, scala e file oltre 4 GB realmente scritto.

### Limiti
Nessuna validazione di trasferimento con iPhone fisico. Nessuna promessa di zero bug o intera libreria iCloud copiata.
La causa della lista vuota della prima prova non è dimostrata: il riconoscimento da parte di Windows era sconosciuto.
Eseguibile non firmato. La vecchia candidata resta disponibile; estrarre rc.2 separatamente e conservare la destinazione con `.porto`.

## 0.1.0-rc.1 — 30 ottobre 2026

Prima candidata Windows 11 x64. Importazione USB WPD in sola lettura, inventario, copia verificata, recupero dopo interruzioni, confronto contenuti con molteplicità, pausa/ripresa e report italiani. Distribuzione portabile con runtime incluso.

Compatibilità hardware iPhone non validata. Intera libreria iCloud, originalità delle risorse WPD, Live Photo senza indizi e varianti modificate non possono essere certificate. Rete, exFAT e Windows ARM non supportati nella candidata.
