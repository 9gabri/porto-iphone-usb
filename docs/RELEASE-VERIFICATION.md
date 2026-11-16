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

## rc.4 — 31 ottobre 2026

- Release candidata: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.4
- Sorgente distribuita: commit `4a60abeeb126f8c09ca156be0847f40674865c3f`, tag `v0.1.0-rc.4`.
- ZIP: `Porto-0.1.0-rc.4-win-x64.zip`, 51.602.369 byte, 283 voci; runtime 10.0.14 incluso.
- SHA-256: `c3ed5b929fcce426fbacc56041392637898f0bf20e2f426db79663ced7ce6505`.
- SHA-256 del file `SHA256SUMS.txt` (96 byte): `6c67a3eb9e2ba0c242f0b9de41a3a9db691f62a36d3832bda54dddc87121ba17`.
- Pubblicazione GitHub: 2026-10-31 17:21:53 UTC, prerelease pubblica con due allegati.

Verificati 192 casi distinti sulla logica finale: 188 ordinari e quattro estesi,
in due esecuzioni. La prima esecuzione combinata terminava 191/192 per una vecchia
aspettativa sul preflight di un hard link; aggiornato soltanto il test e ripetuta
l'intera suite ordinaria, 188/188. Le quattro prove estese erano già passate sulla
medesima logica. Build senza avvisi o errori. Tre cicli massimi di revisione per
area, regressioni negative e mutazione isolata documentate in REVIEW-RC4.md e
TESTS.md. Quattro ulteriori prove sul confezionamento, tutte passate.

Staging nuovo dedicato; compressione e verifica prima di spostare lo ZIP al nome
definitivo, senza sovrascrivere artefatti. Checksum separato per tentativo. CRC di
tutte le voci valido; manifesto e ProductVersion dell'eseguibile coincidono con
versione e commit del tag. Runtime, licenze e istruzioni presenti. Controllati
percorsi ZIP e assenza di duplicati, link, SDK, test, PDB, allegati privati e
configurazioni personali; assenti i marcatori del percorso privato nelle quattro
binarie Porto. Sentinella della vecchia cartella di build esclusa.

ZIP e checksum riscaricati senza autenticazione, entrambi HTTP 200; hash identici
agli originali, inclusi i digest esposti da GitHub. I primi accessi allo ZIP
appena pubblicato hanno restituito 404. Il download completo riuscito ha usato
lo stesso URL pubblico con `?download=1`; il successivo controllo HEAD dell'URL
senza parametri è HTTP 200. Nessuna modifica agli allegati per risolverlo.

Archivio riscaricato controllato nuovamente ed estratto in una nuova cartella.
Superati smoke COM, worker, UI normale, UI ridotta in recupero e UI ridotta in
pausa; nessuna enumerazione o apertura di dispositivi. Ispezionate visivamente
tutte e tre le immagini del pacchetto scaricato, con versione rc.4 e pulsanti
visibili. La verifica UI controlla anche la rimozione della stima dopo la pausa.

GitHub Pages ha completato senza errori la build del commit della release.
HTML e anteprima: HTTP 200 e confronto byte per byte identico ai sorgenti.
Collegamenti a ZIP, checksum e release rc.4 verificati nel contenuto. Il test
interattivo del browser della rc.1 non è stato ripetuto.

I tre ZIP precedenti e il checksum locale della rc.3 sono invariati. Nessun costo,
servizio aggiunto, driver installato o protezione ridotta. Eseguibile non firmato.
Nessun iPhone reale: rimangono da confermare rilevamento, autorizzazione, driver,
cavo, stabilità delle risorse e trasferimento effettivo sul PC della cliente.
La copertura dell'intera libreria Foto e degli originali solo iCloud non è
certificabile con questi test. È una candidata da provare, non una certificazione
di assenza di bug. Questo aggiornamento documentale è successivo al tag;
codice, ZIP e checksum della release rimangono immutati.

## rc.5 — 15 novembre 2026

- Release candidata: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.5
- Sorgente distribuita: commit `6df82ddd935d234a994d32e40321a0a8d5a93283`, tag `v0.1.0-rc.5`.
- ZIP: `Porto-0.1.0-rc.5-win-x64.zip`, 51.607.356 byte, 283 voci; runtime 10.0.14 incluso.
- SHA-256: `3876da28077194aaffa97d5fb131cdbbaf1f5de778d53eba889a7c27ccb27999`.
- SHA-256 del file `SHA256SUMS.txt` (96 byte): `252af66a44e7652090ef10e8ba1164e04e5d8ccb6bac44eb1daad3d1f9a735cd`.
- Pubblicazione GitHub: 2026-11-15 13:29:28 UTC, prerelease pubblica con due allegati.

Suite finale unica **233/233**: 229 prove ordinarie e quattro estese. Quarantuno
nuove regressioni sulle dimensioni delle risorse, fine stream, IPC, AAE e contatori.
Build senza avvisi o errori. Quattro prove di confezionamento separate. Revisione
indipendente, baseline rc.4 e mutazione negativa isolata in REVIEW-RC5.md.
Le prove estese includono 2.000 copie reali sintetiche con secondo passaggio senza
duplicazioni e un contenitore oltre 4 GB scritto, riletto e verificato su NTFS.

Staging nuovo; archivio controllato prima dell'assegnazione del nome finale.
CRC di tutte le voci valido, nessun percorso assoluto/risalita, duplicato o link.
Manifesto e ProductVersion concordano con versione e commit del tag. Runtime,
licenze, istruzioni e risultati presenti. Nessun SDK, test privato, credenziale,
allegato personale, PDB o configurazione locale incluso; assenti i marcatori del
percorso privato nelle quattro binarie Porto. Sentinella della vecchia build esclusa.

ZIP e checksum riscaricati dagli URL pubblici senza autenticazione, entrambi
HTTP 200, usando `?download=1`. Hash identici agli originali e ai digest esposti
da GitHub. Anche il collegamento diretto dello ZIP senza parametri risponde
HTTP 200. Archivio riscaricato ricontrollato ed estratto in una nuova cartella.

Sei smoke superati sul pacchetto riscaricato: COM, worker, UI normale, UI ridotta
in recupero, in pausa e con risultato incompleto. Tutte e quattro le immagini
ispezionate visivamente: versione rc.5 e pulsanti visibili. Gli smoke verificano
anche contatori distinti, rimozione della barra dopo l'arresto e rifiuto di un
aggiornamento tardivo. Nessuna enumerazione o apertura di dispositivi reali.

GitHub Pages: build del commit della release completata senza errori. HTML e
anteprima HTTP 200, identici byte per byte ai file del tag. Collegamenti a ZIP,
checksum e note rc.5 verificati. Nessuna nuova prova interattiva del browser.

I quattro ZIP precedenti e i checksum locali rc.3/rc.4 restano invariati. La
destinazione della prova autorizzata è stata soltanto letta per la diagnosi:
elenco file, dimensioni e date di modifica coincidono con il rilevamento iniziale.
Nessun media personale aperto, modificato, cancellato o pubblicato. Nessuna
installazione, spesa o modifica delle protezioni di Windows.

La rc.4 ha prodotto errori nel collaudo manuale. I test sintetici dimostrano le
correzioni riprodotte, non la causa di tutti gli errori sul dispositivo. La rc.5
richiede un nuovo collaudo fisico in una cartella vuota separata, conservando
intatta la prova precedente. Stabilità del trasferimento, corrispondenza con i
video autonomi attesi e comportamento iOS rimangono da confermare. Nessuna
certificazione dell'intera libreria Foto/iCloud o di assenza di bug. Eseguibile
non firmato. Questo resoconto è successivo al tag; pacchetto e checksum restano
immutati.

## rc.6 — 15 novembre 2026

- Release: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.6
- Sorgente: `5fad0eb863e4e0f9dcb0d20fee23d3116b9db23e`, tag `v0.1.0-rc.6`.
- ZIP: `Porto-0.1.0-rc.6-win-x64.zip`, 51.609.294 byte, 283 voci.
- SHA-256 ZIP: `85b3a7b5281ae5e49830a151c91816c687c306afbd8facafcedc3d34533ba580`.
- SHA-256 `SHA256SUMS.txt` (96 byte): `6e10fcb2497f7239faf481d644467882af5506c5c3a5034e0e2ca8b707f735dd`.
- Pubblicazione: 2026-11-15 20:57:41 UTC, prerelease pubblica con due allegati.

Suite finale unica **253/253** (249 ordinari e quattro estesi), build senza
avvisi/errori. I venti nuovi casi coprono cleanup nativo, wrapper obsoleti,
protocollo della failure precedente, recupero con ID rimappati e motivi delle
ambiguità. Superate anche quattro prove packaging e quattro stati UI separati.
Revisione indipendente e regressioni negative in REVIEW-RC6.md; mutazione
isolata di Cancel rilevata dal test che impedisce a B di ricevere byte di A.

ZIP creato in staging nuovo, verificato prima della pubblicazione. CRC di tutte
le voci valido; percorsi, duplicati, link, file necessari, versione e manifesto
controllati. ProductVersion coincide con commit e versione del tag. Runtime e
licenze inclusi; SDK, test privati, PDB, credenziali e allegati personali esclusi.
Assenti i marcatori dei percorsi privati nelle quattro binarie Porto. La
sentinella della vecchia build è rimasta esclusa. I cinque ZIP precedenti e i
checksum locali controllati rc.3/rc.4/rc.5 sono invariati.

ZIP e checksum riscaricati senza autenticazione dai link pubblici con
`?download=1`, entrambi HTTP 200 e identici agli originali. Anche i digest
GitHub coincidono. Il link diretto ZIP senza parametri risponde HTTP 200.
Archivio riscaricato ricontrollato ed estratto in una nuova cartella.

Sei smoke superati sul pacchetto pubblico: COM, worker e UI normale/ridotta
in recupero, pausa e risultato incompleto. Quattro immagini ispezionate:
versione rc.6, pulsanti visibili, risultato incompleto senza falsa barra piena.
Nessuna enumerazione o apertura di telefoni reali. GitHub Pages ha completato
la build del commit della release; HTML e anteprima HTTP 200 e identici byte
per byte ai file del tag. Verificati i tre link rc.6 (ZIP, checksum, note).

Nessun video personale letto, modificato, cancellato o pubblicato. Nessun accesso
al computer della cliente o al telefono: la diagnosi usa i report allegati.
Nessuna spesa, installazione di driver o riduzione delle protezioni.

La candidata richiede una prova fisica di recupero nella stessa destinazione
rc.5, conservando tutti i file. Le cause di tutti gli errori reali e la differenza
con il numero di video in Foto non sono dimostrate. Un cleanup bloccato può
ancora esaurire i tentativi; errori, pendenti e ambiguità restano espliciti.
Nessuna certificazione di completezza Foto/iCloud o assenza di bug. Eseguibile
non firmato. Questo resoconto è successivo al tag; ZIP e checksum restano immutati.


## rc.7 — 16 novembre 2026

- Release pubblica: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.7
- Sorgente distribuita: commit ad97c9cd41bce5df07aa29d66d942cb99ed1a625, tag v0.1.0-rc.7.
- ZIP: Porto-0.1.0-rc.7-win-x64.zip, 51.611.735 byte, 283 voci, runtime 10.0.14 incluso.
- SHA-256 ZIP: 0aff87c08d2915f22f1dc935286b65904f50d899877f552cc21142971be47c1a.
- SHA-256 SHA256SUMS.txt (96 byte): 75df9f1bf75894188ed138890b1321bcf36394f42c225bd805e4cb6c60407e94.
- Pubblicata il 16 novembre 2026 alle 00:25:20 UTC come prerelease, con due allegati.

Riscaricati ZIP e checksum via HTTPS pubblico senza autenticazione. SHA-256
identici al pacchetto e al checksum locali; CRC di ogni voce, nomi e percorsi,
manifest, versione e commit verificati. Nessun allegato personale, output di
prova, SDK o marcatore di percorso privato incluso. Le sei release precedenti
rimangono presenti con le stesse impronte. Cartella di staging nuova.

Il pacchetto pubblico estratto ha superato smoke COM, worker e quattro stati
UI (normale, ridotto in recupero, pausa ed esito incompleto), con DOTNET_ROOT
puntato a una cartella inesistente per verificare il runtime incluso.
ProductVersion: 0.1.0-rc.7+ad97c9cd41bce5df07aa29d66d942cb99ed1a625.
Le quattro viste del pacchetto locale sono state ispezionate visivamente:
pulsanti accessibili, contenuto scorrevole, versione corretta. Gli smoke non
eseguono ricerca o apertura di telefoni. Nessuna protezione Windows modificata.

Pagina pubblica e immagine: HTTP 200, byte identici ai rispettivi file del tag.
I tre collegamenti ZIP/checksum/release puntano alla rc.7.

Verifica applicativa: 265 test ordinari finali e quattro estesi superati in
due esecuzioni, sul medesimo codice applicativo. Un'aspettativa storica sul
riuso del worker è stata aggiornata; tutta la suite ordinaria è stata rieseguita.
Dettaglio trasparente in TESTS.md e test-results.txt. Build senza avvisi/errori,
quattro prove di confezionamento, 16 regressioni nuove, baseline negativa e
mutazione, revisione indipendente. Nessun costo, driver o servizio aggiunto.

Resta da eseguire la normale importazione sullo specifico iPhone: i test non
dimostrano il recupero dei video reali. ID sempre diversi tra riconnessioni,
timeout persistenti, classificazione Live Photo/varianti e risorse solo iCloud
restano limiti dichiarati. I file della prova reale non sono stati modificati.


## rc.8 — verifica dopo pubblicazione (16 novembre 2026)

Tag/source: `v0.1.0-rc.8`, `675ec7db770e3af84c23abc2b709d51be7d2d16f`.
Release pubblica prerelease, pubblicata 2026-11-16T01:21:31Z:
https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.8

- ZIP: Porto-0.1.0-rc.8-win-x64.zip, 51.614.821 byte, 283 voci.
- SHA-256 ZIP: `97656c6274141727fe9ae01fee7769732a0ee9e61d9909b43ed09bdca40c6b67`.
- SHA256SUMS.txt: 96 byte, SHA-256 `eea0b0ecd4aac5fa4f118d52249d0df623ffffa0b96fdd699abb83af2ddf9629`.

ZIP e checksum riscaricati via HTTPS pubblico senza autenticazione: identici
agli originali locali, coerenti anche con i digest della release GitHub.
Verificati CRC di ogni voce, percorsi, manifest, versione, commit, runtime e
licenze. Assenti allegati, output personali, SDK e marcatori di percorsi privati.
Le sette release precedenti sono conservate; archivi locali con le stesse impronte.
Confezionamento in staging nuovo, nessun file di build estraneo incluso.

Pacchetto pubblico estratto in cartella nuova: sei smoke superati (COM, worker,
UI normale, recupero, pausa, incompleto), con DOTNET_ROOT e DOTNET_ROOT_X64
puntati a una cartella inesistente. ProductVersion:
`0.1.0-rc.8+675ec7db770e3af84c23abc2b709d51be7d2d16f`.
Nessuno smoke ricerca o apre telefoni. Interfaccia ispezionata: versione corretta,
pulsanti visibili, riepilogo distinguibile e contenuto scorrevole.

Pagina e immagine pubbliche HTTP 200, byte identici al tag:
- index.html SHA-256 `2fc0db686d3e790c6602de1e60d7ed6c8785439a2068f725da6b12797e1a2058`.
- app-preview.png SHA-256 `1c38888a20e032f6b9210fcc25ea31fc7c9df2dbef01fa3a385e8252a4d751c4`.

Verifiche: suite 322/322 e 48 ricontrolli pertinenti dopo l’ultimo aggiustamento,
quattro prove packaging, build senza avvisi/errori e revisione indipendente.
Dettagli e distinzione delle esecuzioni in TESTS.md/test-results.txt.
Nessun costo, driver, servizio, riduzione di protezioni o accesso a media personali.

Resta la normale importazione con lo specifico iPhone. Nessun totale Foto fisso
è garantito; risorse aggiuntive, contenuti non esposti via USB e originali solo
cloud non sono una prova di copertura dell’intera libreria. La candidata non è
presentata come certificata o definitivamente compatibile.


## rc.9 — pubblicazione e riscaricamento del 2026-11-16

- Tag `v0.1.0-rc.9`, sorgente `5a510f63c4ca5c90dbbce7eaee16ca581a27fe44`.
- Release pubblica candidata: https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.9
- Pubblicata alle `2026-11-16T12:41:28Z`; prerelease, nessuna certificazione hardware.
- ZIP: `Porto-0.1.0-rc.9-win-x64.zip`, 51.626.126 byte, 284 elementi.
- SHA-256 ZIP: `8e8c2f504c8d46cb05c90e746894a5f955cbcede2037c64e8f7b7986e9d8b846`.
- SHA-256 del file checksum pubblico (96 byte): `8553aaa3cc680ad53f39b9a54299c7887470a9e96c8e722052ab9b0f1c444ed3`.
- Caricati esclusivamente ZIP e checksum; codice/documentazione del progetto nel repository pubblico. Allegati privati, media, report personali, cache e fixture di esecuzione esclusi.
- ZIP e checksum riscaricati dal link pubblico senza autenticazione. SHA-256 locale, contenuto del checksum e digest dell’asset GitHub coincidenti.
- CRC, nomi univoci senza traversal/link, manifest/versione/commit, runtime 10.0.14, licenze, guida cartella e risultati controllati; nessun indicatore di percorso personale nei binari del progetto, nessun PDB o materiale estraneo.
- Pacchetti rc.1–rc.8 e checksum precedenti verificati invariati; nessuna sostituzione di tag o archivi.
- Eseguibile riscaricato: `0.1.0-rc.9+5a510f63c4ca5c90dbbce7eaee16ca581a27fe44`.
- Sette smoke superati sulla copia pubblica estratta in una cartella nuova: interop COM senza enumerazione, protocollo worker, UI cartella iniziale, recupero USB compatto, pausa, esito USB incompleto, esito cartella compatto. `DOTNET_ROOT` e `DOTNET_ROOT_X64` impostati a un percorso inesistente: runtime incluso sufficiente. Nessun telefono cercato o letto.
- Pagina pubblica e `app-preview.png` riscaricati via HTTPS: byte coincidenti con il tag. Immagine dell’app corrente esaminata.

Test del codice: 348 casi distinti coperti, sequenza esatta in TESTS.md (suite 347/347 e ricontrolli 28/28 dopo l’aggiunta di un caso). Il chiarimento finale del testo per file non registrati non cambia la logica. Esportazione reale da Foto e confronto con l’archivio personale ancora da eseguire con l’utente; nessun totale Foto garantito.


## 0.1.0-rc.10 — 16 novembre 2026

- Tag immutabile `v0.1.0-rc.10`, sorgente `73d803595b535842a73ff4f3d20a87da8a25983a`.
- [Release pubblica](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.10), candidata pubblicata alle 15:02:09 UTC.
- [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.10/Porto-0.1.0-rc.10-win-x64.zip): 51.653.759 byte, 285 elementi.
- SHA-256: `a94fa59543a96ad07887180dc2aadc08a223657a7ee98e824b8e62aeffb61c69`.
- [SHA256SUMS.txt](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.10/SHA256SUMS.txt): 97 byte; SHA-256 `258746c071417bab7b98011f6c699032814c79be953bf0072888fc0ff96f30d9`.
- Nuovo staging isolato, manifest/versione/commit coerenti, runtime 10.0.14 incluso, assenza di percorsi privati nei binari, nessun allegato personale, simbolo esterno, credenziale o vecchio file sentinella.
- ZIP riscaricato senza autenticazione dal link pubblico: checksum, CRC, nomi, duplicati, percorsi e manifest ricontrollati prima dell’estrazione in una nuova cartella.
- Eseguibile riscaricato: versione informativa `0.1.0-rc.10+73d803595b535842a73ff4f3d20a87da8a25983a`. Nove smoke superati con DOTNET_ROOT e DOTNET_ROOT_X64 indirizzati a una cartella inesistente: interop, worker, sette stati UI (compreso selettore cartella). Nessuna ricerca o lettura di telefoni.
- HTML e immagine della pagina pubblica confrontati byte per byte con quelli del tag: corrispondono.
- Gli archivi rc.1–rc.9 e i checksum storici controllati sono rimasti invariati. Il primo tentativo di packaging si è fermato nel restore limitato dal sandbox, prima di produrre lo ZIP; il tentativo successivo ha usato un nuovo stage, senza sovrascrivere precedenti release.
- Suite applicativa 387/387, sette controlli UI e quattro prove del confezionamento documentati in TESTS.md.

Questa verifica dimostra integrità e avvio del pacchetto distribuito, non compatibilità con ogni iPhone/Android, accessibilità degli album reali, assenza di bug o completezza di una libreria cloud. Il collaudo fisico resta necessario.

## 0.1.0-rc.11 — 16 novembre 2026

- Tag immutabile `v0.1.0-rc.11`, sorgente `0e6aa1405bb473bcbdb8a1434c4835124febe526`.
- [Release pubblica](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.11), candidata pubblicata alle `2026-11-16T16:54:24Z`.
- [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.11/Porto-0.1.0-rc.11-win-x64.zip): 54.360.498 byte, 291 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: `ed786653fa1bd41d9a1b40cd52b6556b231ac0ab6167821f56191be19d0e2fba`.
- [SHA256SUMS.txt](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.11/SHA256SUMS.txt): 97 byte, SHA-256 `aa2486a6f1420d17344e668135068c6426f04f45dbdf6966816121952d10184d`.
- Nuovo staging vuoto e ZIP nuovo; manifest/versione/commit coerenti, licenze .NET/BouncyCastle/Apache incluse. CRC di tutti i membri verificato, nessun nome duplicato, traversal, link o percorso personale nei binari Porto. Nessun SDK, PDB esterno, database del telefono, credenziale, media o allegato personale incluso.
- ZIP e checksum riscaricati via HTTPS senza autenticazione. Hash locale, file checksum pubblico e digest dell'asset GitHub coincidono. Tutti i controlli di contenuto e CRC ripetuti sul download, estratto solo dopo verifica in una nuova cartella del progetto.
- Eseguibile riscaricato: `0.1.0-rc.11+0e6aa1405bb473bcbdb8a1434c4835124febe526`. Undici smoke passati: interop COM, worker, nove stati UI (inclusi galleria iPhone e selettore album). `DOTNET_ROOT` e `DOTNET_ROOT_X64` impostati a un percorso inesistente, nessun runtime globale richiesto. Nessuna ricerca o lettura di telefoni.
- HTML pubblico e anteprima riscaricati: identici byte per byte ai file del tag. Controllata visivamente la UI sintetica iPhone. Nessuna nuova attestazione di test interattivo in browser o di compatibilità hardware.
- Tutti e dieci gli archivi locali rc.1–rc.10 ricontrollati: checksum invariati. Nessun vecchio tag o asset sostituito.
- Codice: suite ordinaria 445/445, quattro prove estese, poi ricontrolli finali Apple 37/37 e motore galleria 6/6 dopo quattro nuove regressioni. Sequenza, prima esecuzione con fallimenti e successive correzioni documentate in TESTS.md e REVIEW-RC11.md; nessuna singola esecuzione globale verde inventata. Quattro prove di confezionamento aggiuntive superate.

Questo resoconto è successivo al tag: lo ZIP resta immutato. La candidata non è firmata commercialmente e non è certificata. Il nuovo accesso al catalogo Foto usa uno schema privato e deve essere collaudato con un iPhone reale; album personali, originali locali, riconnessione e conteggi restano da confermare. Raccolte intelligenti/condivise iPhone e album privati/cloud Android non sono universalmente accessibili. Non è stato raggiunto né dichiarato il supporto a qualsiasi telefono e qualsiasi album.
