# Porto Album per Windows x64

Richiede Porto Album installata sull’iPhone. Collega i dispositivi alla stessa rete privata.

1. Apri **Porto-Album-PC.exe** dalla cartella estratta. Scegli una cartella locale di prova su disco NTFS, poi premi **Avvia ricezione**.
2. Sull’iPhone inquadra il QR con **Fotocamera** e apri Porto. Se richiesto, autorizza l’accesso alla rete locale e alle foto che intendi copiare.
3. Nell’app scegli un album e il tipo di elementi. Avvia l’invio e sul PC controlla il nome dell’album e il numero di elementi prima di premere **Accetta**.
4. Tieni aperta Porto su entrambi i dispositivi. Dal PC premi **Apri copie** per controllare i risultati e **Ferma ricezione** alla fine.

Il ricevitore ascolta sulla stessa sottorete privata IPv4 sulla porta 53317 con TLS e un certificato dedicato al singolo avvio. La fotocamera trasmette a Porto l’indirizzo del PC e l’impronta del certificato dal QR. Se Windows richiede nuove autorizzazioni di rete, valuta la richiesta con il proprietario del PC.

La cartella scelta contiene una sottocartella per dispositivo, album e filtro. Ogni invio autorizzato produce un rapporto locale JSONL con nomi e impronte. I file arrivati sono riletti e verificati sul disco prima della conferma all’iPhone. Per riprendere, riapri lo stesso album con lo stesso filtro e scegli la stessa cartella del PC. Se il titolo dell’album o l’installazione di Porto sul telefono cambiano, la sottocartella può cambiare. Un file interrotto durante la copia viene richiesto dall’inizio.

## Compilazione e collaudo

Con .NET 10 su Windows x64, dalla cartella del progetto esegui `dotnet publish PortoAlbum.csproj -c Release -r win-x64 --self-contained true`. La dipendenza QRCoder 1.7.0 (MIT) è distribuita tramite NuGet, con i crediti in LICENSE-QRCoder.txt. Il ricevitore usa le librerie di .NET per HTTPS e Windows Forms.

L’eseguibile supporta `--self-test CARTELLA_TEMPORANEA`. Il test crea una sottocartella univoca, si collega esclusivamente a 127.0.0.1 e usa 800 file sintetici. Verifica il riavvio dopo 323 file, i 477 mancanti, gli omonimi, l’interruzione, il consenso e la conservazione dei conflitti. Scrive l’esito in esito.txt. Una prova con hardware reale resta necessaria per valutare il collegamento iPhone e le prestazioni Wi-Fi.
