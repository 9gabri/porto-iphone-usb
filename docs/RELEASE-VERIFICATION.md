# Verifica della pubblicazione — 30 ottobre 2026

- Repository pubblico: https://github.com/9gabri/porto-iphone-usb
- Release candidata: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.1
- Pagina: https://9gabri.github.io/porto-iphone-usb/
- Sorgente dell'app distribuita: commit `f241f1df3c3deebf15df71bb8c2d7e7d6558f116`, tag `v0.1.0-rc.1`.
- ZIP: `Porto-0.1.0-rc.1-win-x64.zip`, 51.565.196 byte, 283 voci, runtime incluso.
- SHA-256: `41c1e6c8dca774f9d9d545a877b287a34c391b8cfdba4654e95e6a6b3bfc6341`.

Il download pubblico senza autenticazione è riuscito e il suo SHA-256 coincide con lo ZIP locale compilato. Anche SHA256SUMS.txt coincide. L'archivio locale supera il controllo CRC di tutti i membri; nessun SDK, output di test, credenziale o configurazione privata incluso. Controllati gli eseguibili/librerie Porto per marcatori di percorsi dell'ambiente privato.

GitHub Pages segnala build completata senza errori. HTTP 200 sulla pagina, HTML identico al sorgente. Verifica in browser reale: titolo corretto, collegamenti a ZIP/checksum/release presenti, immagine caricata, nessun errore o warning console. Ispezionate vista desktop e viewport mobile 390 px (senza overflow orizzontale); pannello compatibilità apribile. Il viewport è stato poi ripristinato.

L'eseguibile è non firmato. Il runtime è incluso, ma le prove non comprendono alcun iPhone fisico, versione iOS, inventario Foto reale o trasferimento fisico da 250 GB. I 44 test sono sintetici; gli smoke UI/COM/worker del pacchetto non enumerano dispositivi.

## rc.2 — 31 ottobre 2026

- Release candidata: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.2
- Sorgente distribuita: commit `a15c359de59fc272810ffc4bcb5bce048562ab07`, tag `v0.1.0-rc.2`.
- ZIP: `Porto-0.1.0-rc.2-win-x64.zip`, 51.578.202 byte, 283 voci; runtime 10.0.14 incluso.
- SHA-256: `1924a8c0d79033658294ea01aa4296c36435ed1df52425e880cac70b29d60bc3`.

Il pacchetto e SHA256SUMS.txt sono stati scaricati dai link pubblici senza
autenticazione, entrambi HTTP 200. I file scaricati coincidono con gli artefatti
locali. Il controllo CRC di tutte le voci e il manifesto del commit sono corretti;
controllati i percorsi prima dell'estrazione in una cartella dedicata. Nessun SDK,
output di test privato, credenziale, screenshot della cliente o configurazione
personale incluso. Nessun marcatore del percorso privato nelle quattro binarie Porto.

Sul pacchetto riscaricato sono passati gli smoke COM, worker e UI (normale e
ridotta con recupero in corso), senza enumerare dispositivi. Le immagini locali
del pacchetto sono state ispezionate. I 74 casi verificati comprendono 70 test
ordinari e quattro prove estese; dettagli e limiti in TESTS.md e REVIEW-RC2.md.

GitHub Pages ha completato la build del commit della release senza errori.
Pagina e anteprima rispondono HTTP 200 e coincidono byte per byte con il sorgente.
I collegamenti pubblicati puntano allo ZIP, checksum e release rc.2. Per rc.2 non
è stato ripetuto il controllo interattivo del browser eseguito per rc.1.

La release e lo ZIP rc.1 sono conservati: l'hash dell'archivio precedente è
invariato. Nessun costo, nuovo servizio o riduzione delle protezioni. Eseguibile
ancora non firmato; nessuna prova con iPhone fisico. Non è una certificazione di
assenza di bug o della completezza della libreria Foto/iCloud. Questo resoconto è
un aggiornamento di documentazione successivo al tag; il pacchetto rimane immutato.

## rc.3 — 31 ottobre 2026

- Release candidata: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.3
- Sorgente distribuita: commit `ec906caadb299af14aa074fd14a0b1e316e97f6c`, tag `v0.1.0-rc.3`.
- ZIP: `Porto-0.1.0-rc.3-win-x64.zip`, 51.591.969 byte, 283 voci; runtime 10.0.14 incluso.
- SHA-256: `9680d86d6949d4934f3af64310908b73c71a1874499e5144b7a0caa25195dd23`.
- Pubblicazione GitHub: 2026-10-31 16:39:12 UTC, candidata pubblica con ZIP e checksum.

Suite finale unica: **124/124**, di cui 120 prove ordinarie e quattro estese.
Build senza avvisi o errori. Le prove negative sulla baseline e la mutazione
isolata sono documentate in TESTS.md e REVIEW-RC3.md.

Staging nuovo e vuoto per questa release. Una sentinella sintetica lasciata
appositamente nella vecchia cartella di build non compare nello ZIP. CRC di
tutte le voci valido, manifesto coerente con tag/versione, percorsi controllati.
Nessun SDK, test privato, credenziale, allegato della cliente o configurazione
personale incluso; nessun marcatore del percorso privato nelle quattro binarie Porto.

ZIP e SHA256SUMS.txt riscaricati dai collegamenti pubblici senza autenticazione,
entrambi HTTP 200. Coincidono con gli artefatti locali. Anche GitHub espone il
medesimo digest SHA-256. Archivio riscaricato verificato ed estratto in una nuova
cartella dedicata: smoke COM, worker e UI normale/ridotta tutti superati, senza
enumerare dispositivi. Le due immagini del pacchetto scaricato sono state
ispezionate visivamente; versione rc.3 e pulsanti visibili.

GitHub Pages ha completato senza errori la build del commit della release.
Pagina e anteprima rispondono HTTP 200 e coincidono byte per byte con il sorgente.
I collegamenti puntano a ZIP, checksum e note rc.3. Non è stato ripetuto il
controllo interattivo del browser eseguito per rc.1.

Release e ZIP rc.1/rc.2 conservati; entrambi gli hash locali precedenti invariati.
Nessun costo, nuovo servizio o modifica alle protezioni. Eseguibile non firmato.
Nessuna validazione con iPhone fisico: la compatibilità con telefono, driver e
cavo della cliente resta da provare. Non è una certificazione di zero bug o di
copertura Foto/iCloud. Questo resoconto è successivo al tag; il pacchetto resta immutato.
