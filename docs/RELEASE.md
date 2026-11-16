# Porto 0.1.0-rc.7

Candidata Windows 11 x64 portabile, con runtime incluso.

Dopo una lettura fallita Porto sostituisce il proprio processo USB e rilegge
l'inventario prima di aprire il video successivo. Il controllo copre flussi
con dimensione errata e hash discordante. Se l'inventario cambia o è
incompleto, il video non ancora aperto resta pendente e serve un nuovo passaggio.
Nessuna associazione per solo nome e nessuna falsa conferma di completamento.

I test includono cinque video difettosi seguiti da tre sani, copiati esattamente
nello stesso passaggio senza duplicati. Nessuna copia manuale richiesta.
Report con versione dell'app e numero dei riavvii USB.

269 verifiche superate: 265 ordinarie e quattro estese, in due esecuzioni.
Build senza avvisi/errori, regressioni sulla rc.6, mutazione e revisione
indipendente. Nessun telefono o video personale usato nei test. Nessuna
conversione, cancellazione sul telefono, installazione di driver o spesa.

**Il recupero dei video reali resta da confermare con una normale importazione.**
Inventari cambiati a ogni connessione, timeout e disconnessioni possono
esaurire i tre passaggi. Restano i limiti rispetto all'intera libreria Foto,
alle risorse ambigue e ai contenuti solo iCloud.

Chiudere Porto, estrarre l'intero ZIP in una cartella nuova e aprire Porto.exe.
Scegliere la stessa destinazione della prova precedente, conservando video,
report, temporanei e .porto. Mantenere la verifica approfondita e avviare.
Durante il recupero può ripetersi l'esame dei metadati. Le copie presenti
vengono rilette e verificate. Non disattivare le protezioni Windows.

[Istruzioni](https://9gabri.github.io/porto-iphone-usb/) · [Test](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.7/docs/TESTS.md) · [Revisione](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.7/docs/REVIEW-RC7.md)

Scaricare Porto-0.1.0-rc.7-win-x64.zip e SHA256SUMS.txt dagli allegati.
