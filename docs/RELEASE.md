# Porto 0.1.0-rc.3

Candidata Windows 11 x64 per copiare in massa i video accessibili dell'iPhone via USB.
Estrarre tutta la cartella e aprire Porto.exe; runtime incluso.

La revisione avversaria ha corretto conteggi storici di video identici, letture
pendenti cancellate da vecchi fingerprint, riuso di ID dopo riconnessione e
blocco dei file sani dopo un errore. Timeout e Pausa coprono anche i comandi.

Aggiunti controlli su hard link, cataloghi/journal estranei, report atomici,
gerarchia dei contenitori e confezionamento in una cartella nuova.
Tutti i 124 test finali sono passati. Le prove comprendono crash reali di processi sintetici, pipe sature, migrazioni,
file oltre 4 GB e clip MOV/MP4/M4V H.264/HEVC generate localmente.

**Nessun trasferimento con iPhone reale è stato validato.** Rimane una candidata,
senza promessa di zero bug o copertura degli originali presenti soltanto su iCloud.
Il telefono è utilizzato in sola lettura. Nessun servizio a pagamento introdotto.

Per aggiornare: chiudere la vecchia app ed estrarre rc.3 separatamente. Conservare
la destinazione con .porto e usare la nuova versione per le importazioni successive.
Vecchi lavori pendenti senza storia sufficiente rimangono esplicitamente incerti.
Usare una cartella locale NTFS dedicata a un solo telefono/libreria.

Eseguibile non firmato; non disattivare antivirus o altre protezioni.

[Istruzioni](https://9gabri.github.io/porto-iphone-usb/) · [Prove e limiti](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.3/docs/TESTS.md) · [Revisione](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.3/docs/REVIEW-RC3.md)

Scaricare Porto-0.1.0-rc.3-win-x64.zip e SHA256SUMS.txt dagli allegati.
Gli archivi Source code sono per sviluppatori.
