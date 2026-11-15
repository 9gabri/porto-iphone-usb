# Porto 0.1.0-rc.6

Candidata Windows 11 x64, portabile con runtime incluso, per una nuova prova di
recupero dopo gli errori USB della rc.5.

- Annullamento WPD delle operazioni di lettura incomplete prima del rilascio e
  dell'apertura successiva. Se la cancellazione fallisce serve una nuova sessione.
- Un errore nella chiusura della lettura precedente non viene attribuito al video
  successivo ancora da aprire. I file realmente falliti vengono rimandati dopo i sani.
- Un vecchio stream non può leggere i byte di una risorsa aperta successivamente.
- Contatori più chiari sull'ultimo passaggio e motivazioni distinte per le
  ambiguità da foto omonima o nome IMG_E. Zero pendenti non significa zero errori.

Dimensioni, hash, verifica locale e seconda lettura richiesta restano controllati.
Nessuna conversione, cancellazione sul telefono, driver o servizio aggiunto.
Superati 253/253 test in una suite finale unica, build senza avvisi/errori.
Regressioni sintetiche, revisione indipendente, baseline negativa e mutazione
isolata documentate nei collegamenti sotto; nessun video personale incluso.

**Non è dimostrato che tutti gli errori reali abbiano la stessa causa o siano
risolti. La differenza rispetto ai video autonomi in Foto resta da riconciliare.**
Se la cancellazione del driver si blocca, il timeout termina il worker; entro
tre tentativi possono restare elementi non raggiunti, sempre dichiarati incompleti.
Le risorse ambigue e gli originali solo iCloud non vengono dichiarati recuperati.

Per aggiornare: chiudere la vecchia app, estrarre tutta la rc.6 separatamente,
aprire Porto.exe. **Per recuperare la prova rc.5 scegliere la stessa destinazione
con tutti i file e .porto conservati**, mantenere la verifica approfondita e
avviare. Le copie precedenti vengono rilette e riutilizzate se integre: non è
una selezione basata solo sui nomi. Temporanei divergenti restano conservati.

Eseguibile non firmato. Non disattivare le protezioni Windows. Necessaria una
prova con lo specifico iPhone, iOS, cavo e driver; nessuna promessa di zero bug.

[Istruzioni](https://9gabri.github.io/porto-iphone-usb/) · [Test](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.6/docs/TESTS.md) · [Revisione](https://github.com/9gabri/porto-iphone-usb/blob/v0.1.0-rc.6/docs/REVIEW-RC6.md)

Scaricare Porto-0.1.0-rc.6-win-x64.zip e SHA256SUMS.txt dagli allegati.
