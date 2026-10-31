# Novità

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
