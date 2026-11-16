# Verifiche della candidata 0.1.0-rc.15

Windows 11 x64, SDK .NET 10.0.403 e runtime 10.0.14. Solo dati sintetici in RAM e cartelle isolate del progetto; nessun telefono, record di pairing o archivio personale consultato.

## Riproduzione e correzione

Quattro casi positivi con certificati modellati sulle fonti primarie Apple pairing falliscono contro Porto.Apple.dll della rc.14 pubblicata: 0/4, tutti APPLE-TLS-LOCAL-42. Coprono issuer/subject vuoti, seriale zero/uno, certificato SHA-1 con handshake SHA-256 e parametri RSA opzionali assenti. Il server sintetico costruisce i certificati indipendentemente dal nuovo adattatore.

Dopo la correzione: 13/13 nuovi casi superati. Handshake completo, identità client verificata dal server e dati scambiati nei due sensi. Sono respinti certificati estranei, certificati diversi con la stessa chiave, uso della chiave non consentito, firme handshake SHA-1, autenticazione reciproca assente, DER malformato ed estensioni duplicate. Byte originali, metadati e copie difensive verificati; il parser BouncyCastle globale rimane restrittivo.

Revisione indipendente della correzione e dei test senza difetti bloccanti. Nessun test esistente rimosso o indebolito, nessuna dipendenza modificata.

## Suite finale

503/503 test ordinari superati in una singola esecuzione finale tramite Porto.Tests.exe; log in test-results.txt. Build test e app senza avvisi/errori. Interfaccia rc.15 compilata e controllata visivamente con dati sintetici.

Due avvii preliminari della suite sono stati interrotti, non conteggiati come suite riuscite: il primo usava dotnet con DLL invece dell'apphost necessario ai test dei sottoprocessi; il secondo era limitato dal sandbox, che impediva la creazione degli hard link sintetici. La suite finale ha usato l'apphost e i permessi necessari nelle sole cartelle di prova, senza modificare il codice per aggirare i controlli. Nessun errore o avviso nella compilazione finale.

Le quattro prove estese già eseguite nella rc.13 non sono ripetute per questa modifica al trasporto. Il confezionamento verifica interop, worker e nove stati UI senza enumerare telefoni. Il riscaricamento pubblico viene registrato dopo il tag in RELEASE-VERIFICATION.md senza sostituire lo ZIP.

## Limiti

La fotografia del telefono è coerente con il difetto riprodotto, ma il certificato reale non è stato acquisito: la causa sul dispositivo resta un'inferenza. I test sintetici non provano l'interoperabilità hardware. Restano da confermare autenticazione, catalogo, album, originali locali, copie e ripresa. Nessuna certificazione o promessa di compatibilità universale. Dettagli e fonti in REVIEW-RC15.md.
