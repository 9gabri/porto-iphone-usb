# Porto 0.1.0-rc.14

Candidata Windows 11 x64 per una nuova prova del collegamento iPhone, con runtime incluso. La rc.13 ha trovato un iPhone reale ma ha fallito l'autenticazione prima di leggere il catalogo Foto. Questa release corregge incompatibilità TLS riprodotte e rende distinguibili gli errori; **non è ancora dimostrato che risolva il blocco osservato su quel telefono**.

Modifiche:
- La negoziazione accetta firme RSA/SHA-384, RSA/SHA-512 e RSA-PSS con SHA-256/384/512, oltre a RSA/SHA-256. Prima poteva rifiutare una combinazione forte supportata dall'interlocutore.
- Gli errori distinguono certificato non corrispondente, autenticazione client mancante, tipo/firma non supportati, estensione TLS richiesta assente e alert locali o ricevuti. I codici non contengono nomi, seriali, certificati o chiavi.
- Restano TLS 1.2, ECDHE_RSA/AES-GCM, controllo esatto del certificato abbinato e autenticazione reciproca. Nessun fallback a TLS obsoleto, CBC, firme handshake SHA-1 o collegamenti senza le protezioni richieste. Nessuna modifica alle protezioni di Windows.

Le correzioni precedenti e le modalità USB generica/cartella restano incluse. Nessuna modifica al formato dei video, nessuna cancellazione o sovrascrittura delle copie precedenti. Nessuna installazione di servizi/driver da parte di Porto e nessun nuovo costo.

Per aggiornare, attendi che un'eventuale importazione sia terminata oppure sia stata messa in pausa e risulti ferma; chiudi Porto. Estrai tutto lo ZIP in una cartella nuova e apri il nuovo Porto.exe. Conserva la vecchia versione e l'intero archivio con .porto, report e copie.

Per provare il collegamento: con l'iPhone collegato, sbloccato e già autorizzato negli strumenti Apple, scegli **iPhone · libreria Foto via USB → Cerca dispositivi**, poi **Un album della libreria Foto → Scegli album**. Questa verifica non avvia la copia dei media. Se gli album compaiono, prova Solo video su un piccolo album personale e una nuova destinazione. Se fallisce, conserva il messaggio completo, incluso il codice APPLE-TLS: non servono tentativi identici o modifiche alle protezioni del PC.

Il servizio Apple deve essere disponibile sul computer. Anche dopo l'autenticazione restano da verificare accesso e schema del catalogo, album, originali locali, copie e ripresa. Originali solo cloud, raccolte intelligenti/condivise e formati non supportati restano limiti. Nessun totale fisso, compatibilità universale o riuscita della prova reale garantiti. Eseguibile non firmato commercialmente.

[Verifiche effettive](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.14/docs/TESTS.md) · [Analisi del blocco](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.14/docs/REVIEW-RC14.md) · [Guida iPhone](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.14/docs/PHONES-AND-ALBUMS.md)
