# Revisione avversaria rc.3

Mandato in tasks/ADVERSARIAL-READINESS.md. Tre revisori con contesto nuovo hanno
esaminato contabilità, USB e storage. Ulteriori riesami hanno cercato regressioni
nelle correzioni. Nessuna chiamata a servizi/modelli esterni, accesso hardware,
file personali o modifica a protezioni. Le decisioni sono state verificate sul
codice e mediante fixture; il parere di un revisore non è una certificazione.

## Riscontri e disposizione

| Riscontro | Risoluzione e prova |
|---|---|
| Una copia assolveva due duplicati dopo alias e pause | Snapshot/conteggi persistenti, prove per inversioni, catene e ricomparsa contemporanea |
| Vecchio fingerprint cancellava un elemento appena osservato ma non letto | Stato unread distinto; regressione con alias successivo e contenuto diverso ancora non letto |
| Seconda lettura USB discordante poteva essere riconciliata in seguito | Conflitto persistente prima dell'errore; prova anche dopo alias e inventario vuoto |
| Gruppo locale verificato una volta poteva diventare obsoleto | Rilettura del file specifico al momento del riuso; mutazione fra due elementi |
| Riparazione riuscita lasciava errore Damaged permanente | Collegamento Replaced e conservazione del vecchio payload; passaggio successivo pulito |
| Riconnessione riusava ID oggetto della sessione morta | Nuovo inventario obbligatorio; child sintetico termina realmente durante la lettura |
| Il primo errore per file fermava i successivi nella correzione USB | Errore remoto valido separato da morte/protocollo; tre file rotti seguiti da due sani tramite worker reale sintetico |
| Invio al worker poteva ignorare timeout/Pausa | Scrittura asincrona e cleanup limitato; pipe non letta con comando lungo |
| Conteggio zero generava una raffica di progressi | Throttle uniforme, provato con 10.000 callback |
| Hard link permettevano scritture su file estranei | Link count prima di append/SQLite; file sentinella esterno invariato |
| DB/journal estranei venivano adottati | Compatibilità schema/header/versione, colonne nascoste, journal troncati/non attribuibili e footer esterni rifiutati; migrazione e recupero nativi provati |
| Report finali troncati dopo crash | Temporaneo + flush + pubblicazione senza overwrite; arresto reale durante scrittura |
| Profondità di un hdlr scambiata per gerarchia video | Percorso moov/trak/mdia esplicito; strutture malformate e clip codificate valide |
| Staging riutilizzato poteva includere residui privati | Directory nuova per ogni pacchetto e controllo del contenuto dello ZIP |

Tre regressioni sono state riprodotte sulla rc.2 esportata in una cartella di
build separata: falsa completezza dopo inversione degli ID, errore Damaged
permanente e gerarchia media non valida. Una mutazione isolata del confronto
del numero di copie fa fallire la regressione pertinente. Nessuna mutazione
è stata applicata alla sorgente distribuita.

## Confini mantenuti

- Nessuna scrittura alla sorgente; nessun upload, acquisto, certificato o driver.
- Prove con dati sintetici, incluse clip H.264/HEVC generate e decodificate localmente;
  nessuna validazione su iPhone reale o trasferimento fisico da 250 GB.
- Integrità dei byte ricevuti distinta da originalità Foto e decodifica di ogni video.
- Una destinazione per telefono/libreria. Non mescolare cataloghi o journal e non
  alterarli manualmente: lo schema è controllato, non autenticato crittograficamente.
- Il processo locale ostile concorrente e la reale perdita di alimentazione
  non sono simulati. Journal non riconoscibili vengono conservati e rifiutati.
- Gli elementi non riletti, i conflitti di contenuto e i debiti legacy senza storia
  sufficiente rimangono esplicitamente pendenti/incerti: nessun successo forzato.
- Dopo tre passaggi, gli impedimenti permanenti restano nel report. Non si può
  promettere completamento automatico con cavo scollegato, dati assenti o disco pieno.

I risultati eseguiti sono in TESTS.md e test-results.txt; la verifica del download
pubblico viene registrata separatamente in RELEASE-VERIFICATION.md.
