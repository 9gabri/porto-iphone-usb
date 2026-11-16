# Porto 0.1.0-rc.16 — candidata diagnostica

Versione Windows 11 x64, con runtime incluso, per identificare il nuovo blocco della libreria Foto dell'iPhone: **«Catalogo Foto oltre il limite di 512 MiB in memoria o non leggibile come file regolare»**.

Nella prova rc.15 il collegamento ha raggiunto il controllo dei metadati del catalogo, successivo all'autenticazione TLS e all'accesso AFC. Il messaggio precedente univa cause diverse e non permette di sapere quale si sia verificata. La rc.16 le distingue con un codice `APPLE-CATALOG-…`, il tipo quando non regolare e le dimensioni in byte di database e WAL.

**Questa versione serve alla diagnosi: il blocco del catalogo non è ancora risolto e la modalità album iPhone non è pronta per una copia completa.** Il limite resta 512 MiB. Non vengono seguiti automaticamente collegamenti, salvati database del telefono su disco o ridotte le protezioni. La modifica non richiede installazioni, servizi aggiuntivi o spese.

Per ottenere il messaggio utile:

1. Chiudi la vecchia versione di Porto, dopo aver fermato un'eventuale importazione.
2. Estrai tutto lo ZIP rc.16 in una cartella nuova. Conserva le versioni precedenti e tutti i file delle prove.
3. Apri il nuovo `Porto.exe` e controlla che il titolo riporti **0.1.0-rc.16**.
4. Lascia l'iPhone collegato, sbloccato e già autorizzato. In Porto scegli **iPhone · libreria Foto via USB**, premi **Cerca dispositivi** e seleziona l'iPhone.
5. Scegli **Un album della libreria Foto**, poi premi **Scegli album**.
6. Fotografa il nuovo messaggio completo, compresi codice e numeri. Se compare invece l'elenco degli album, segnala questo esito.

Questa prova non avvia l'importazione dei media: non premere **Avvia importazione** e non ripetere una grande importazione per diagnosticare il problema. Non occorre modificare impostazioni di Windows o installare altro. Non inviare database, certificati, foto o video personali.

Le copie già presenti restano da conservare. Restano da confermare accesso e schema del catalogo, album, trasferimento e ripresa sull'iPhone reale. Gli originali disponibili soltanto nel cloud non sono garantiti via USB. Eseguibile non firmato commercialmente.

[Verifiche effettive](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.16/docs/TESTS.md) · [Analisi e limiti della candidata diagnostica](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.16/docs/REVIEW-RC16.md) · [Guida iPhone](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.16/docs/PHONES-AND-ALBUMS.md)
