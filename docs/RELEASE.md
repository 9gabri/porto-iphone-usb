# Porto 0.1.0-rc.12

Corregge il rifiuto di foto JPEG che contengono dati aggiuntivi dopo la fine dell'immagine principale. In rc.11 un controllo troppo restrittivo poteva lasciare queste foto come ambigue nella cartella .porto, anche dopo il trasferimento e la verifica SHA-256.

La correzione è nel lettore comune: vale per USB Android/iPhone, catalogo iPhone e cartella locale, senza riconoscimenti per marca o modello. Porto controlla la struttura JPEG principale e conserva l'intero file senza conversioni. I dati aggiuntivi non sono interpretati né dichiarati strutturalmente verificati; il loro numero di byte è indicato nel report anche quando la copia viene riutilizzata. Non vengono estratti video da foto animate o certificate funzioni HDR/Motion.

Per completare una prova precedente: chiudi Porto, estrai il nuovo ZIP in una nuova cartella, seleziona lo stesso telefono/album, Foto e video e lo stesso Archivio PC. Premi Avvia importazione. Le copie integre vengono confrontate e riutilizzate; i temporanei prima rifiutati vengono riesaminati e, se accettati e corrispondenti alla sorgente, pubblicati come nuove copie. I vecchi .part e i file precedenti restano conservati.

Verifiche: 457/457 test ordinari, quattro prove del confezionamento, revisione indipendente e controllo in sola lettura dei 29 JPEG interessati. Recupero e riutilizzo senza duplicati verificati su un archivio sintetico di 77 elementi.

Runtime incluso, nessun driver o servizio da installare tramite Porto, nessun costo. Eseguibile non firmato. Non è compatibilità universale né certificazione di assenza di bug; altri formati o problemi rimangono visibili. Restano i limiti della rc.11 per album, cloud e collegamento iPhone da collaudare.

[Test e limiti](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.12/docs/TESTS.md) · [Revisione](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.12/docs/REVIEW-RC12.md)
