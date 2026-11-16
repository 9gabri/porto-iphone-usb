# Revisione rc.10 — 16 novembre 2026

Obiettivo: conservare la copia massiva dei video, aggiungere foto/entrambi, dispositivi WPD non Apple e selezione di una cartella fisica senza confonderla con album virtuali. Nessun telefono reale o archivio personale letto. Nessun nuovo servizio, driver, costo o riduzione delle protezioni.

## Metodo e responsabilità

Due revisori indipendenti, nella quota disponibile, con proprietà dei file separate: uno su parser foto e fattibilità album, l’altro su motore, cataloghi e selezione. Dopo l’implementazione si sono riesaminati reciprocamente; il responsabile ha integrato USB/UI/report e riprodotto i controesempi. Tre cicli circoscritti per l’area parser, due per scope/USB. Nessuna riscrittura della pipeline di trasferimento.

I test sono stati osservati fallire per i controesempi prima delle correzioni, quando applicabile: API foto mancanti, nuovi filtri non ancora collegati, PNG compresso incoerente, PNG con coda duplicata, grid HEIF fuori copertura e amplificazione NAL. Un test di riuso inizialmente fallito dipendeva invece dalla fixture: nomi foto/video abbinati attivavano correttamente il comportamento Apple. La fixture è stata disaccoppiata e la distinzione Android/Apple coperta separatamente.

## Difetti confermati e correzioni

| Area / percorso | Controesempio e conseguenza | Gravità | Correzione e prova |
| --- | --- | --- | --- |
| ImportEngine / InspectSelected | Metadati video e nome .jpg potevano soddisfare Solo foto con byte video, anche riutilizzando una copia precedente | Alta per correttezza filtro | Controllo del tipo verificato in copia nuova e riuso; esito Ambiguous, nessuna copia dichiarata valida per il filtro |
| UsbFolders / List | Nome fisico `A / B` e livelli `A`→`B` mostravano la stessa etichetta e potevano far scegliere il ramo sbagliato | Media | Escape dei separatori, percentuali e controlli solo nella visualizzazione; segmenti originali conservati. Test di etichette e ramo risolto |
| WpdSource + ImportEngine / NeedsTracking | GenericFile con clip.mkv poteva apparire come semplice file estraneo escluso | Media | Tipi WPD noti distinti, estensioni video non supportate visibili come ambigue in video/entrambi; Solo foto le esclude come tipo non scelto |
| PhotoInspector / CheckPngRows | CRC di chunk valido con dati compressi o scanline incongruenti poteva superare controlli solo strutturali | Alta per verifica | Decompressione limitata, filtri, quantità di scanline, Adler e fine stream |
| PhotoInspector / RangeStream | Coda aggiunta dopo zlib con Adler duplicato poteva essere consumata anticipatamente dal buffer e apparire valida | Media | Ultimi byte alimentati singolarmente e verifica del consumo esatto; regressione con code di dimensioni diverse |
| PhotoInspector / ValidateImage | Grid HEIF più grande della copertura delle tile veniva accettata | Media | Geometria grid e dimensioni omogenee dei tile verificate; regressione grid 2px con tile 1px |
| PhotoInspector / configurazioni | Molti item con proprietà hvcC condivisa amplificavano le riletture | Media, disponibilità | Cache della configurazione e delle dimensioni, metadati limitati |
| PhotoInspector / NAL | 256 ID tile che riusavano una piccola extent da 800 NAL superavano il limite per immagine aggregata | Media, disponibilità | Budget NAL cumulativo 200.000; fixture sotto 100 kB osservata fallire prima e verificata nella suite finale |

## Garanzie esaminate

- Namespace USB separa la storia di dispositivi con ID oggetto uguali. SourceItem legacy con namespace nullo conserva gli stessi byte JSON e la stessa chiave ledger.
- Cartella scelta risolta tramite catena completa e persistent ID disponibili. ID transitori cambiati ammessi solo nella nuova risoluzione; assenza, cicli, parent mancanti, spostamenti o ambiguità non ampliano la selezione.
- Wrapper inoltra gli oggetti originali al recupero USB, che mantiene le verifiche dell’inventario completo. Errori di enumerazione non nascosti dalla selezione.
- Modalità predefinita resta Solo video. Foto Porto aggiunge soltanto percorsi generati e formati esplicitamente ammessi al catalogo.
- 193 file video sintetici preesistenti conservati con confronto dei byte mentre vengono importate foto da una cartella locale. Riuso, molteplicità, vecchi cataloghi e formati esclusi hanno casi dedicati.
- UI blocca filtri, origine e scope durante il lavoro; cambio dispositivo azzera la cartella scelta; l’avvio richiede una selezione valida. Ricerca USB solo su azione esplicita dell’utente.
- Report mostra fonte, ambito, filtro, conteggi foto/video, esclusioni e incertezze. Nessuna rimozione automatica delle copie precedenti o classificazione automatica di quelle non riutilizzate come Live Photo.

## Esito e limiti

Nessun difetto bloccante residuo rilevato nelle revisioni circoscritte. [Test finali](TESTS.md) e relativo output documentano ciò che è stato effettivamente eseguito.

Il controllo foto è strutturale: non decodifica tutti i pixel JPEG/HEVC, non certifica originalità o assenza di qualunque difetto. Limiti 512 MiB foto/PNG decompresso, 16 MiB metadati HEIF, budget strutturali e profili conservativi. Non è lettura universale degli album delle app. WPD generico non garantisce compatibilità con ogni dispositivo/driver. Il collaudo fisico iPhone/Android e l’esportazione di album reali restano da fare.
