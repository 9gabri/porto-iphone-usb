# Porto 0.1.0-rc.13

Candidata Windows 11 x64 per il collaudo iPhone, con runtime incluso. Non richiede un Mac né un'esportazione preliminare, ma richiede collegamento Apple e abbinamento già disponibili sul PC. Compatibilità con l'iPhone reale ancora da confermare.

Correzioni verificate:
- Un elemento scomparso tra tentativi automatici non permette più di dichiarare riuscita una selezione ridotta; copie e lavoro precedente restano conservati.
- Le risposte AFC malformate invalidano il collegamento, invece di lasciarlo apparentemente utilizzabile.
- Un journal SQLite PERSIST già concluso non blocca automaticamente la lettura; journal attivi, incerti o mutevoli restano rifiutati.
- Stati di visibilità non riconosciuti nel catalogo rimangono conteggiati e ambigui, senza aprire gli originali o attribuirli automaticamente a iCloud.

La correzione JPEG della rc.12 resta inclusa. Nessuna modifica al formato dei video, nessuna cancellazione o sovrascrittura delle copie precedenti. TLS invariato; nessun driver/servizio installato o protezione ridotta.

Per aggiornare: chiudi Porto, estrai tutto lo ZIP in una cartella nuova e apri il nuovo Porto.exe. Per il primo collaudo scegli **iPhone · libreria Foto via USB**, Cerca dispositivi e poi **Un album della libreria Foto → Scegli album**: la ricerca del telefono, da sola, non prova l'accesso al catalogo. Prova **Solo video** su un piccolo album personale esistente e una destinazione di prova. Dopo il controllo passa alla selezione desiderata e all'archivio precedente, conservando .porto e report.

Restano limiti: schema iOS privato, originali solo cloud, album intelligenti/condivisi e formati non supportati. Variazioni di metadati o disponibilità durante i tentativi possono richiedere un nuovo avvio esplicito. Nessun numero fisso di video o compatibilità universale garantiti. Eseguibile non firmato commercialmente; non disattivare le protezioni Windows.

[Verifiche effettive](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.13/docs/TESTS.md) · [Revisione indipendente](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.13/docs/REVIEW-RC13.md) · [Guida alla prima prova](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.13/docs/PHONES-AND-ALBUMS.md#prima-prova-iphone-rc13)