# Revisione rc.11 — gallerie, album e navigazione USB

## Ambito

Due agenti distinti hanno implementato e revisionato i confini USB/WPD e catalogo Foto Apple; il coordinatore ha integrato interfaccia, confronto delle copie e report. Revisione reciproca finale del collegamento TLS e dei criteri di completamento. Nessun telefono, archivio personale o credenziale reale è stato letto dai test. Nessun servizio/driver installato, acquisto o protezione Windows modificata.

## Difetti confermati e correzioni

1. **Scansione completa prima della scelta cartella (alto impatto sul tempo):** MainForm.PickUsbFolder chiamava Inventory() dell'intero dispositivo. Ora ReadLevel legge soltanto i figli immediati e recupera insieme cartelle e raccolte. Fixture: 60.000 discendenti estranei mai visitati.
2. **Selezione limitata applicata dopo l'inventario globale:** UsbImportSource filtrava l'elenco già completo. Ora usa un inventario nativo del solo ramo scelto; la riconnessione risolve nuovamente lo stesso percorso e non ripiega sul telefono intero.
3. **Proprietà USB inutili:** GetValues con pKeys nullo chiedeva tutte le proprietà. Ora richiede le sei utilizzate; WPD_OBJECT_REFERENCES è letto soltanto per una raccolta esplicitamente scelta.
4. **Album confusi con cartelle:** i tre tipi WPD di album hanno una selezione distinta e copiano soltanto i riferimenti dichiarati. Le raccolte non esposte dal driver restano non supportate, senza inventare appartenenze.
5. **Tipo Foto sconosciuto interpretato tramite estensione MOV:** un ruolo ignoto del catalogo poteva ricadere nel riconoscimento generico per estensione. Ora resta ambiguo e non viene aperto. Un asset Foto di tipo immagine, comprese Live Photo, non entra in Solo video.
6. **Errori su contenuti esclusi dal filtro:** il primo adapter controllava anche gli originali di tutte le foto quando erano richiesti solo video. Ora non effettua stat dei tipi esclusi. Fixture con 10.000 foto escluse: una sola richiesta al file video.
7. **Risultato falso “incompleto” per zero elementi del tipo scelto:** l'esistenza di righe escluse impediva il messaggio Nessun elemento. Ora un inventario stabile senza incertezze produce un esito vuoto esplicito.
8. **Descrizione dell'album assente dal report:** il nome era acquisito dopo la creazione del report. Viene aggiornato dopo l'inventario iniziale. I metadati dei tipi esclusi vengono conteggiati in aggregato, senza salvare i loro nomi/UUID nell'inventario della modalità galleria.
9. **Parser di protocollo:** UTF-8 malformato poteva uscire dal confine IOException; riferimenti ripetuti nel plist binario potevano amplificare eccessivamente la memoria. Aggiunti errori normalizzati e budget di materializzazione.
10. **TLS Schannel e chiavi effimere Windows:** il test interamente in memoria ha riprodotto il rifiuto delle chiavi effimere. Nessun workaround con chiavi salvate su disco. TLS gestito con BouncyCastle gratuito, versione fissata, protocollo e cifrari limitati, certificato del dispositivo associato confrontato esattamente e cancellazione su handshake/letture/scritture.

## Garanzie deliberate e limiti residui

- AFC ammette soltanto stat, apertura in sola lettura, lettura e chiusura; percorsi limitati al catalogo Foto e DCIM. Nessuna creazione di abbinamenti, scrittura di file o comando di cancellazione sul dispositivo.
- Catalogo e WAL in memoria, dimensione cumulativa massima 512 MiB, lettura ripetuta e controllo delle variazioni, checksum WAL, ultimo commit valido e integrity_check SQLite. Questo NON dimostra uno snapshot atomico della libreria; il report non certifica l'intera galleria.
- La modalità Foto Apple usa uno schema privato: supporto ai soli album personali riconosciuti, esclusione esplicita di Nascosti/Eliminati. Raccolte intelligenti/condivise non supportate. La disponibilità reale su iOS resta da collaudare.
- Android: i protocolli USB non espongono necessariamente gli album virtuali di OPPO/Google Foto/altre app. Nessuna compatibilità universale promessa. Un livello con moltissimi figli diretti deve comunque essere enumerato se aperto.
- Copie già presenti riutilizzate solo dopo lettura completa e verifica; nessuna cancellazione o sovrascrittura. Le risorse Live precedenti restano conservate.
- Formati limitati a quelli strutturalmente supportati; contenuti cloud, formati sconosciuti e variazioni rimangono visibili, senza dichiarazioni di completamento assoluto.

Risultati numerici finali e limiti delle prove in TESTS.md; verifica del download pubblico in RELEASE-VERIFICATION.md. I riscontri sintetici non sostituiscono il collaudo del dispositivo reale.

## Ricontrolli finali e fixture

- La prima suite estesa ha rilevato una regressione introdotta nel messaggio di selezione vuota: le copie storiche mancanti non devono essere mascherate da Nessun elemento. Il controllo considera di nuovo gli obblighi storici nelle modalità USB generiche; il caso di regressione e poi tutti i 445 test ordinari sono passati.
- Sei test del recupero rc.7 fallivano perché il testo atteso della versione era fissato a rc.10. La fixture ora deriva la versione effettiva dall'assembly dei test. Il comportamento di recupero era già passato; le sei asserzioni complete sono state rieseguite nella suite verde.
- Due test TLS negativi usavano inizialmente un pin diverso dal certificato sintetico del server: potevano fallire per il motivo sbagliato. Corretti i pin, sono stati ripetuti tutti e cinque i casi TLS: server corretto accettato, server diverso rifiutato, assenza di richiesta del certificato client rifiutata, firma handshake SHA-1 rifiutata, cancellazione rispettata. Nessuna attestazione basata sulle fixture confondenti.

## Due correzioni finali del coordinatore, riprodotte dal revisore

1. **Recupero Apple fallito prima di aprire il prossimo file.** Una riconnessione o un catalogo cambiato potevano produrre SourceUnavailableException; il motore continuava attribuendo errori anche ai file mai aperti e riutilizzando un inventario ormai svuotato. Ora PreviousReadCleanupException interrompe quel passaggio, lascia i successivi pendenti e impedisce nuovi tentativi nello stesso adapter finché non viene acquisito un nuovo inventario. Fixture: un solo errore di lettura, due pendenti, ripresa automatica al secondo tentativo con tre copie verificate. Cancellazione propagata senza errore imputato al file successivo.
2. **Errore isolato dei metadati di un originale.** Un errore Stat, anche con connessione ancora utilizzabile, interrompeva l'intero inventario. Ora quell'asset resta presente, non risolto, con errore aggregato sui metadati; gli originali sani proseguono. La connessione compromessa continua invece a interrompere l'inventario. Fixture: tre video, due importati e uno mancante/non risolto; nessuna dichiarazione di selezione verificata e nessuna attribuzione automatica a iCloud.

Quattro nuovi casi e rafforzamento del test di inventario cambiato: due fallimenti riprodotti prima delle correzioni (2/4); build finale senza avvisi/errori, 37/37 verifiche Apple e 6/6 verifiche del motore galleria superate. Queste modifiche riguardano esclusivamente AppleGallerySource e i suoi test; la precedente regressione ordinaria 445/445 e le quattro prove estese restano documentate separatamente.
