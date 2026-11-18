# Porto Album per iPhone e Windows

Prototipo separato dalla versione USB di Porto. Su iPhone scegli un album personale e cosa copiare. Il PC riceve via Wi-Fi gli originali disponibili sul telefono e li verifica uno per volta.

## Prima installazione di prova

Serve un iPhone con iOS 17 o successivo e un PC Windows x64. Il pacchetto `PortoAlbum-per-firma.ipa` contiene l'app per un iPhone fisico. Per installarlo occorrono una firma associata al tuo Apple Account e un profilo valido per il tuo dispositivo.

Una possibilità è **AltServer per Windows**, con **Sideload .ipa** dal menu dell'icona tenendo premuto Maiusc. AltServer richiede i componenti Apple indicati nella sua guida. Controlla i prerequisiti prima di cambiare un'installazione esistente. Ti viene chiesto di usare il tuo Apple Account con questo programma di terze parti. Sul telefono occorre autorizzare il computer e abilitare la modalità sviluppatore, che secondo Apple riduce la sicurezza del dispositivo. Se compare una richiesta di revoca di certificati, fermati: potrebbe influire su altre app di sviluppo. La firma personale gratuita scade dopo sette giorni e richiede rinnovo. La build di Porto e la firma personale sono passaggi separati. [Installazione Windows](https://faq.altstore.io/altstore-classic/how-to-install-altstore-windows) · [Sideload del pacchetto](https://faq.altstore.io/release-notes/altserver) · [Modalità sviluppatore](https://developer.apple.com/documentation/xcode/enabling-developer-mode-on-a-device) · [Limiti della firma gratuita](https://developer.apple.com/help/account/basics/about-your-developer-account/)

## Prima copia

1. Collega iPhone e PC alla stessa rete privata. Su Windows apri **Porto-Album-PC.exe**, premi **Scegli cartella** e poi **Avvia ricezione**.
2. Con **Fotocamera** dell’iPhone inquadra il QR del PC, apri **Porto Album** e conferma **Collega** dopo aver confrontato l'indirizzo e l'impronta mostrati.
3. Tocca **Scegli album**. Accorda a Porto l’accesso alle foto che desideri copiare. Seleziona il tuo album personale e **Solo foto**, **Solo video** oppure **Foto e video**.
4. Tocca **Copia album sul PC**. Sul PC controlla nome dell’album e numero di elementi, poi premi **Accetta**. Tieni Porto aperto sull’iPhone.
5. Controlla il conteggio finale. Sul PC premi **Apri copie**. In caso d’interruzione scegli di nuovo lo stesso album, filtro e cartella. Porto rilegge ciascun originale per confrontarlo con la copia sul PC e invia i contenuti mancanti.

La prima prova consigliata è un album piccolo con una foto e un video disponibili sull'iPhone. Un originale conservato solo su iCloud può risultare illeggibile in questa modalità. Per ogni elemento occorre spazio temporaneo sul telefono sufficiente per l’originale. Le Live Photo in modalità foto forniscono l'immagine. Il filtro video considera gli elementi che Foto classifica come video. La selezione riguarda gli elementi visibili con i permessi concessi. Per misurare l'attesa reale, annotare il tempo dal tocco su **Copia album sul PC** alla prima copia verificata e al conteggio finale.

## Codice e collaudi

Il workflow del ramo `codex/porto-ios-album-prova-1` compila su un runner macOS standard del repository pubblico. `Build/build-iphone.sh` produce il pacchetto dispositivo con Xcode; `Build/verify-ipa.py` ne controlla struttura e piattaforma. Il test in `Build/simulator-smoke.sh` usa una libreria sintetica del simulatore e un ricevitore di test sullo stesso computer. Su Windows il progetto è `Windows/PortoAlbum.csproj`, con test HTTPS su loopback avviabili con `Porto-Album-PC.exe --self-test CARTELLA_DI_COLLAUDO`.
