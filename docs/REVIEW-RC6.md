# rc.6: letture interrotte e recupero

Diagnosi dai report autorizzati del collaudo rc.5; nessuna lettura dei video,
scrittura nella destinazione reale o enumerazione del telefono. Le regressioni
usano soltanto risorse, processi e destinazioni sintetiche.

## Riscontri e correzioni

- `WpdSource`: Release dello stream non annullava esplicitamente un'operazione
  WPD incompleta. Un driver sintetico conserva la transazione interrotta e
  restituisce dati della precedente risorsa finché non riceve Cancel. La
  regressione fallisce sulla baseline. Ora viene chiamato
  IPortableDeviceResources.Cancel (slot 7) prima del rilascio solo se non è
  stato raggiunto EOF, e anche quando GetStream fallisce. Nessuna chiamata a
  Delete, Revert o metodi di scrittura. Un trasferimento completato non viene
  annullato. Lo stream attivo viene rilasciato prima del suo proprietario COM.
- Cleanup fallito: l'errore primario non viene mascherato; la prossima
  operazione rifiuta la sessione e richiede un nuovo inventario. L'eccezione
  PreviousReadCleanupException attraversa il worker con un indicatore
  strutturato: la risorsa successiva non è ancora stata aperta, resta Pending
  e non viene penalizzata nell'ordine dei tentativi.
- `ImportEngine`: vengono rimandati i file realmente falliti, anche quando
  l'errore del singolo file precede la perdita della sessione. In caso contrario
  un file A difettoso poteva bloccare di nuovo i sani B/C/D in ogni passaggio.
  Due regressioni, con ID stabili e rimappati, falliscono sulla baseline e
  richiedono tre copie sane esatte entro i tre tentativi; A resta irrisolto.
- `WorkerClient`: dopo un'altra apertura, inventario o connessione, un vecchio
  wrapper poteva leggere lo stream corrente. Una generazione locale ora rifiuta
  la lettura obsoleta prima dell'RPC. Non è stato trovato questo uso nel percorso
  corrente dell'engine: è un difetto API riprodotto, non una causa dimostrata
  degli errori della prova fisica.
- UI e report: espliciti copie verificate, nuove copie e riuso nell'ultimo
  passaggio. Zero pendenti aggiuntivi non implica assenza di errori o ambiguità.
  Le ragioni delle risorse ambigue distinguono foto omonima nella stessa cartella
  da nome IMG_E, includendo entrambi gli indizi quando presenti. Le decisioni di
  classificazione restano prudenti: nessun elemento viene nascosto per fare
  coincidere i conteggi con Foto.

## Verifica indipendente e limiti

Revisione indipendente su ABI, lifetime, integrità e recupero. La revisione ha
individuato l'attribuzione errata del cleanup alla risorsa successiva; il
controesempio è stato riprodotto e corretto con il segnale strutturato e la
priorità dei file realmente falliti. Nessun costo o servizio aggiuntivo.

Baseline: quattro regressioni sui wrapper obsoleti 0/4; sei prove native 1/6
(il controllo EOF già passava); tre motivazioni di classificazione 0/3;
protocollo della causa precedente 0/1; recupero con ID stabili/rimappati 0/2.
Le nuove prove comprendono anche GetStream senza puntatore, doppia apertura,
timeout e cancellazione mentre il worker non risponde all'apertura successiva.
Una mutazione isolata inverte la condizione di Cancel per verificare che il
test rilevi la regressione. Esiti finali in TESTS.md.

Le coincidenze delle lunghezze nei report reali sono compatibili con stato
residuo del driver, ma non lo dimostrano. Questa candidata non è ancora stata
validata sull'iPhone; non è garantito che ogni errore USB si risolva.
La differenza rispetto al numero di video autonomi mostrati da Foto non è
riconciliata: servono altri riscontri sull'inventario e sulla classificazione.
Non dichiarare recuperati gli elementi ambigui o gli originali solo iCloud.

Se Cancel si blocca anziché restituire un errore, il client osserva un timeout
durante il comando successivo e termina il worker. La fase di cleanup non è
distinguibile con certezza da un blocco della nuova apertura; entro i tre
tentativi possono restare video sani non raggiunti. Il report resta incompleto.

Per il recupero rc.5 → rc.6 usare la stessa destinazione conservando cataloghi,
report, video e temporanei. Le copie già presenti vengono rilette e confrontate;
non si tratta di saltare i file sulla sola base del nome. Un vecchio temporaneo
divergente rimane conservato. Schema e criteri di integrità non sono allentati.
