# Telefoni, foto, video e album — rc.10

Porto copia **dal telefono al PC**, con accesso in sola lettura al dispositivo. Puoi scegliere **Solo video**, **Solo foto** oppure **Foto e video**. L’elenco viene cercato automaticamente: non devi selezionare ogni file.

## Collegamento USB

1. Estrai tutto il nuovo ZIP in una cartella separata e apri Porto.exe.
2. Collega il telefono con un cavo dati e sbloccalo. Su iPhone autorizza il computer e imposta **Mantieni originali**. Su Android scegli **Trasferimento file** nella notifica USB; i nomi possono cambiare secondo il modello.
3. In Porto scegli **Importa da → Telefono / dispositivo USB**, premi **Cerca dispositivi** e scegli il tuo dispositivo.
4. In **Cosa copiare**, scegli Solo video, Solo foto oppure Foto e video.
5. In **Ambito USB**, lascia **Tutto il dispositivo accessibile**, oppure scegli **Una cartella del dispositivo → Scegli cartella USB**. Sono incluse le sottocartelle. Se la cartella sparisce, cambia identità o è ambigua, Porto interrompe la selezione senza allargarla al telefono intero.
6. In **Archivio PC**, scegli la cartella dove conservare le copie. Per confrontare un archivio Porto esistente scegli la sua radice, conservando .porto, report e file.
7. Premi **Avvia importazione**. Alla fine apri il report. Pausa e Riprendi conservano il lavoro e ripetono i controlli.

Porto vede i dispositivi portatili esposti da Windows tramite WPD, normalmente MTP/PTP. È supporto al protocollo, **non una promessa di compatibilità con ogni telefono**. Un dispositivo non esposto dal sistema non viene raggiunto da Porto. L’app non installa driver, non modifica permessi del telefono e non riduce le protezioni Windows.

## Album delle app

Una cartella USB non è necessariamente un album. Alcune gallerie Android mostrano cartelle reali come DCIM/Camera o Pictures/Vacanze; Foto di iPhone e Google Foto possono invece usare album logici che non compaiono come cartelle USB. Porto non ricostruisce questi album da nomi o date.

Per scegliere un vero album non esposto via USB, esportane gli originali in una cartella separata sul PC usando gli strumenti già disponibili. Poi in Porto:

1. **Importa da → Cartella / album esportato**.
2. **Scegli origine**: la cartella dell’album, comprese le sottocartelle.
3. **Cosa copiare**: solo foto, solo video o entrambi.
4. **Archivio PC**: la cartella precedente oppure una cartella dedicata a quell’album.
5. **Avvia importazione**.

Porto verifica la cartella ricevuta; non può verificare che l’esportazione comprenda l’intero album. Non configura condivisioni di rete né richiede Mac o nuove app a pagamento. La preparazione dell’esportazione dipende dall’app fotografica e va provata sul dispositivo reale. [Dettagli dell’esportazione iPhone](FOLDER-IMPORT.md).

## Copie precedenti e destinazioni

I file precedenti restano al loro posto. Le copie identiche sono riutilizzate dopo confronto completo e SHA-256; file distinti della selezione con gli stessi byte richiedono copie distinte. File mancanti o danneggiati non sono considerati copie valide; un recupero usa un nuovo nome e conserva il danneggiato. Versioni convertite o modificate con byte diversi restano distinte.

Le nuove foto vanno in **Foto Porto**, i video in **Video Porto**. Gli indizi Apple di Live Photo o modifiche possono indirizzare video in **Risorse video aggiuntive**: non sono prove della loro appartenenza all’album Video. Questi indizi Apple non vengono applicati indiscriminatamente ai dispositivi di altri produttori. Le vecchie copie non riutilizzate non sono automaticamente Live Photo.

L’app non riordina le copie precedenti per album. Una copia già verificata resta nel percorso originale indicato nel report. Un archivio separato per telefono/libreria mantiene più semplice lo storico. I nuovi identificatori USB includono il dispositivo; gli archivi precedenti restano leggibili e vengono confrontati per contenuto.

## Formati e limiti espliciti

- Video: MOV, MP4, M4V, con verifiche strutturali del contenitore. Non viene decodificato ogni fotogramma.
- Foto: JPEG, PNG statico, HEIF/HEIC con immagini HEVC e metadati riconosciuti. Verifica della struttura, non certificazione della decodifica dei pixel. PNG: anche CRC, decompressione limitata e coerenza delle scanline. Limite prudenziale foto 512 MiB; altri profili sono segnalati come ambigui.
- Foto/video riconosciuti ma non supportati, file troncati e metadati contraddittori restano visibili come non verificabili; non vengono spacciati per copie completate. I tipi esclusi dal filtro scelto hanno una motivazione nel report.
- Solo contenuti accessibili localmente; Porto non scarica file da iCloud, Google Foto o altri cloud. Il totale USB non è il totale della libreria sul telefono.
- Origine locale e archivio: cartelle NTFS separate. Destinazione locale Windows 11 x64, percorso massimo 120 caratteri. Rete, exFAT, Windows ARM, collegamenti e junction non supportati.
- Byte ricevuti conservati, nessuna conversione. È il telefono a decidere quali byte esporre: Porto non può provare che siano gli originali fotografici senza una fonte indipendente.

**Stato:** candidata verificata con dati sintetici; collegamento reale iPhone/Android, cartelle effettivamente esposte e album esportati restano da collaudare. Nessun totale di file o compatibilità universale promesso.
