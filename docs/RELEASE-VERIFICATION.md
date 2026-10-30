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
