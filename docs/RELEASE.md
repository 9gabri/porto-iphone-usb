# Porto 0.1.0-rc.18 — album con associazioni non risolte

Candidata Windows 11 x64 con runtime incluso. Corregge il blocco generale che impediva di aprire qualsiasi album quando una sola associazione del catalogo risultava irrisolta.

La schermata rc.17 conferma che il tentativo ha raggiunto l'interpretazione delle associazioni dopo la lettura e il controllo di integrità del catalogo. Non sappiamo ancora quali associazioni siano irrisolte sul telefono della cliente né perché: non vengono considerate automaticamente eliminate o irrilevanti.

Con la rc.18 gli album personali con associazioni integre possono essere scelti. Gli album coinvolti restano visibili come **NON SELEZIONABILE**, con il conteggio delle associazioni irrisolte. La galleria completa resta bloccata se esiste qualsiasi associazione irrisolta. Porto non allarga la scelta ad altri album e non presenta una selezione incompleta come completa.

**Questa modifica non garantisce che l'album desiderato dalla cliente sia utilizzabile.** Se anche quell'album contiene associazioni irrisolte, il suo trasferimento rimane bloccato. La riuscita con il dispositivo reale resta da confermare.

Per la prossima prova:

1. Chiudi la versione precedente di Porto. Conserva tutti i file delle prove e le versioni precedenti.
2. Estrai tutto lo ZIP in una cartella nuova, apri `Porto.exe` e verifica **0.1.0-rc.18** nel titolo.
3. Con iPhone collegato, sbloccato e già autorizzato, scegli **iPhone · libreria Foto via USB → Cerca dispositivi** e seleziona il telefono.
4. Imposta **Solo video → Un album della libreria Foto → Scegli album**. Attendi la lettura; questa fase non copia i video.
5. Cerca nell'elenco l'album desiderato. Se è indicato **NON SELEZIONABILE**, conserva una foto della riga e dell'avviso completo: non avviare una copia parziale come soluzione.
6. Se l'album è selezionabile, prova prima un piccolo album compatibile, scegli una nuova cartella PC di prova e avvia l'importazione. Verifica esito, conteggi e apertura dei video prima di passare alla selezione grande. Per riutilizzare copie precedenti conserva il relativo archivio completo, compresi `.porto` e report.

Non sono richiesti nuovi componenti, spese o riduzioni delle protezioni di Windows. Non vengono salvati su disco cataloghi personali del telefono. I contenuti del telefono e i file delle prove precedenti restano da conservare; questa correzione non elimina file. Originali soltanto nel cloud, raccolte non supportate, schemi iOS e compatibilità reale restano soggetti ai limiti documentati. Candidata non firmata commercialmente e non certificata.

[Verifiche effettive](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.18/docs/TESTS.md) · [Revisione e limiti](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.18/docs/REVIEW-RC18.md) · [Guida iPhone](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.18/docs/PHONES-AND-ALBUMS.md)
