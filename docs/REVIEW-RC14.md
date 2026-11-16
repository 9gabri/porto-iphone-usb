# Analisi dell'autenticazione iPhone — rc.14

## Evidenza reale e limite della diagnosi

La prova guidata della rc.13 su iPhone 16 con iOS 26.6.1 ha mostrato due ostacoli distinti. Inizialmente il servizio USB Apple non era disponibile. Dopo l'installazione ufficiale di Dispositivi Apple, autorizzata esplicitamente dall'utente, l'app Apple riconosceva il telefono e la ricerca di Porto riusciva. Il comando Scegli album falliva invece nell'autenticazione TLS, prima della lettura del catalogo Foto. Un riavvio delle applicazioni non ha risolto il blocco.

La fotografia mostrava soltanto il messaggio generico di AppleTls.Protect. Quel messaggio aggregava condizioni diverse: non permette di identificare l'alert, la fase o la politica che ha causato il fallimento. Non sono stati acquisiti certificati, chiavi, record di pairing o dati personali del telefono. La causa precisa del caso reale resta quindi non dimostrata.

Le prove sul precedente telefono Android usavano WPD e non dimostrano la compatibilità del percorso Apple. Durante questo ciclo non sono stati cercati o letti telefoni reali e non sono stati modificati file delle prove personali.

## Difetti riprodotti e modifiche circoscritte

AppleTls.Client annunciava soltanto RSA/SHA-256 e richiedeva la stessa firma per l'autenticazione del client. Un interlocutore che usa RSA/SHA-384 o RSA-PSS/SHA-256 può rispettare il livello di protezione richiesto ma non essere accettato. La nuova matrice sintetica, eseguita contro il binario rc.13, ha riprodotto quattro fallimenti su nove casi: firma server SHA-384, richiesta client SHA-384, firma server RSA-PSS e richiesta client RSA-PSS. Gli altri cinque casi passavano.

La negoziazione ora seleziona una firma comune da un elenco esplicito: RSA PKCS#1 con SHA-256/384/512 e RSA-PSS RSAE con SHA-256/384/512. L'elenco vale per le firme dell'handshake e la selezione delle credenziali client. Non sono stati aggiunti SHA-1 per l'handshake, TLS obsoleti o suite CBC. TLS 1.2, ECDHE_RSA/AES-GCM, pin DER esatto del certificato e autenticazione reciproca restano richiesti.

Questi difetti sono riproducibili, ma non costituiscono prova che l'iPhone del caso reale abbia proposto proprio quelle firme. La nuova candidata deve ancora essere provata su quel dispositivo.

## Ipotesi escluse o non dimostrate

- **Catena con foglia firmata SHA-1 e radice:** il nuovo caso sintetico passa anche sul binario rc.13. Non è una correzione di questa release. Nessun override per disattivare il controllo degli algoritmi dei certificati è stato introdotto.
- **RootCertificate invece di HostCertificate:** non è un errore dimostrato. Implementazioni primarie usano scelte differenti: il percorso OpenSSL di libimobiledevice utilizza le credenziali root, mentre altri percorsi impiegano quelle host. Nessun cambio speculativo delle credenziali di pairing.
- **Estensione di rinegoziazione sicura assente:** possibile incompatibilità, non verificata sul telefono reale. Porto mantiene il rifiuto; la nuova diagnostica permette di riconoscere questo caso senza consentirlo silenziosamente.

## Diagnostica senza dati personali

Il messaggio contiene una categoria fissata dal programma e, nei casi generici, un numero di alert TLS. Non include testo ricevuto dal peer, dettagli dell'eccezione interna, subject, seriali, certificati, chiavi, percorsi di file o credenziali.

| Codice | Significato |
| --- | --- |
| APPLE-TLS-PIN | Il certificato ricevuto non coincide con quello abbinato |
| APPLE-TLS-CLIENT-AUTH | Autenticazione reciproca non richiesta dal server |
| APPLE-TLS-CLIENT-TYPE | Tipo di certificato client non supportato |
| APPLE-TLS-CLIENT-SIGNATURE | Nessuna firma client forte comune |
| APPLE-TLS-RENEGOTIATION | Estensione di sicurezza TLS 1.2 richiesta ma assente |
| APPLE-TLS-LOCAL-numero | Alert generato dal client Porto |
| APPLE-TLS-PEER-numero | Alert ricevuto dall'interlocutore |

Il messaggio aiuta a diagnosticare; non certifica da solo una causa nel telefono, nel driver o nel sistema operativo. Lo stesso collegamento può superare questa fase e incontrare successivamente limiti del catalogo Foto.

## Verifiche e collaudo residuo

Le regressioni usano certificati, database e stream sintetici in cartelle isolate. Nessuna nuova dipendenza, nessuna API a consumo, nessuna installazione automatica o cambiamento delle protezioni del sistema. Risultati finali della suite, build e confezionamento sono registrati in [TESTS.md](TESTS.md) e [test-results.txt](test-results.txt); integrità e avvio della distribuzione pubblica in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).

La prova necessaria è: servizio Apple e pairing disponibili, ricerca del dispositivo, lettura dell'elenco album, piccola selezione di originali locali, confronto delle copie ripetute e ripresa. Se Scegli album fallisce ancora, conservare il nuovo codice diagnostico. La rc.14 è una candidata per questo collaudo, non una soluzione hardware già dimostrata. Le limitazioni su schema privato iOS, iCloud, album intelligenti/condivisi e formati restano valide.

## Fonti primarie consultate

- [BouncyCastle AbstractTlsPeer](https://github.com/bcgit/bc-csharp/blob/master/crypto/src/tls/AbstractTlsPeer.cs): verifiche di rinegoziazione e notifiche TLS.
- [BouncyCastle BcDefaultTlsCredentialedSigner](https://github.com/bcgit/bc-csharp/blob/master/crypto/src/tls/crypto/impl/bc/BcDefaultTlsCredentialedSigner.cs): selezione del firmatario RSA/PSS.
- [BouncyCastle SignatureAndHashAlgorithm](https://github.com/bcgit/bc-csharp/blob/master/crypto/src/tls/SignatureAndHashAlgorithm.cs): identificatori degli algoritmi supportati.
- [libimobiledevice idevice.c](https://github.com/libimobiledevice/libimobiledevice/blob/master/src/idevice.c): uso delle credenziali di pairing nei diversi backend.
- [pymobiledevice3 service_connection.py](https://github.com/doronz88/pymobiledevice3/blob/master/pymobiledevice3/service_connection.py): configurazione TLS di un altro client, usata come confronto e non copiata per abbassare la politica di Porto.
- [RFC 8446](https://www.rfc-editor.org/rfc/rfc8446.html): schemi di firma e aggiornamenti applicabili a TLS 1.2. Porto non abilita TLS 1.3 in questa modifica.
