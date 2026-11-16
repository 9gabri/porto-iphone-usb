# Verifiche della candidata 0.1.0-rc.14

Windows 11 x64, SDK .NET 10.0.403 e runtime 10.0.14. Nessun telefono o archivio personale usato nei test. Il caso reale resta da ricontrollare: la foto del messaggio rc.13 non identifica la causa precisa.

## Prima e dopo

Matrice iniziale eseguita contro Porto.Apple.dll della rc.13 pubblicata: 5/9 passati, quattro fallimenti su firme server/client SHA-384 e RSA-PSS/SHA-256. Il certificato leaf SHA-1 con root e handshake SHA-256 passava gia: nessuna correzione speculativa della catena.

Dopo: 15/15 nella matrice finale. Tutte le sei firme forti ammesse, traffico frammentato bidirezionale; rifiuto di pin errato e certificato diverso con stessa chiave, SHA-1 nel handshake server/client, autenticazione reciproca assente. Alert ricevuto e motivi locali con categorie fisse, senza segreti o eccezioni interne.

TLS 1.2, ECDHE_RSA/AES-GCM, pin DER, autenticazione client e requisito di rinegoziazione sicura mantenuti. Revisione indipendente senza difetti bloccanti nel diff. Nessuna modifica a copia USB generica, ledger, cataloghi o filesystem.

## Verifica integrata

Suite ordinaria finale: 490/490 passati, incluse le 15 nuove regressioni TLS. Log in test-results.txt. Build test e app senza avvisi/errori; UI compilata con dati sintetici. Le quattro prove estese rc.13 (50.000 elementi, 2.000 copie ripetute, stream/file oltre 4 GiB) non sono ripetute in questo ciclo TLS e restano documentate nel tag precedente. Nessun test esistente rimosso o indebolito.

Il confezionamento esegue interop/worker e nove stati UI senza enumerazione telefoni. Il download pubblico viene verificato e registrato dopo il tag in RELEASE-VERIFICATION.md senza cambiare lo ZIP.

## Riproduzione e limiti

`scripts/dev.ps1 test` esegue la suite ordinaria; `--filter=apple TLS matrix` filtra i 15 nuovi casi. Client e server sintetici usano BouncyCastle: non costituiscono prova di interoperabilita con iPhone.

Restano da verificare autenticazione reale, catalogo, album, originali locali, copia e ripresa. In caso di errore conservare il codice APPLE-TLS. Non si dichiara risolto il caso reale. Dettagli e fonti in REVIEW-RC14.md.
