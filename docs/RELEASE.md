# Porto 0.1.0-rc.15

Candidata Windows 11 x64, con runtime incluso, per riprovare il collegamento alla libreria Foto dell'iPhone dopo il fallimento `APPLE-TLS-LOCAL-42` della rc.14.

È stato riprodotto un rifiuto del lettore X.509 per certificati con issuer vuoto, un formato usato nell'abbinamento Apple. Il rifiuto avveniva prima del confronto con il certificato autorizzato. La rc.15 aggiunge un lettore circoscritto ai byte esatti dei certificati già abbinati; conserva i certificati originali e i loro vincoli. Non accetta certificati estranei, non cambia le protezioni di Windows e non riduce la versione della libreria TLS.

**La correzione è verificabile nei test sintetici; la riuscita sull'iPhone della cliente resta da confermare.** Il certificato del telefono reale non è stato acquisito o analizzato. Restano TLS 1.2, cifratura AES-GCM, controllo esatto del certificato abbinato e autenticazione reciproca.

Le modalità USB generica e cartella restano incluse. Nessuna conversione dei media, cancellazione delle copie precedenti o modifica ai file delle prove. Nessuna nuova dipendenza, installazione automatica di servizi/driver o spesa.

Per aggiornare:

1. Attendi la fine di un'eventuale importazione oppure mettila in pausa e aspetta che sia ferma; chiudi Porto.
2. Estrai tutto lo ZIP rc.15 in una cartella nuova e apri il nuovo `Porto.exe`.
3. Conserva la vecchia versione e l'intero archivio precedente, inclusi `.porto`, report e copie.

Per la prima prova, con l'iPhone collegato, sbloccato e autorizzato e il servizio Apple disponibile, scegli **iPhone · libreria Foto via USB → Cerca dispositivi**, poi **Un album della libreria Foto → Scegli album**. L'apertura dell'elenco non avvia la copia dei media. Se gli album compaiono, prova **Solo video** su un piccolo album e una nuova cartella del PC. Prima di importare una grande raccolta, controlla l'esito e apri le copie. Se compare un errore, conserva il testo completo e il codice: non modificare le protezioni e non ripetere tentativi identici.

Restano da confermare accesso e schema del catalogo, album, originali locali, trasferimento e ripresa sul telefono reale. Originali solo cloud e raccolte non esposte restano limiti; nessun totale fisso o compatibilità universale garantiti. Eseguibile non firmato commercialmente.

[Verifiche effettive](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.15/docs/TESTS.md) · [Analisi del blocco](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.15/docs/REVIEW-RC15.md) · [Guida iPhone](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.15/docs/PHONES-AND-ALBUMS.md)
