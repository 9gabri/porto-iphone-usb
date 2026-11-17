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

## 0.1.0-rc.12 — 16 novembre 2026

- Tag immutabile `v0.1.0-rc.12`, sorgente `04430f1f283e4af9d237e8802904671b7e50d538`.
- [Release pubblica](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.12), candidata pubblicata alle `2026-11-16T18:29:07Z`.
- [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.12/Porto-0.1.0-rc.12-win-x64.zip): 54.362.423 byte, 292 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: `c7a0c68fe3ceebefc5f3acd716fbf8a9d53d7b377de665a9fff446cf481a4d65`.
- [SHA256SUMS.txt](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.12/SHA256SUMS.txt): 97 byte, SHA-256 `da24794022857c712f4585d01cc451b20d0e967d0c84858818340909b4c57d22`.
- Nuovo staging e nuovo ZIP, senza sostituire le release precedenti. Tutti gli undici archivi locali rc.1–rc.11 ricontrollati e invariati.
- ZIP e checksum riscaricati dal link pubblico senza autenticazione: hash locale, checksum pubblico e digest GitHub coincidenti. CRC di tutti i membri, nomi univoci, assenza di traversal/link, manifest/versione/commit, runtime, licenze e documentazione verificati. Nessun dato personale della prova, media, allegato, SDK, PDB, catalogo o credenziale incluso; nessun percorso privato del progetto trovato nei binari Porto.
- Eseguibile pubblico: `0.1.0-rc.12+04430f1f283e4af9d237e8802904671b7e50d538`. Estratto in una cartella nuova e verificato con undici smoke: interop COM, worker e nove stati UI. Runtime incluso sufficiente con `DOTNET_ROOT`/`DOTNET_ROOT_X64` inesistenti e `DOTNET_MULTILEVEL_LOOKUP=0`. Nessun telefono cercato o letto.
- HTML e anteprima pubblici identici byte per byte ai file del tag. SHA-256 HTML `ea9c7da6de59369b621e05af91e8c543601933259d3d0f17fc25ad4c84e9bc35`; anteprima `43e11df2375d2e83cf7de416a069593f152097a66b3c4ae58c26f9e05266dd21`.
- Codice finale: 457/457 test ordinari, build senza avvisi/errori, quattro prove di confezionamento. Baseline con fallimenti, successiva correzione, recupero sintetico 48+29 e revisione indipendente documentati in TESTS.md, test-results.txt e REVIEW-RC12.md.
- Controllo strutturale autorizzato dei 29 JPEG reali soltanto in lettura: nuovo lettore 29/29 accettati, precedente 29/29 rifiutati, hash invariati e coincidenti con il report originale. Nessuna immagine visualizzata o trasferita, nessun catalogo personale aperto in scrittura e nessuna importazione reale effettuata.

Questo resoconto è successivo al tag e non modifica lo ZIP. Resta da completare l'importazione fisica con il telefono dell'utente. La correzione riguarda il lettore JPEG comune a tutte le sorgenti, non garantisce ogni formato o dispositivo. I dati dopo l'immagine primaria sono conservati e dichiarati non interpretati né strutturalmente verificati. La candidata non è firmata commercialmente, certificata o dichiarata priva di bug.

## 0.1.0-rc.13 — 16 novembre 2026

- Tag immutabile `v0.1.0-rc.13`, sorgente `edc101e609497ac53cb58c8bc5d90c696542a23b`.
- [Release pubblica](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.13), candidata pubblicata alle `2026-11-16T20:16:40Z`.
- [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.13/Porto-0.1.0-rc.13-win-x64.zip): 54.369.089 byte, 292 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: `9385770ac8520e5ce4d69dab4f10e9b5e5151b31704d8b00ccd3a40225a9cb98`.
- [SHA256SUMS.txt](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.13/SHA256SUMS.txt): 97 byte, SHA-256 `8c712e2a65a5f05bb80a09003653240beee793c5ef700526faca36ad2a2ee572`.
- Nuovo staging e nuovo ZIP. Il primo processo di confezionamento si è interrotto dopo il publish, senza produrre l'archivio finale; conservato e ripetuto in un nuovo staging. Il secondo tentativo ha completato gli undici smoke e la convalida dell'archivio. Nessuna modifica al codice per il tentativo successivo.
- ZIP e checksum riscaricati via HTTPS senza autenticazione. SHA-256 locale, checksum pubblico e digest GitHub coincidono. CRC, percorsi univoci, assenza di traversal/link, manifest/versione/commit, runtime, licenze e documenti verificati. Nessun allegato, media, catalogo personale, credenziale, SDK o PDB esterno incluso; nessun percorso privato del progetto nei binari Porto.
- Eseguibile pubblico: `0.1.0-rc.13+edc101e609497ac53cb58c8bc5d90c696542a23b`. Estratto in una nuova cartella; undici smoke passati: interop COM, worker, nove stati UI, inclusi iPhone e album sintetici. `DOTNET_ROOT` e `DOTNET_ROOT_X64` inesistenti, `DOTNET_MULTILEVEL_LOOKUP=0`: avvio con runtime incluso. Nessuna enumerazione o lettura di telefoni. Selettore album pubblico controllato visivamente.
- HTML e anteprima della pagina pubblica riscaricati e identici ai file del tag. SHA-256 HTML `c96b6e1ac35ca4d034bdfd164d59a85ede0e20edf9bc01cad7c19bd3a5a815ff`; anteprima `307493102e70f3a26a80bc5beedcdd92cd013f3768bf8bd1d9fceb01bc3a64e7`.
- Dodici archivi rc.1–rc.12 ricontrollati e invariati. Nessun vecchio asset o tag sostituito; file delle prove personali non consultati né modificati in questo ciclo.
- Suite integrata 479/479 (475 ordinari e quattro estesi, inclusi 2.000 file copiati/ricontrollati e un file NTFS oltre 4 GiB). Dopo il solo chiarimento testuale dei conteggi iniziali: build senza avvisi/errori, 16/16 casi report e due smoke UI. Quattro prove del confezionamento. Sequenza, riproduzioni prima delle correzioni e riesami indipendenti in TESTS.md, test-results.txt e REVIEW-RC13.md.

Questo resoconto è successivo al tag e non modifica lo ZIP. Nessun test reale del percorso Apple è stato eseguito: restano da confermare servizio/abbinamento, TLS/AFC, lettura e schema Photos.sqlite, album, originali locali, trasferimento e ripresa sull'iPhone della cliente. I test Oppo precedenti non dimostrano tali capacità. Candidata non firmata commercialmente, non certificata e senza promessa di totale fisso o compatibilità universale. Nessuna API a consumo, acquisto, installazione di driver/servizi o riduzione delle protezioni.

## 0.1.0-rc.14 — 17 novembre 2026

- Tag immutabile `v0.1.0-rc.14`, sorgente `adba0aad8c642b9a9bb0c6ac2c9363bbbf45b33f`.
- [Release pubblica](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.14), candidata pubblicata alle `2026-11-16T23:09:18Z` (17 novembre in Italia).
- [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.14/Porto-0.1.0-rc.14-win-x64.zip): 54.368.195 byte, 292 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: `ba3270eed89d782f7803fa586a69ce489936431cb29daef06f37a18412da050e`.
- [SHA256SUMS.txt](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.14/SHA256SUMS.txt): 97 byte, SHA-256 `d5058991f264924d4a75c38881dcd22850631452ca1c70d837bd1fa0728e16e1`.
- Nuovo staging, nuovo ZIP, tredici archivi rc.1–rc.13 ricontrollati e invariati. Nessun vecchio tag o asset sostituito e nessun file delle prove personali letto o modificato in questo ciclo.
- ZIP e checksum riscaricati via HTTPS senza autenticazione. SHA-256 locale, checksum pubblico e digest GitHub coincidono. CRC di tutti gli elementi, percorsi univoci, assenza di traversal/link, manifest/versione/commit, runtime, licenze e documentazione verificati. Nessun media, allegato personale, catalogo del telefono, credenziale, SDK o PDB esterno incluso; nessun percorso privato del progetto trovato nei binari Porto.
- Eseguibile riscaricato: `0.1.0-rc.14+adba0aad8c642b9a9bb0c6ac2c9363bbbf45b33f`. Estratto in una nuova cartella, undici smoke passati: interop COM, worker e nove stati UI. Avvio con runtime incluso verificato con `DOTNET_ROOT` e `DOTNET_ROOT_X64` inesistenti e `DOTNET_MULTILEVEL_LOOKUP=0`. Nessuna enumerazione o lettura di telefoni. Schermata iPhone sintetica controllata visivamente.
- HTML e anteprima della pagina pubblica riscaricati e identici byte per byte ai file del tag. Pubblicazione GitHub Pages completata sul commit della candidata.
- Codice finale: 490/490 test ordinari, compilazione senza avvisi/errori. Inclusi quindici nuovi casi TLS, con quattro incompatibilità riprodotte prima della correzione contro il binario rc.13. Le quattro prove estese rc.13 non sono state ripetute per questa modifica TLS. Sequenza e revisione indipendente in TESTS.md, test-results.txt e REVIEW-RC14.md.

Questo resoconto è successivo al tag e non modifica lo ZIP. La causa precisa del fallimento TLS sul telefono reale resta da confermare: i test sintetici usano BouncyCastle anche sul lato server. Le correzioni delle firme e la nuova diagnostica non dimostrano da sole il funzionamento su iPhone. Restano da collaudare autenticazione, catalogo, album, originali locali, copie e ripresa. TLS 1.2, cifratura AES-GCM, controllo esatto del certificato abbinato e autenticazione reciproca restano richiesti. Nessuna spesa, installazione o riduzione delle protezioni; candidata non firmata commercialmente e non certificata.

## 0.1.0-rc.15 — 17 novembre 2026

- Tag immutabile `v0.1.0-rc.15`, sorgente `75013c8948c2086345d1ef9a0e008771623567f1`.
- [Release pubblica](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.15), candidata pubblicata alle `2026-11-16T23:35:46Z` (17 novembre in Italia).
- [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.15/Porto-0.1.0-rc.15-win-x64.zip): 54.369.588 byte, 292 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: `3b6215178ae9839f7fd4ff0c66eb34559762563e9375d4f4f234ccf12f584305`.
- [SHA256SUMS.txt](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.15/SHA256SUMS.txt): 97 byte, SHA-256 `05bd5eafa30bef808dd346a79fbb1b381490bc3d7f0e33e9fbea21279de290d1`.
- Nuovo staging e nuovo ZIP; quattordici archivi rc.1–rc.14 ricontrollati e invariati. Nessun vecchio tag o asset sostituito. Nessun telefono, record di pairing o file delle prove personali consultato o modificato.
- ZIP e checksum riscaricati via HTTPS senza autenticazione. Hash locale, checksum pubblico e digest GitHub coincidenti. CRC di tutti gli elementi, nomi univoci, assenza di traversal/link, manifest/versione/commit, runtime, licenze e documentazione verificati. Nessun media, allegato personale, catalogo del telefono, credenziale, SDK o PDB esterno incluso; nessun percorso privato del progetto nei binari Porto.
- Eseguibile riscaricato: `0.1.0-rc.15+75013c8948c2086345d1ef9a0e008771623567f1`. Estratto in una nuova cartella; undici smoke passati (interop COM, worker, nove stati UI) con `DOTNET_ROOT` e `DOTNET_ROOT_X64` inesistenti e `DOTNET_MULTILEVEL_LOOKUP=0`. Avvio con runtime incluso, nessuna ricerca o lettura di telefoni.
- HTML e anteprima della pagina pubblica riscaricati, identici byte per byte ai file del tag. Pubblicazione GitHub Pages completata sul commit della candidata. Anteprima compilata rc.15 controllata visivamente con dati sintetici.
- Suite finale ordinaria 503/503, build test/app senza avvisi/errori. Tredici nuove regressioni per certificati di abbinamento; quattro casi riproducono LOCAL-42 contro la rc.14 e passano con la correzione. Revisione indipendente del codice e dei test senza difetti bloccanti. Due tentativi preliminari interrotti per configurazione del lanciatore/sandbox e prove estese non ripetute sono dichiarati in TESTS.md.

Questo resoconto successivo al tag non modifica lo ZIP. Il difetto riprodotto è coerente con l'errore reale, ma il certificato dell'iPhone non è stato letto: la causa sul dispositivo e la riuscita dopo la correzione restano da confermare. I test usano un server TLS sintetico e un codec di certificati indipendente dall'adattatore di produzione. Restano da provare collegamento, catalogo, album, originali locali, copia e ripresa reali. Certificati originali, pin, autenticazione reciproca e protocolli richiesti sono conservati; nessuna spesa, nuova dipendenza, installazione o riduzione delle protezioni di Windows. Candidata non firmata commercialmente e non certificata.

## 0.1.0-rc.16 — 17 novembre 2026 — candidata diagnostica

- Tag immutabile `v0.1.0-rc.16`, sorgente `244930d23754303e147626fc2af673caf344d008`.
- [Release pubblica](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.16), candidata pubblicata alle `2026-11-16T23:52:00Z` (17 novembre in Italia).
- [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.16/Porto-0.1.0-rc.16-win-x64.zip): 54.364.035 byte, 292 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: `cb3ceadffb3fe30dede8c413ab1170e3e51188a4f19d2a2953fd9ecb8d898473`.
- [SHA256SUMS.txt](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.16/SHA256SUMS.txt): 97 byte, SHA-256 `d8389ecefc046df9d5da216d7163e32eca471e93655673436782296ec3c7656a`.
- Nuovo staging e nuovo ZIP; quindici archivi rc.1–rc.15 ricontrollati e invariati. Nessun tag o asset precedente sostituito. Nessun telefono, record di pairing o file delle prove personali aperto o modificato.
- ZIP e checksum riscaricati via HTTPS senza autenticazione; hash locale, checksum pubblico e digest GitHub coincidenti. CRC di tutti gli elementi, nomi univoci, assenza di traversal/link, manifest/versione/commit, runtime, licenze e documentazione verificati. Nessun media, allegato personale, catalogo del telefono, credenziale, SDK o PDB esterno incluso; nessun percorso privato del progetto nei binari Porto.
- Eseguibile pubblico `0.1.0-rc.16+244930d23754303e147626fc2af673caf344d008`, estratto in una nuova cartella. Dodici smoke passati: interop COM, worker e dieci stati UI, incluso il nuovo errore completo. Avvio con runtime incluso verificato con `DOTNET_ROOT` e `DOTNET_ROOT_X64` inesistenti e `DOTNET_MULTILEVEL_LOOKUP=0`. Nessuna ricerca o lettura di telefoni. Schermata diagnostica pubblica controllata visivamente.
- HTML e anteprima pubblici riscaricati e identici byte per byte ai file del tag. SHA-256 HTML `bd72fd90ae16cd8b5c9612f9633d36130d0758bfb1a381ca64a0ffc8bace265d`; anteprima `2cd1e64f7c6653a5c8d2ea4663495531ca0f105d0654db2113bd4ff9fdce6f4f`.
- Undici nuovi casi diagnostici passati; verifica finale pertinente Apple 88/88 e importazione galleria 12/12, build senza avvisi/errori. Suite generale ed estese precedenti non ripetute. Riproduzione, limiti e revisione indipendente in TESTS.md e REVIEW-RC16.md.

Questo resoconto successivo al tag non modifica lo ZIP. La foto rc.15 mostra che TLS e accesso AFC hanno raggiunto il controllo dei metadati; il messaggio aggregato non identifica la causa del rifiuto. La rc.16 distingue dimensioni e tipo: **non risolve ancora il blocco e non rende pronta l'importazione album iPhone**. Occorre acquisire il nuovo codice e i numeri dal computer della cliente. Limite di memoria invariato, nessun collegamento seguito, nessun database del telefono salvato su disco e nessuna protezione ridotta. Nessuna spesa. Candidata diagnostica non firmata commercialmente e non certificata.

## 0.1.0-rc.17 — 17 novembre 2026

- Tag immutabile v0.1.0-rc.17, sorgente e6ae78dfaf0f7672fcd3c25f04b36ac849767dba. Pubblicazione candidata: 2026-11-17T00:09:32Z.
- [Release](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.17), [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.17/Porto-0.1.0-rc.17-win-x64.zip): 54.373.396 byte, 292 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: 71ae5d6eb9064493522129fab51387959b47173717548d6e3db18940f38f970b. File SHA256SUMS.txt: 97 byte, SHA-256 4304485661da58f0f6787405795d6acf6b1c80a520893425197a158eccbe0c75.
- Nuovo staging, nuovo ZIP; sedici archivi rc.1–rc.16 ricontrollati e invariati. Nessun telefono, record di pairing, catalogo personale o file delle prove aperto o modificato. Soltanto fixture sintetici.
- ZIP e checksum riscaricati via HTTPS senza autenticazione. Hash locale, checksum pubblico e digest GitHub coincidono. Verificati CRC, percorsi univoci e privi di traversal/link, manifest, versione, commit, runtime, licenze e contenuto. Nessun media, allegato personale, catalogo del telefono, credenziale, SDK o PDB esterno incluso; nessun percorso privato del progetto nei binari Porto.
- Versione pubblica 0.1.0-rc.17+e6ae78dfaf0f7672fcd3c25f04b36ac849767dba, estratta in una nuova cartella. Dodici smoke passati: interop COM, worker e dieci stati UI. Runtime incluso verificato con DOTNET_ROOT e DOTNET_ROOT_X64 inesistenti e DOTNET_MULTILEVEL_LOOKUP=0. Nessuna ricerca o lettura di telefoni.
- HTML e anteprima pubblici riscaricati e identici byte per byte ai file del tag. Schermata diagnostica compatta del build finale controllata visivamente.
- Suite ordinaria finale 528/528, build test/app senza avvisi/errori. Prova estesa Apple 3/3: due regressioni dimensionali più un catalogo SQLite valido di 566362112 byte con WAL di 4227152 byte, due letture complete, integrità, album e hash sorgenti invariati. Misure e limiti in TESTS.md. Le precedenti prove estese non Apple non sono state ripetute.
- Revisione indipendente senza blocchi residui nel diff. La proposta di puntare direttamente a un buffer senza spazio aggiuntivo è stata respinta per conservare il margine nativo SQLite di 32 byte. Nessuna protezione Windows modificata, nuova dipendenza installata o spesa.

Questo resoconto successivo al tag non modifica lo ZIP. La correzione del limite DB-LIMIT è riprodotta e verificata con dati sintetici; restano da confermare sul dispositivo reale schema iOS, coerenza del catalogo, lista album, originali locali, trasferimento e ripresa. La disponibilità di memoria viene misurata, non prenotata. La candidata non garantisce qualsiasi dimensione di catalogo o compatibilità universale e non è firmata commercialmente né certificata.

## 0.1.0-rc.18 — 17 novembre 2026

- Tag immutabile v0.1.0-rc.18, sorgente b63992cb7232145ab562d693201a058f575985cc. Candidata pubblicata alle 2026-11-17T00:26:19Z.
- [Release](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.18), [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.18/Porto-0.1.0-rc.18-win-x64.zip): 54.366.779 byte, 292 elementi, .NET 10.0.14 incluso.
- SHA-256 ZIP: 53e56b51f863dbcc8b40c73332f0e4a094db341d3de34caf16c7397955bddbb5. SHA256SUMS.txt: 97 byte, SHA-256 d814319be21175e0e4c2c96a7280f79e842e3327f5d546d3499dcece86fbf3c9.
- Nuovo staging e nuovo archivio; diciassette ZIP precedenti ricontrollati e invariati. Nessun telefono, catalogo personale, media, pairing o file delle prove aperto o modificato.
- Download HTTPS senza autenticazione: hash locale, checksum pubblico e digest GitHub identici. Controllati CRC, percorsi univoci, assenza di traversal/link, manifest/versione/commit, runtime, licenze e documentazione. Nessun media, allegato personale, database del telefono, credenziale, SDK o PDB esterno incluso; nessun percorso privato del progetto nei binari Porto.
- Eseguibile pubblico 0.1.0-rc.18+b63992cb7232145ab562d693201a058f575985cc, estratto in nuova cartella. Tredici smoke passati: interop COM, worker e undici stati UI, incluso selettore con album integro/bloccato e verifica del pulsante. Runtime incluso provato con DOTNET_ROOT e DOTNET_ROOT_X64 inesistenti e DOTNET_MULTILEVEL_LOOKUP=0, senza enumerare telefoni.
- HTML e anteprima pubblici riscaricati e identici byte per byte al tag. Selettore sintetico locale controllato visivamente; dimensioni delle immagini smoke pubbliche verificate.
- Build test/app senza avvisi/errori. Test Apple 109/109, importazione galleria 12/12, poi suite dedicata finale 9/9 (sette già compresi nei 109), 123 casi distinti. Suite generale e catalogo grande rc.17 non ripetuti. Due controesempi riprodotti contro il binario pubblico rc.17. Revisione indipendente senza difetti bloccanti nel diff; dettagli in TESTS.md e REVIEW-RC18.md.

Resoconto successivo al tag, ZIP invariato. La correzione isola gli album integri: non ripara automaticamente le associazioni sconosciute. Gli album coinvolti restano visibili e bloccati, e qualsiasi associazione irrisolta mantiene bloccata la galleria completa. La causa concreta sul telefono e la selezionabilità dell'album desiderato restano da confermare, insieme a copia e ripresa reali. Nessuna spesa, installazione o riduzione delle protezioni. Candidata non firmata commercialmente e non certificata.

## 0.1.0-rc.19 — 17 novembre 2026

- Tag v0.1.0-rc.19, sorgente fef8d242136bcd191d04f5171095d92dc67533f2. Candidata pubblicata alle 2026-11-17T13:52:03Z. Branch sorgente codex/performance-rc19.
- [Release](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.19), [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.19/Porto-0.1.0-rc.19-win-x64.zip): 54.384.351 byte, 292 elementi, runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: b72446b9510c547d1779eb6da06e57533e6df689ebe1c5d185ebfd9a935bf743. SHA256SUMS.txt: 97 byte, SHA-256 23086fcd3e8b70595d969e1633b8e26bf045e4542729864212aa9b330f73c25c.
- Nuovo staging e ZIP, 18 archivi rc.1–rc.18 ricontrollati e invariati. Screenshot del packaging in una cartella nuova; quelli precedenti conservati. Nessun telefono, record di pairing, video/foto o archivio personale delle prove consultato o modificato.
- Riscaricati ZIP e checksum via HTTPS senza autenticazione. Hash locale, checksum pubblico e digest GitHub corrispondono. Tutti gli elementi decompressi e controllati; percorsi univoci, senza traversal/link. Manifest/versione/commit corretti. Esclusi allegati, media personali, cataloghi del telefono, SDK, credenziali e PDB esterni. Nessun percorso privato del progetto nelle DLL Porto.
- Copia pubblica estratta in una nuova directory. Versione eseguibile 0.1.0-rc.19+fef8d242136bcd191d04f5171095d92dc67533f2. Undici smoke passati: COM senza dispositivi, protocollo worker senza dispositivi, nove stati UI sintetici. Runtime incluso verificato con DOTNET_ROOT e DOTNET_ROOT_X64 inesistenti e DOTNET_MULTILEVEL_LOOKUP=0. Nessuna ricerca di telefoni. Schermata compatta iPhone controllata visivamente prima della pubblicazione; quella pubblica è identica per SHA-256.
- Build senza avvisi/errori. Controllo NuGet delle vulnerabilità mantenuto: il primo restore nel sandbox non aveva accesso alla rete; ripetuto con accesso a nuget.org, senza cambiare dipendenze né disattivare controlli. La configurazione win-x64 del lockfile resta identica al tag.
- 553 casi ordinari verificati per incrementi (545 al primo giro e otto fixture NTFS ripetute nell'ambiente che ne consente la creazione); ultimi 16 casi rc.19 passati dopo gli aggiustamenti dei contatori. Tre prove estese passate: replay oltre 4 GiB con singola lettura sorgente, percorso ordinario oltre 4 GiB e catalogo SQLite/WAL grande. Quattro casi packaging passati. Benchmark comparativo su tre ripetizioni: traffico sorgente 48 -> 24 MiB, mediana 4,47 -> 2,84 s con attese artificiali, non USB reale. Dettagli e limiti in REVIEW-RC19.md e TESTS.md.

Resoconto successivo al tag: il pacchetto non viene modificato. Nessuna spesa, installazione di servizi/driver o riduzione delle protezioni. Le revisioni indipendenti della diagnosi sono state disponibili; una nuova revisione finale delegata dell'implementazione non è stata completata per limite di quota, senza fallback a pagamento. La candidata resta da provare sull'iPhone: non garantisce 29 GB/800 video entro un'ora, compatibilità universale o assenza di blocchi fisici. Controlli catalogo, integrità, molteplicità, ripresa e protezione dei file precedenti conservati. App non firmata commercialmente e non certificata.

## 0.1.0-rc.20 — 17 novembre 2026

- Tag immutabile v0.1.0-rc.20, commit `00698da4cbeb138d7bdeec923254ac9e421f7748`, branch `codex/performance-rc20`. Candidata pubblicata alle 2026-11-17T15:44:45Z; verifica pubblica conclusa alle 15:45:42Z.
- [Release](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.20), [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.20/Porto-0.1.0-rc.20-win-x64.zip): 54.386.485 byte, 292 elementi, 128.827.889 byte decompressi. Runtime .NET 10.0.14 incluso.
- SHA-256 ZIP: `2f8a0da0d3a8646a42d37a8fc6ba9c734a96ce823ef68ff0e429dfa852d797cf`. Checksum pubblico SHA256SUMS.txt: 97 byte, digest `a7a001576af27eaaf315ef47399f96a6c15f4bb4193e3cf38d8eb317d57d6b3f`.
- Nuovi staging, archivio, cartella di download e screenshot. Diciannove archivi precedenti ricontrollati e invariati. Nessun file personale delle prove, telefono o record di abbinamento consultato o modificato.
- Download HTTPS senza autenticazione. Hash locale, checksum pubblico e digest GitHub coincidenti. Ogni voce decompressa; tutti i 292 file estratti hanno lo stesso SHA-256 dei corrispondenti file di staging. Percorsi univoci, niente traversal o link; manifest/versione/commit/runtime corretti. Nessun allegato, foto/video, catalogo personale, credenziale, SDK o PDB esterno nel pacchetto. Nessun percorso privato del progetto nelle DLL Porto.
- Versione pubblica `0.1.0-rc.20+00698da4cbeb138d7bdeec923254ac9e421f7748`. Undici smoke pubblici passati: COM senza dispositivi, protocollo worker senza dispositivi, nove stati UI sintetici. Runtime incluso verificato con DOTNET_ROOT e DOTNET_ROOT_X64 inesistenti e DOTNET_MULTILEVEL_LOOKUP=0. Schermata iPhone compatta verificata; immagine pubblica identica per hash allo staging. Nessuna ricerca automatica di telefoni.
- Build senza avvisi/errori, controllo vulnerabilità NuGet mantenuto e dipendenze invariate. Suite finale 555/555; catalogo SQLite/WAL grande 1/1; flusso letto e sottoposto a hash oltre 4 GiB 1/1; packaging 4/4. Due esecuzioni del benchmark AFC, tre ripetizioni per variante, dodici guasti negativi per esecuzione passati. Risultati e limiti in REVIEW-RC20.md e TESTS.md.
- La ripetizione della scrittura NTFS oltre 4 GiB **non è stata completata**: fermata per lentezza locale, senza dichiararla superata. Il motore Core rimane quello della rc.19, il cui precedente test completo resta documentato. Il nuovo limite AFC e l'obiettivo 29 GB/ora richiedono ancora misure reali sul telefono e sul PC della cliente.

Resoconto successivo al tag: ZIP e tag invariati. Non è una certificazione e non garantisce 29 GB in un'ora, supporto universale o assenza di blocchi. Nessuna spesa, servizio/driver installato o protezione ridotta. Revisione delegata non disponibile per quota, senza fallback a pagamento; revisione locale e regressioni non presentate come revisione indipendente. Il prototipo Core con guadagno di tempo non dimostrato è stato escluso. App non firmata commercialmente, candidata per collaudo hardware.

## 0.1.0-rc.21 — 17 novembre 2026

- Tag v0.1.0-rc.21, commit `8e85b91114629e9be7b24b740802849d9492da11`, branch `codex/catalog-size-rc21`. Pubblicata alle 2026-11-17T22:09:06Z, verifica pubblica conclusa alle 22:09:40Z.
- [Release](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.21), [ZIP verificato](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.21/Porto-0.1.0-rc.21-win-x64.zip): 54.384.871 byte, 292 file, 128.811.845 byte decompressi. Runtime .NET 10.0.14 incluso.
- SHA-256 ZIP `4afce8a922e141cbe706f5c490bd14083118e218a8f8575d79742bf6b556bd9e`. Checksum pubblico SHA256SUMS.txt: 97 byte, digest `92d599cbfef96bec32d56ff5811f57bfbf77c971415a231670a893878fb1b9ab`.
- Nuovi staging, archivio, download e screenshot. Venti ZIP precedenti ricontrollati e invariati. Nessun telefono, pairing, catalogo personale o file delle prove consultato o modificato. Il grande SQLite generato dai test è interamente sintetico ed è conservato nelle nuove directory di prova, escluso dalla distribuzione.
- Download HTTPS senza autenticazione: hash locale, checksum pubblico e digest GitHub uguali. Ogni voce decompressa; tutti i 292 file estratti identici per SHA-256 allo staging. Percorsi univoci, niente traversal o link, manifest/versione/commit/runtime corretti. Nessun allegato, foto/video, database personale o sintetico, credenziale, SDK o PDB esterno nel pacchetto; nessun percorso privato del progetto nelle DLL Porto.
- Eseguibile pubblico `0.1.0-rc.21+8e85b91114629e9be7b24b740802849d9492da11`. **12 smoke pubblici passati**: COM e protocollo worker senza dispositivi, dieci stati UI sintetici. Runtime incluso verificato con DOTNET_ROOT e DOTNET_ROOT_X64 inesistenti e DOTNET_MULTILEVEL_LOOKUP=0. Nessuna ricerca di telefoni. Diagnostica compatta del limite controllata visivamente; immagini pubbliche iPhone e diagnostica identiche per hash allo staging.
- Build senza avvisi/errori e controllo vulnerabilità NuGet mantenuto. Suite finale **559/559**, quattro regressioni mirate già incluse; riproduzione iniziale 0/2 con fallimento atteso. Test esteso su catalogo valido da 1.426.288.640 byte con WAL da 7.337.752 byte passato, ripetuto con due catture consecutive complete. Packaging 4/4. Dati e limiti in TESTS.md e REVIEW-RC21.md.

Resoconto successivo al tag: ZIP e tag immutati. La candidata corregge il limite da 1 GiB osservato, con budget di memoria più prudente e soglia di 2047 MiB; richiede circa 3,4 GB liberi per le dimensioni segnalate. La disponibilità della RAM è misurata, non prenotata. Restano da confermare schema iOS, catalogo stabile, album, originali locali e importazione reale. Nessuna promessa di 29 GB/ora, assenza di errori o compatibilità universale. Autenticazione, verifiche e motore di copia invariati. Nessun costo, servizio installato o protezione ridotta. Revisori delegati ancora indisponibili per quota, nessun fallback a pagamento. App non firmata commercialmente e non certificata.

## 0.1.0-rc.22 — 18 novembre 2026

- Tag immutabile v0.1.0-rc.22, commit `2b6348920b2c607c400dfb2c6091b236e360cb31`, branch `codex/catalog-memory-rc22`. Pubblicata 2026-11-17T23:10:49Z (18 novembre locale), verifica pubblica conclusa alle 23:11:06Z.
- [Release](https://github.com/9gabri/porto-iphone-usb/releases/tag/v0.1.0-rc.22), [ZIP](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.22/Porto-0.1.0-rc.22-win-x64.zip): 54.386.363 byte, 292 file, 128.815.923 byte decompressi. Runtime .NET 10.0.14 incluso.
- SHA-256 `05018a4931f3b08bc21b2666a10938a501d3dfd0d5733cc3a804fdf960d888d9`. Checksum pubblico SHA256SUMS.txt: 97 byte, digest `28dfd3836bb5adbd6a05fbc3011ce2d38443c6b3cc54643a53539260174e3733`.
- Nuovi staging, archivio, download e screenshot; 21 ZIP precedenti invariati. Download pubblico HTTPS senza autenticazione. Hash locale, checksum e digest GitHub coincidenti. Ogni voce decompressa e controllata: percorsi univoci, niente traversal/link, manifest/versione/commit/runtime corretti. Tutti i 292 file estratti identici allo staging per SHA-256.
- Contenuto limitato a runtime, eseguibili del progetto, dipendenze, licenze e documentazione. Nessun allegato, media, database, pairing, credenziale, SDK o PDB esterno. Nessun percorso privato nelle DLL Porto. Nessun file delle prove o telefono consultato/modificato.
- Eseguibile `0.1.0-rc.22+2b6348920b2c607c400dfb2c6091b236e360cb31`. **12 smoke pubblici passati**: due interop/protocollo senza dispositivi e dieci schermate sintetiche. Runtime incluso con DOTNET_ROOT/ROOT_X64 inesistenti e MULTILEVEL_LOOKUP=0. Interfaccia compatta iPhone vista; screenshot pubblici iPhone e diagnostica identici allo staging.
- Build senza avvisi/errori, controllo vulnerabilità NuGet mantenuto, dipendenze invariate. Suite finale **565/565**, catalogo grande con due catture consecutive **1/1**, packaging **4/4**. Primo avvio generale con dotnet DLL interrotto per invocazione dei figli errata, poi suite completa con apphost; nessun caso saltato. Dettagli in TESTS.md e REVIEW-RC22.md.

Resoconto successivo al tag: pacchetto e tag invariati. La condivisione dell'immagine riduce il requisito iniziale della schermata da 3,37 a 1,95 GB; il WAL ridimensionato può richiedere più RAM e la disponibilità è misurata, non prenotata. Restano da confermare memoria, schema iOS, originali, album e importazione reali. Nessuna promessa di 29 GB/ora o compatibilità universale. Nessuna spesa, servizio/driver o riduzione delle protezioni. Revisione locale; revisori delegati indisponibili per quota, senza fallback a pagamento. Candidata non firmata commercialmente e non certificata.
