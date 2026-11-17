# Telefoni, foto, video e album — rc.18

La rc.18 distingue gli album con associazioni integre da quelli non verificabili. Nel selettore gli album coinvolti restano visibili con il numero di associazioni irrisolte e non possono essere scelti. La galleria completa resta bloccata finché esistono queste anomalie. Gli altri album possono essere provati, senza estendere la selezione. La causa sul telefono non è ancora nota; l'album desiderato potrebbe restare bloccato. Conservati limite 1 GiB e controlli di memoria della rc.17. Prima di aggiornare chiudi Porto ed estrai la candidata in una cartella nuova, conservando tutti i file precedenti.

Porto copia dal telefono al PC, senza cancellare o spostare gli originali. La nuova modalità iPhone legge il catalogo della libreria Foto; non richiede un'esportazione preliminare. È una candidata da collaudare: compatibilità reale non ancora confermata.

## Aggiornamento

1. Chiudi Porto.
2. Scarica ed estrai tutto lo ZIP in una cartella nuova. Conserva la vecchia versione e l'intero archivio precedente.
3. Apri il nuovo Porto.exe. Non disattivare le protezioni Windows.

## iPhone: galleria o album personale

1. Collega l'iPhone con un cavo dati, sbloccalo e autorizza il computer. Il collegamento Apple e l'abbinamento devono essere già presenti: Porto non installa componenti né crea nuovi abbinamenti.
2. In Porto scegli **Importa da → iPhone · libreria Foto via USB**, poi **Cerca dispositivi** e scegli l'iPhone.
3. Scegli **Solo video**, **Solo foto** oppure **Foto e video**.
4. In **Ambito USB** scegli **Galleria visibile · tutti gli elementi**, oppure **Un album della libreria Foto → Scegli album** e seleziona l'album personale.
5. In **Archivio PC → Scegli cartella**, scegli la destinazione. Per confrontare le copie precedenti usa la loro cartella principale, conservando `.porto` e Report Porto.
6. Premi **Avvia importazione**. Tieni il telefono collegato e sbloccato. Puoi usare **Pausa** e poi **Riprendi**.
7. Leggi l'esito e **Apri report**. Errori, originali mancanti o inventario cambiato non equivalgono a importazione completata.

Le Live Photo sono trattate come immagini: con Solo video le componenti animate non vengono copiate. Con Solo foto o Foto e video viene copiata la sola immagine originale della Live Photo, non l'effetto animato. Le copie precedenti, anche eventuali clip Live, restano al loro posto.

Sono supportati gli album personali riconosciuti nel catalogo, non tutte le raccolte intelligenti/condivise. Nascosti ed Eliminati di recente sono esclusi con conteggi visibili. Gli originali presenti solo su iCloud non sono scaricati da Porto. Un catalogo non accessibile, oltre 1 GiB o con schema non riconosciuto blocca questa modalità; non viene sostituito con i file USB generici. La lettura del catalogo può richiedere tempo, ma non esplora tutte le cartelle del telefono.

## Prima prova iPhone rc.18

1. Apri la rc.18 sul PC Windows della cliente. Collega e sblocca il suo iPhone con un cavo dati; il computer deve essere già autorizzato e il collegamento Apple disponibile. Porto non installa componenti né crea l'abbinamento.
2. Seleziona **Importa da → iPhone · libreria Foto via USB → Cerca dispositivi**. La comparsa del telefono prova soltanto la ricerca del dispositivo, non ancora l'accesso alla libreria.
3. Seleziona **Un album della libreria Foto → Scegli album**. Questa operazione verifica anche la sessione Apple e la lettura del catalogo senza copiare foto o video. Se compare un errore, conserva il messaggio: non passare alla modalità USB generica per dichiarare risolto il problema delle Live Photo.
4. Se disponibile, scegli un piccolo album personale esistente con video originali locali. Seleziona **Solo video** e una nuova cartella NTFS di prova. Le componenti animate delle Live Photo non devono essere importate come video. Se non c'è un album piccolo adatto, l'elenco degli album resta comunque una verifica preliminare; il trasferimento della selezione richiesta va ancora collaudato.
5. Premi **Avvia importazione**, mantieni il telefono collegato e sbloccato e leggi l'esito. Un report incompleto o con elementi irrisolti non è un collaudo superato. Conferma anche che i video della piccola prova si aprano correttamente.
6. Ripeti sulla stessa piccola selezione/destinazione: il risultato atteso è copie già presenti verificate, senza nuove copie. Poi passa alla galleria o all'album desiderato e all'archivio precedente della cliente, conservando `.porto`, report e tutte le copie esistenti.

Un errore di lettura non certifica che un file sia in iCloud. Originali non disponibili, catalogo cambiato e problemi del collegamento sono segnalati distintamente quando il protocollo consente di determinarli. Nessun totale fisso è garantito, soprattutto se nel frattempo sono stati eliminati o modificati elementi nella libreria.

## Android e altri dispositivi USB


1. Collega il telefono, sbloccalo e scegli **Trasferimento file** nella notifica USB, se disponibile.
2. In Porto scegli **Importa da → Dispositivo USB · cartelle e raccolte → Cerca dispositivi**, poi il telefono.
3. Scegli Solo video, Solo foto oppure Foto e video.
4. In **Ambito USB** scegli tutti i file accessibili oppure **Una cartella o raccolta USB → Scegli cartella / raccolta**.
5. Apri una memoria/cartella con doppio clic. Porto legge solo quel livello, senza una scansione preliminare dell'intero telefono. Seleziona la cartella e premi **Usa questa cartella**; le eventuali raccolte native sono indicate come **[Raccolta USB]**.
6. Scegli Archivio PC e premi Avvia importazione. Una cartella comprende le sue sottocartelle; una raccolta comprende soltanto i riferimenti esposti dal dispositivo.

Per copiare una cartella puoi selezionarla dal genitore senza aprirla: eviti una lettura preliminare di tutti i suoi figli. Tutti i file accessibili richiede invece un inventario completo; senza un indice della galleria non è corretto saltare cartelle e affermare che non manchi nulla.

Le cartelle fisiche e le raccolte WPD non equivalgono a tutti gli album della galleria. Album virtuali privati di Google Foto, OPPO o altre app potrebbero non comparire. Questa candidata non risolve l'accesso universale a tali raccolte e non dichiara supportato qualsiasi cellulare.

## Copie precedenti e limiti

Usa lo stesso archivio per riutilizzare copie integre con gli stessi byte. Il confronto è completo e richiede letture: non si basa soltanto su nomi o dimensioni. Due elementi distinti con byte uguali mantengono due copie. File danneggiati, estranei e copie non riutilizzate restano conservati. Le copie non vengono riorganizzate per album.

Nuove foto in Foto Porto, video in Video Porto. Cartella del PC resta una fonte accessoria per file già disponibili localmente.

Formati: MOV/MP4/M4V, JPEG, PNG statico, HEIF/HEIC HEVC riconosciuti. Gli altri formati rimangono non verificabili. Archivio locale NTFS su Windows 11 x64, percorso massimo 120 caratteri, niente rete/exFAT/link/junction. Nessuna conversione o decodifica completa dei media.

Non è stata effettuata una nuova prova con telefoni reali durante questo sviluppo. Il collaudo deve confermare accessibilità del catalogo, album, conteggi, riconnessione e trasferimento effettivo; nessun totale, velocità o compatibilità universale garantiti.

## Recuperare foto rifiutate dalla rc.11

La rc.12 accetta JPEG la cui immagine principale è completa ma seguita da dati aggiuntivi. Conserva l’intero file e indica nel report quanti byte aggiuntivi non sono stati interpretati. Usa la stessa origine e la stessa cartella Archivio PC, mantenendo .porto e report. Le copie sane sono riutilizzate; i temporanei corrispondenti vengono recuperati in nuove copie, senza modificare i vecchi .part. La coda non viene convertita o separata in un video. Il controllo resta strutturale e non certifica la decodifica dei pixel o delle funzioni aggiuntive.
