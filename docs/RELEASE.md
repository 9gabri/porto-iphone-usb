# Porto 0.1.0-rc.17 — cataloghi iPhone oltre 512 MiB

Candidata Windows 11 x64 con runtime incluso. Affronta il blocco `APPLE-CATALOG-DB-LIMIT` mostrato dalla rc.16: il catalogo osservato misura 563425280 byte, con WAL di 4227152 byte, e superava il precedente limite di 512 MiB.

La rc.17 accetta database e WAL entro **1 GiB complessivo**, previa verifica della memoria disponibile. La seconda lettura verifica tutti i byte con un buffer di 1 MiB, senza conservare un'altra copia completa del catalogo. Restano i controlli sui dati, la copia SQLite di sola lettura con margine aggiuntivo, la gestione degli errori e la pulizia della memoria. Il limite complessivo della fase di acquisizione passa a dieci minuti.

**La correzione riguarda il limite dimensionale identificato. Catalogo, album e copie sull'iPhone reale restano da collaudare: non è una garanzia che l'intero trasferimento riuscirà.** Un computer con memoria insufficiente riceverà un messaggio esplicito; non viene forzata l'allocazione.

Per la prossima prova:

1. Chiudi la versione precedente di Porto, dopo aver fermato un'eventuale importazione.
2. Estrai tutto lo ZIP in una cartella nuova e apri `Porto.exe`. Controlla che il titolo riporti **0.1.0-rc.17**. Conserva le copie, i report e le versioni precedenti.
3. Tieni l'iPhone collegato, sbloccato e già autorizzato. In Porto scegli **iPhone · libreria Foto via USB**, premi **Cerca dispositivi** e seleziona il telefono.
4. Imposta **Solo video**, poi **Un album della libreria Foto → Scegli album**. La lettura del catalogo può richiedere alcuni minuti. Questo comando non avvia la copia dei video.
5. Se compaiono gli album, scegli prima un album piccolo di prova. Se compare un errore, conserva una foto del messaggio completo con codice e numeri.
6. Per la prova di copia scegli una cartella PC nuova e premi **Avvia importazione**. Mantieni il telefono collegato e sbloccato. Controlla l'esito, il report e l'apertura dei video copiati prima di passare a un album grande.

La versione non richiede nuove installazioni o spese. Non salva il catalogo Foto personale su disco, non modifica i contenuti del telefono e non richiede di ridurre le protezioni Windows. Gli originali presenti soltanto nel cloud non sono garantiti via USB. Candidata non firmata commercialmente e non certificata.

[Verifiche effettive](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.17/docs/TESTS.md) · [Revisione e limiti](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.17/docs/REVIEW-RC17.md) · [Guida iPhone](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.17/docs/PHONES-AND-ALBUMS.md)
