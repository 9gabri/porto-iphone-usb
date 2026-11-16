# Certificati di abbinamento iPhone — rc.15

## Evidenza e diagnosi

La prova reale con rc.14 è fallita su **Scegli album**, mostrando `APPLE-TLS-LOCAL-42`. Il catalogo Foto non era ancora disponibile. La rc.14 non ha quindi risolto quel blocco: le incompatibilità delle firme corrette nel ciclo precedente non erano una spiegazione dimostrata del caso reale.

L'analisi del codice 42 ha individuato un difetto riproducibile: BouncyCastle 2.7.0 rifiuta durante il parsing X.509 un issuer DN vuoto. Il rifiuto diventa `bad_certificate(42)` prima del callback di Porto che confronta il certificato con quello abbinato. Le implementazioni libimobiledevice e pymobiledevice3 documentano o generano certificati di pairing con nomi vuoti. I test precedenti usavano nomi sintetici non vuoti e non esercitavano questo caso.

La riproduzione iniziale contro il binario rc.14 pubblicato ha fatto fallire quattro handshakes sintetici validi per il contratto di pairing: nomi vuoti, seriale zero o uno, certificato firmato SHA-1 con handshake forte, parametri SPKI opzionali assenti. L'errore osservato era lo stesso `APPLE-TLS-LOCAL-42`.

Questa è una causa concreta compatibile con la fotografia, **non la lettura del certificato del telefono reale**. Non sono stati consultati telefoni, record di pairing, chiavi o file delle prove personali. La corrispondenza con il caso della cliente resta un'inferenza da confermare con il nuovo eseguibile.

## Correzione e confine di fiducia

`AppleTls.Client` usa ora `ApplePairingCrypto`, limitato alla singola connessione. Il parser BouncyCastle rimane il primo percorso. L'adattatore interviene sul rifiuto X.509 soltanto quando i byte coincidono esattamente con il certificato del dispositivo già abbinato o con quello del computer usato per l'autenticazione. Il confronto usa `FixedTimeEquals`; un certificato con la stessa chiave ma byte diversi non acquisisce questa autorizzazione.

Il parser .NET legge il certificato autorizzato con issuer vuoto. L'adattatore conserva e restituisce il DER originale, insieme a chiave pubblica, numero di serie, algoritmo e parametri di firma, estensioni e vincoli KeyUsage. Non modifica certificati trasmessi o record di pairing. La verifica del pin del dispositivo rimane necessaria anche dopo il parsing.

Non viene abilitata un'opzione globale, sostituita la libreria con una versione precedente o introdotta una nuova dipendenza. Restano TLS 1.2, ECDHE_RSA/AES-GCM, firme handshake forti, autenticazione reciproca e controlli di rinegoziazione. Il supporto al formato del certificato autorizzato non autorizza certificati sconosciuti né handshake SHA-1.

## Verifica e limiti

Le regressioni impiegano certificati e stream sintetici, includendo casi accettati e rifiuti avversari. Risultati finali, build e sequenza delle verifiche sono registrati in [TESTS.md](TESTS.md) e [test-results.txt](test-results.txt); i controlli del pacchetto pubblico sono registrati dopo la pubblicazione in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).

Prima di una grande importazione occorre confermare sul telefono: ricerca del dispositivo, apertura dell'elenco album, copia di un piccolo album con originali locali, apertura delle copie e riconoscimento delle copie già verificate. Se la lettura degli album fallisce ancora, conservare il messaggio completo. Il superamento dell'autenticazione non dimostra da solo compatibilità dello schema Foto, disponibilità degli originali cloud, completezza degli album o ripresa reale.

Questa candidata non è certificata, dichiarata priva di bug o universalmente compatibile. Nessun costo, installazione di driver/servizi o riduzione delle protezioni di Windows è stato necessario per la modifica.

## Fonti primarie

- [BouncyCastle 2.7.0 TbsCertificateStructure](https://raw.githubusercontent.com/bcgit/bc-csharp/release-2.7.0/crypto/src/asn1/x509/TBSCertificateStructure.cs): rifiuto dell'issuer DN vuoto nel parser generale.
- [BouncyCastle 2.7.0 BcTlsCertificate](https://raw.githubusercontent.com/bcgit/bc-csharp/release-2.7.0/crypto/src/tls/crypto/impl/bc/BcTlsCertificate.cs): conversione degli errori di parsing in alert 42.
- [libimobiledevice userpref.c](https://raw.githubusercontent.com/libimobiledevice/libimobiledevice/master/common/userpref.c): generazione dei certificati di pairing; fonte consultata, non copiata nella modifica.
- [pymobiledevice3 ca.py](https://raw.githubusercontent.com/doronz88/pymobiledevice3/master/pymobiledevice3/ca.py): costruzione esplicita di certificati con issuer e subject vuoti.
