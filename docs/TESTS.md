# Prove della candidata 0.1.0-rc.9

Windows 11 x64, SDK .NET 10.0.403, runtime 10.0.14. Fixture sintetiche in cartelle NTFS isolate nel progetto. Nessun telefono, archivio personale o video della cliente letto o modificato.

## Risultati effettivi

**348 casi distinti coperti.** Suite completa con **347/347** casi superati (343 ordinari e quattro estesi), poi **28/28** ricontrolli delle aree cartella sul codice finale dopo l’aggiunta del caso per i contenitori video non supportati. Quei 28 comprendono 27 casi già eseguiti e un caso nuovo: non sono 375 casi distinti. Il successivo chiarimento testuale del report per copie non registrate non modifica la logica di confronto. Non viene dichiarato un unico run completo di 348 casi dopo l’ultimo aggiustamento.

Build applicativa finale senza avvisi/errori. Cinque smoke dell’interfaccia: origine/cartella, recupero USB, pausa, incompleto USB, risultato di confronto cartella in finestra compatta. Screenshot esaminati; nessuna discovery hardware negli smoke. Quattro prove separate del confezionamento: compressione interrotta, ZIP invalido, archivio già esistente conservato, successo con checksum.

[Output](test-results.txt) · [Revisione indipendente](REVIEW-RC9.md).

## Nuova modalità: 26 casi

14 casi del source adapter: ricorsione, formati e file accessori, byte invariati, scritture negate durante la lettura, ID stabili, date/dimensioni/identità NTFS, input forgiati, sovrapposizione radici, annullamento, root scomparsa, file non leggibili, hardlink/junction, percorsi remoti/dispositivo, dispose.

12 casi di integrazione cartella: confronto con copie non registrate; mantenimento del debito storico; copia nella cartella aggiuntiva riutilizzata senza spostamento; molteplicità degli identici; vecchi errori USB; riparazione che conserva il danneggiato e i suoi dettagli nei report successivi; contenitore malformato e prosecuzione; inventario finale cambiato; doppia lettura discordante; sorgente cartella reale ripetuta senza duplicati; contenitore AVI non supportato lasciato ambiguo.

I test iniziali prima dell’implementazione engine mostravano quattro fallimenti attesi su cinque. I test nativi di hardlink/junction inizialmente non potevano creare le fixture nel sandbox; sono stati rieseguiti con permessi adatti esclusivamente nei percorsi sintetici e sono passati. Nessuna protezione Windows è stata ridotta.

## Regressioni mantenute

SHA-256, EOF e troncamenti, letture discordanti, dimensioni native, metadati QuickTime, classificazione USB, priorità e recupero limitato, timeout e cancellazione, morti reali del processo nei passaggi di pubblicazione, cataloghi/journal interrotti o estranei, percorsi ostili e collegamenti, collisioni, disco pieno, permessi, inventari incompleti, cambi di ID e molteplicità storica.

Prove estese: stream oltre 4 GiB effettivamente letto e hashato; inventario di 50.000 elementi e 250 GB logici simulati; 2.000 file fisici copiati con secondo passaggio di verifica e nessuna copia aggiuntiva; video oltre 4 GiB scritto, riletto e pubblicato su NTFS. I 250 GB sono simulati, non scritti sul disco.

## Riproduzione

`scripts/dev.ps1 test -Large` esegue la suite completa corrente (348 casi); senza `-Large` 344. Il runner compilato supporta `--filter=folder` per i 28 casi pertinenti. `tests/PackageStagingTests.ps1` esegue i quattro casi confezionamento. Il pacchetto verifica anche `--interop-smoke`, `--worker-smoke` e i cinque stati UI senza enumerare dispositivi.

## Cosa non è stato verificato

Esportazione reale da Foto verso Windows, posizione SMB nel flusso di esportazione, originali disponibili solo su iCloud, archivio personale precedente, conteggio della raccolta Video sul telefono e compatibilità di ogni driver/iPhone. La cartella deve già contenere l’esportazione completa voluta dall’utente. Non è una certificazione, una promessa di assenza di bug o un totale di video garantito. Gli eventuali duplicati visivi fra versioni con byte diversi non vengono risolti semanticamente.
