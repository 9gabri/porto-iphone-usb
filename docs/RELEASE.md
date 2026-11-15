# Porto 0.1.0-rc.5

Candidata Windows 11 x64 per copiare in massa i video accessibili dell'iPhone via
USB. Estrarre tutta la cartella e aprire Porto.exe; runtime incluso.

Dopo gli errori emersi nel collaudo rc.4, questa versione corregge la lettura
oltre la fine del flusso USB, distingue la dimensione della risorsa aperta da
quella dell'inventario e riconosce gli accessori AAE. Se le dimensioni dichiarate
differiscono, sono obbligatorie due letture complete concordanti: i controlli
su troncamento, SHA-256, copia locale e contenitore rimangono attivi. Corretto
anche il riuso delle copie quando la dimensione inventariata è inesatta.

L'interfaccia distingue risorse USB, immagini, accessori, possibili Live Photo
o modifiche e candidati alla copia. Una barra piena non rimane visibile dopo
un esito incompleto. Errori e ambiguità restano nel report.

Superati **233/233 test** in un'unica esecuzione finale, con build senza avvisi
o errori. Le verifiche comprendono regressioni native COM e processi worker sintetici,
troncamenti, letture discordanti, ripresa, duplicati, cataloghi e percorsi
ostili, 50.000 elementi, 2.000 copie con secondo passaggio e un file oltre
4 GB su NTFS. Risultati effettivi e limiti sono documentati sotto.

**La rc.5 richiede ancora la prova con iPhone reale.** I report precedenti non
dimostrano che tutti gli errori fisici avessero la stessa causa. Nessuna promessa
di zero bug o copertura degli originali soltanto su iCloud. Il telefono viene
aperto in sola lettura; nessun nuovo servizio, driver o costo introdotto.

Per aggiornare: chiudere la vecchia app ed estrarre rc.5 separatamente.
**Per il collaudo scegliere una cartella vuota nuova, per esempio
“Porto - prova rc5”, conservando intatti i file della prova precedente.**
Usare una cartella locale NTFS dedicata a un solo telefono/libreria. I cataloghi
precedenti restano compatibili, ma vecchi obblighi senza prove sufficienti non
vengono cancellati per supposizione.

Eseguibile non firmato; non disattivare antivirus o altre protezioni.

[Istruzioni](https://9gabri.github.io/porto-iphone-usb/) · [Prove e limiti](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.5/docs/TESTS.md) · [Revisione](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.5/docs/REVIEW-RC5.md)

Scaricare Porto-0.1.0-rc.5-win-x64.zip e SHA256SUMS.txt dagli allegati.
Gli archivi Source code sono per sviluppatori.
