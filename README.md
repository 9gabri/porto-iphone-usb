# Porto

App locale italiana per copiare e verificare video su Windows 11 x64, senza conversioni. **0.1.0-rc.9**, candidata con importazione da cartella e confronto automatico delle copie precedenti. La prova dell’esportazione reale dall’iPhone resta necessaria: i test sintetici non certificano un totale di video Foto.

[Scarica Windows](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.9/Porto-0.1.0-rc.9-win-x64.zip) · [Checksum](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.9/SHA256SUMS.txt) · [Istruzioni](https://9gabri.github.io/porto-iphone-usb/)

## Importazione da cartella

1. Esporta gli **originali non modificati selezionati nella raccolta Video di Foto** in una cartella separata sul PC. Non usare l’intera cartella DCIM: può contenere componenti di Live Photo. [Preparazione e limiti](docs/FOLDER-IMPORT.md).
2. Chiudi la vecchia app, estrai tutto il nuovo ZIP separatamente e apri `Porto.exe`. Il runtime è incluso; non servono Mac, compilatori o installazione .NET.
3. Lascia **Importa da → Cartella esportata da Foto**. Come **Origine** scegli l’esportazione; come **Archivio** scegli la stessa cartella delle prove precedenti, quella che contiene `Video Porto`, `.porto` e `Report Porto`.
4. Premi **Avvia importazione**. Porto legge i contenuti, controlla le copie precedenti e riutilizza quelle identiche. Copia i contenuti mancanti con nomi nuovi e verifica SHA-256 dopo la rilettura del file salvato. **Pausa** e **Riprendi** conservano il lavoro e ripetono i confronti.
5. Apri il report: trovi ogni video selezionato e il relativo percorso, nuove copie, copie riutilizzate, errori, esclusioni e copie precedenti non riutilizzate.

Origine e archivio devono essere cartelle locali NTFS separate: nessuna può contenere l’altra. La destinazione ha un limite di 120 caratteri. Nessuna lettura USB viene avviata automaticamente in questa modalità.

## Che cosa succede alle copie precedenti

- Il confronto usa byte e SHA-256, non soltanto nome, data o dimensione. Rilegge i file.
- Le copie integre rimangono nella cartella attuale, anche se si trovano in `Risorse video aggiuntive`.
- Se manca il vecchio catalogo, vengono cercati anche MOV, MP4 e M4V non registrati nell’archivio. Questi file vengono letti, mai registrati come percorsi modificabili o sostituiti.
- Due file distinti selezionati con byte uguali richiedono due copie: una copia precedente non può soddisfarli entrambi.
- I file precedenti non riutilizzati restano invariati. **Non significa che siano Live Photo**: possono essere copie ulteriori, versioni diverse, file non più presenti sul telefono o letture ancora pendenti.
- Copie danneggiate rimangono al loro posto; un recupero viene pubblicato con un nuovo nome. Il problema precedente resta nel report.
- Versioni con byte diversi restano distinte: Porto non può provare automaticamente che un video convertito e il suo originale siano lo stesso elemento Foto.

I cataloghi e i report vengono aggiornati per registrare il lavoro. Non vengono cancellati video o temporanei esistenti. I temporanei associabili possono essere completati dopo il confronto integrale del prefisso; se divergono vengono conservati. I report nuovi hanno nomi univoci.

## Copertura e limiti

**Cartella verificata** significa che tutti i candidati supportati individuati hanno una copia verificata e che l’inventario finale è stabile. Non prova che tutti i video dell’iPhone siano stati esportati, né che l’esportazione non contenga Live Photo. La selezione va fatta nella raccolta Video di Foto; i contenuti solo iCloud devono prima risultare disponibili all’esportazione.

MOV, MP4 e M4V sono controllati strutturalmente; non viene decodificato ogni fotogramma. I contenitori video comuni non supportati, come AVI e MKV, restano segnalati come ambigui. Foto, AAE e altre estensioni escluse hanno una motivazione nel report. Un elenco cambiato, letture discordanti, errori e temporanei ambigui non producono una selezione verificata. I vecchi debiti USB restano distinti e visibili.

La modalità **iPhone tramite USB** resta disponibile, con ricerca esplicita del dispositivo. Legge tutte le risorse video esposte via WPD, incluse possibili componenti Live Photo; gli indizi vengono separati in `Risorse video aggiuntive`. Non equivale a selezionare la raccolta Video di Foto. Recupero con worker isolati, inventari verificati e tentativi limitati; nessun retry infinito. [Architettura](docs/ARCHITECTURE.md).

Windows ARM, exFAT, rete, junction, link e placeholder cloud non disponibili localmente non sono supportati. La nuova candidata deve essere provata sullo specifico computer e sull’esportazione reale. Non è garantito un numero prestabilito di video.

## Privacy e distribuzione

Nessuna funzione di modifica o cancellazione sull’iPhone, conversione, upload, telemetria o pagamento. Porto non configura condivisioni di rete, non chiede Apple ID, non installa driver e non richiede privilegi amministrativi. Non modifica le protezioni di Windows. L’eseguibile non è firmato con un certificato commerciale: Windows può mostrare avvisi o impedirne l’avvio secondo le proprie policy.

Conserva le vecchie versioni e l’intero archivio. Dopo un aggiornamento usa la nuova app per quell’archivio. La cartella archivio viene ricordata localmente in `%LOCALAPPDATA%\Porto\destination.txt`. La riproduzione HEVC dipende dai codec del PC; la copia non richiede acquisti.

## Verifiche e sviluppo

[Test eseguiti](docs/TESTS.md) · [Revisione rc.9](docs/REVIEW-RC9.md) · [Fonti](docs/SOURCES.md) · [Verifica della pubblicazione](docs/RELEASE-VERIFICATION.md).

SDK .NET 10, runtime incluso 10.0.14. `scripts/dev.ps1 test -Large`, `scripts/dev.ps1 build`, `scripts/dev.ps1 publish`. Test sintetici in cartelle isolate. Per verificare l’interfaccia senza cercare telefoni: `--ui-smoke <percorso.png>`. Non avviare ricerche USB senza autorizzazione al dispositivo.

Licenza MIT. Progetto indipendente, non affiliato ad Apple o Microsoft.
