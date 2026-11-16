# Porto 0.1.0-rc.10

Candidata Windows 11 x64 portabile, runtime incluso. Aggiunge **Solo foto / Solo video / Foto e video**, ricerca dei dispositivi portatili WPD anche non Apple e scelta di una cartella fisica USB con le sottocartelle.

La copia massiva resta disponibile: scegli tutto il dispositivo accessibile e avvia una volta. Gli album virtuali di Foto/Google Foto non sono necessariamente esposti via USB: per quelli usa **Cartella / album esportato** dopo l’esportazione sul PC. Non è promessa compatibilità con ogni telefono.

Le copie precedenti restano al loro posto, senza cancellazioni o sovrascritture. Confronto completo e SHA-256 per riutilizzare copie identiche; file distinti selezionati con gli stessi byte mantengono copie distinte. Foto nuove in Foto Porto, video in Video Porto, indizi Apple separati nelle Risorse video aggiuntive.

Chiudi la vecchia app, estrai tutto lo ZIP separatamente e apri Porto.exe. Scegli fonte, contenuti, ambito e archivio PC. Per riutilizzare le vecchie copie scegli lo stesso archivio, conservando .porto e report. Poi Avvia importazione e consulta il risultato.

Verifiche finali documentate in TESTS.md: suite sintetica completa, build senza avvisi/errori, sette stati UI e quattro prove di confezionamento. Revisione indipendente di filtri, selezione USB e parser immagini con controesempi riprodotti. Nessun file personale o telefono letto durante lo sviluppo.

**Resta da confermare il trasferimento fisico su iPhone/Android e l’esportazione degli album reali.** Formati controllati: MOV/MP4/M4V, JPEG, PNG statico e HEIF/HEIC HEVC riconosciuti. Controlli strutturali, nessuna certificazione di decodifica completa. Formati non supportati e incertezze restano nel report. Contenuti solo cloud possono mancare. Non disattivare le protezioni Windows.

[Guida](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.10/docs/PHONES-AND-ALBUMS.md) · [Test](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.10/docs/TESTS.md) · [Revisione](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.10/docs/REVIEW-RC10.md)

Allegati: Porto-0.1.0-rc.10-win-x64.zip e SHA256SUMS.txt.
