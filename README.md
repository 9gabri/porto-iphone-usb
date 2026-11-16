# Porto

App locale italiana per importare via USB i video accessibili di un iPhone su Windows 11 x64. Versione **0.1.0-rc.8**, candidata sperimentale: il collegamento WPD è reale, ma **questa versione richiede ancora una prova con iPhone fisico**. I collaudi precedenti hanno evidenziato errori e conteggi ancora da riconciliare.

[Pagina e istruzioni](https://9gabri.github.io/porto-iphone-usb/) · [Download Windows](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.8/Porto-0.1.0-rc.8-win-x64.zip) · [Checksum SHA-256](https://github.com/9gabri/porto-iphone-usb/releases/download/v0.1.0-rc.8/SHA256SUMS.txt)

La rc.8 tenta tutte le risorse video USB: nomi `IMG_E…`, foto omonime e
indizi di Live Photo non impediscono più la lettura. Le copie con questi
indizi vengono verificate e conservate in **Risorse video aggiuntive**,
separatamente dai video riconosciuti senza tali indizi. Possono essere migliaia
di clip brevi: non equivalgono a migliaia di video autonomi nell'app Foto.
[Revisione, garanzie e limiti](docs/REVIEW-RC8.md).

**Per riprendere una prova precedente, usa la stessa destinazione conservando
tutti i video, i report e `.porto`. Estrai l'app rc.8 separatamente.**
Le copie già presenti vengono rilette e verificate senza spostarle. Gli errori
USB vengono isolati con processi nuovi e continuazioni limitate, dando
precedenza alle risorse non ancora raggiunte. Non serve selezionare i video.

## Provala

1. Scarica lo ZIP, estrai **tutta la cartella** e apri `Porto.exe`. Il runtime è incluso: non servono compilatori o un'installazione .NET.
2. Collega l'iPhone via USB, sbloccalo e autorizza il PC. Apple indica l'app ufficiale **Dispositivi Apple** come prerequisito Windows: se mancano i relativi componenti, installala dal Microsoft Store seguendo [Apple](https://support.apple.com/en-us/120267). Porto non installa driver.
3. Sul telefono verifica **Impostazioni → App → Foto → Trasferisci su Mac o PC → Mantieni originali**. Scegli una cartella locale NTFS e premi **Avvia importazione**.

L'app inventaria prima di copiare e non richiede selezioni di file o lotti. **Pausa** interrompe anche l'attesa di riconnessione. **Riprendi** esegue un nuovo inventario e confronta i contenuti già copiati. Un temporaneo appartenente allo stesso elemento viene confrontato integralmente con il prefisso USB: solo se coincide viene aggiunta la parte mancante. Nessun byte esistente viene sovrascritto o troncato. Se non è associabile o diverge, viene conservato e si prepara una nuova copia.

Gli errori del singolo video invalidano il collegamento prima della lettura
seguente. Ogni nuovo processo deve rileggere un inventario completo e identico
prima di usare gli ID precedenti. Se cambia, il video non aperto resta pendente
e il passaggio successivo usa gli ID nuovi. Tre passaggi ordinari possono essere
seguiti da continuazioni aggiuntive solo finché viene raggiunto un nuovo ostacolo
prima non tentato: massimo 128, con limite fissato dal primo inventario completo.
Nomi e dimensioni servono soltanto all'ordine, mai a dichiarare una copia riuscita.
Attese cancellabili di 15 secondi, stesso dispositivo, nessun retry infinito.
Disco pieno e permessi insufficienti fermano il trasferimento. Errori persistenti,
contenuti non riconosciuti e osservazioni storiche non riconciliate restano visibili.

Al termine viene riletto l'inventario USB: se cambia o rimane incompleto, il risultato non è dichiarato completo. Le osservazioni precedenti ancora pendenti restano visibili. Se gli identificatori cambiano prima che un contenuto sia verificato, può restare un debito storico non riconciliabile: non viene cancellato per ottenere un risultato verde. Il confronto non è uno snapshot atomico della libreria Foto.

La rc.3 conserva anche il numero minimo storico di copie identiche provato dagli inventari: pause e successivi cambi di ID non possono azzerare un duplicato ancora dovuto. Dopo la perdita della sessione USB è obbligatorio un inventario nuovo; gli errori del singolo file non bloccano quelli successivi. Report e inventari vengono pubblicati dopo scrittura e flush completi. Cataloghi estranei e file con hard link vengono rifiutati senza modificarli.

La rc.4 rimanda i video che hanno interrotto una sessione dopo gli altri, anche quando nomi e dimensioni coincidono. I suggerimenti servono solo all'ordine: ogni video richiede comunque la verifica del contenuto. Le copie storiche vengono ricontrollate anche se la sorgente non ricompare; elementi di tipo ignoto rimangono esplicitamente non verificati. Cataloghi, percorsi e impostazioni hanno controlli aggiuntivi prima delle scritture.

Per aggiornare, chiudi la versione precedente ed estrai il nuovo ZIP separatamente. Conserva la destinazione con `.porto`. I lavori pendenti delle vecchie versioni privi di storia sufficiente restano segnalati come non verificabili anche dopo nuove copie; non vengono dichiarati risolti per supposizione. Dopo la migrazione usa la nuova versione per quella destinazione.

Se il telefono non compare, **Cerca iPhone** distingue nessun dispositivo portatile esposto da Windows, nessun dispositivo identificato Apple e metadati non leggibili. Verifica prima il riconoscimento in **Dispositivi Apple**. Una lista vuota può richiedere ulteriore diagnosi di Porto/driver.

Il pacchetto non è firmato con un certificato commerciale: Windows può mostrare un avviso o impedirne l'avvio secondo le proprie policy. Non disattivare SmartScreen, antivirus o altre protezioni. La riproduzione HEVC dipende dai codec disponibili sul PC; la copia non richiede decoder o acquisti.

## Che cosa viene salvato

- Video MOV, MP4 e M4V, con controllo del contenitore e della traccia video, anche oltre 4 GB. Nessuna conversione, ricompressione o riscrittura dei metadati interni.
- Nuovi file in `Video Porto` o `Risorse video aggiuntive`, con nome sanificato e suffisso univoco. Copie già verificate restano nella cartella precedente anche quando emergono nuovi indizi di classificazione. File preesistenti mai sovrascritti.
- Catalogo SQLite, inventari e temporanei in `.porto`; report leggibile `.txt` ed esportabile `.json` in `Report Porto`.
- SHA-256 calcolato sui byte ricevuti e confrontato con la rilettura locale. La verifica approfondita ripete anche la lettura USB e richiede più tempo; se fallisce, il nuovo file non viene pubblicato.
- Identificatori USB, nome, data e dimensioni **non bastano** per saltare un file. Il confronto è sul contenuto completo, mantenendo la molteplicità degli elementi identici dell'inventario. Le importazioni ripetute rileggono quindi la sorgente.

Nessuna funzione di cancellazione, modifica, rinomina o sincronizzazione sull'iPhone. Nessuna cancellazione automatica nella destinazione, neppure dei temporanei interrotti. Temporanei divergenti o non associabili possono occupare spazio aggiuntivo; quelli associabili vengono ripresi dopo confronto e gli ambigui verificati vengono riutilizzati. Conserva `.porto` insieme ai video per mantenere la ripresa senza duplicati. Un catalogo perso o rimosso non può riconoscere automaticamente copie precedenti.

La cartella scelta viene ricordata in `%LOCALAPPDATA%\Porto\destination.txt`, solo sul PC. Durante il trasferimento Porto richiede a Windows di evitare la sospensione per inattività; non impedisce blocco schermo, sospensione manuale o chiusura del coperchio. I pulsanti restano visibili ridimensionando la finestra; il contenuto può scorrere.

## Limiti da conoscere prima della prova

**Integrità non significa originalità o completezza.** La verifica dimostra la corrispondenza con i byte ricevuti tramite USB. Non è un checksum calcolato dall'iPhone e non certifica che la risorsa sia l'originale Foto non modificato. “Mantieni originali” evita la conversione di formato; non rende tutte le risorse WPD originali certificati. Le varianti con nome `IMG_E…` vengono copiate tra le risorse aggiuntive; altre versioni modificate potrebbero non essere distinguibili.

Le fotografie sono escluse dalla lettura dei contenuti. Tutte le risorse video
candidate vengono tentate, anche se esiste una foto omonima. Il parser legge la
struttura dei metadati temporizzati: una chiave `still-image-time` strutturata è
un indizio, non la prova certa di una Live Photo. La sola stringa o il solo
`content.identifier` non escludono un video. I contenitori invalidi restano
conservati come temporanei e segnalati, senza fingere che siano video validi.
Vecchi temporanei validi vengono riclassificati tramite una nuova copia,
preservando i byte del temporaneo originale. Non si accede ai dati di altre app iOS.

Porto funziona offline, non chiede Apple ID e non scarica da iCloud. WPD non offre in questa implementazione un conteggio affidabile degli originali solo cloud: **la copertura dell'intera libreria non è verificabile**, anche quando finisce l'inventario. “Mancanti noti: 0” non significa che non manchi nulla su iCloud. Non cambiamo impostazioni del telefono e non consigliamo di liberarlo cancellando contenuti.

La candidata accetta solo destinazioni **locali NTFS** con percorso fino a 120 caratteri. Rete, exFAT e percorsi con junction/symlink vengono rifiutati. Windows ARM viene bloccato perché non verificato. Nessuna promessa di velocità USB, compatibilità iPhone 16/17 o versione iOS: dipendono dal dispositivo, cavo, driver e comportamento effettivo.

Usa una cartella dedicata a una libreria/telefono. La riconciliazione conserva il numero di copie per contenuto osservato nell'inventario, ma non può provare l'identità Foto di due elementi byte-identici tra sessioni o librerie differenti. Non unire più telefoni nella stessa destinazione se vuoi mantenerne distinte le copie identiche.

## Verifiche e struttura

Vedi [test e compatibilità](docs/TESTS.md), [architettura e sicurezza](docs/ARCHITECTURE.md), [fonti primarie](docs/SOURCES.md) e [dipendenze/licenze](THIRD-PARTY-NOTICES.md).

`src/Porto.Core`: copia, riconciliazione, parser, catalogo e report. `src/Porto.Usb`: WPD e worker isolato. `src/Porto.App`: WinForms. `tests/Porto.Tests`: sola sorgente sintetica, non distribuita nell'app.

Per sviluppatori, su Windows x64 con SDK .NET 10.0.403: `powershell -File scripts/dev.ps1 test` e `powershell -File scripts/dev.ps1 publish`. Il progetto non ha pacchetti NuGet applicativi; la pubblicazione scarica i runtime Microsoft 10.0.14. Cache di build nel progetto, nessun driver richiesto per i test. Non eseguire l'app in modalità normale durante test senza autorizzazione ai dispositivi; `--ui-smoke <percorso.png>` disabilita la ricerca USB.

Licenza MIT. Progetto indipendente, non affiliato ad Apple o Microsoft.
