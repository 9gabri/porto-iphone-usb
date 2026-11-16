# Revisione rc.8 — copertura delle risorse USB

## Obiettivo e limiti dell’evidenza

Questo ciclo corregge esclusioni anticipate e interruzioni ripetute. Usa solo
codice, report/inventari autorizzati e dati sintetici. Nessun accesso a media
personali, telefoni reali o computer della cliente; nessuna spesa, API a consumo,
installazione o riduzione delle protezioni. Gli allegati privati non fanno parte
né del repository pubblico né del pacchetto.

Un conteggio dichiarato nell’album Video non è riconciliabile con certezza dai soli
metadati WPD. Non si attribuisce automaticamente lo scarto a iCloud e non si
trasformano componenti Live Photo in video autonomi per raggiungere un numero.
L’utente ha inoltre segnalato eliminazioni fra i collaudi: le copie storiche
locali restano conservate anche se la relativa sorgente non è più enumerata.

## Revisori indipendenti e controesempi

Due agenti separati, senza agenti annidati, hanno lavorato su media/classificazione
e USB/continuità. Si sono scambiati la revisione dei rispettivi cambiamenti e
hanno controllato indipendentemente engine, catalogo e destinazione. Massimo tre
cicli per area; nessuna riscrittura del protocollo o nuova dipendenza.

1. **Confermato, alto: esclusione pre-lettura per nome/foto abbinata.**
   In ImportEngine.Run, il ramo SuspectedCompanions produceva Ambiguous senza
   aprire la risorsa. Controesempio: MOV valido con foto omonima oppure IMG_E.
   Conseguenza: nessuna copia anche se l’indizio non prova una Live Photo.
   Correzione: tutte le risorse candidate vengono tentate, con verifica e
   classificazione separata delle copie aggiuntive. Le foto non vengono lette.
2. **Confermato, medio: stringhe opache considerate prova Live Photo.**
   MediaInspector cercava marcatori nei byte di moov. Una stringa dentro free
   o un solo content.identifier bastava per trattenere un video valido.
   Correzione: struttura QuickTime delimitata, namespace esatto, traccia meta,
   tabella keys e keyd; il risultato resta soltanto un indizio. Le fonti Apple
   precisano che la registrazione di una chiave non prova che il valore sia stato
   scritto. Nessuna certificazione dell’identità Foto.
3. **Confermato, alto: più interruzioni del budget impediscono di raggiungere sani.**
   ImportSession aveva sempre tre passaggi. Sei file bloccanti con ID rimappati
   lasciano risorse sane mai aperte. Correzione: continuazioni limitate solo con
   nuovo ostacolo raggiunto, precedenza agli elementi non ancora visitati,
   budget immutabile dal primo inventario completo. No associazioni per nome.
4. **Confermato, medio: quarantena precedente non riclassificabile.**
   Il riuso del fingerprint ritornava sempre Ambiguous. La rc.8 ricontrolla il
   contenitore e pubblica una nuova copia verificata, con transazione Replaces,
   senza spostare o alterare il vecchio .part. Crash reali ai cinque confini,
   collisioni e molteplicità vengono verificati.
5. **Confermati durante revisione, medio: classificazione e storia nel riuso.**
   Il percorso precedente non deve cancellare un nuovo indizio aggiuntivo.
   Un contenitore accettato prima ma ora riconosciuto malformato non può diventare
   AlreadyPresent riuscito. Un elemento comparso nel solo inventario finale è
   attuale, mentre un vecchio errore privo di prova resta storico. Una copia
   locale mancante con sorgente assente resta un problema storico visibile.
   Correzioni e regressioni dedicate preservano byte, percorsi e debiti.

## Verifiche e prova negativa

La suite finale e i risultati grezzi pubblicabili sono in TESTS.md e
 test-results.txt. Comprende parser, riuso, promozioni con morte reale del
processo, conflitti di hash, duplicati distinti, USB bloccato, inventari variabili,
file grandi, NTFS e report. Le vecchie fixture che usavano content.identifier
come sinonimo di contenitore invalido sono state sostituite con contenitori
realmente troncati: le asserzioni di conservazione non sono state eliminate.

Prova negativa in copia isolata: ripristinando il limite rigido di tre passaggi,
la compilazione riesce ma il test con sei errori e ID rimappati fallisce (0/1)
perché mancano risorse sane. Il medesimo test passa nel codice distribuito.
Nessuna mutazione nei sorgenti consegnati. Quattro prove autonome del packaging.

## Cosa resta da confermare

Nessun difetto bloccante confermato lasciato irrisolto nel codice sottoposto a
questo ciclo. Questo non certifica la compatibilità fisica: la catena reale di
errori del driver non è riproducibile con certezza dai soli report. Serve una
normale importazione sullo specifico iPhone, nella stessa destinazione conservata.
Non serve selezionare né verificare manualmente ciascun video.

Le risorse aggiuntive possono essere migliaia e richiedere molto più tempo e
spazio. Il parser verifica il contenitore, non decodifica i fotogrammi. Risorse
non esposte via USB, originali solo iCloud e contenuti eliminati non possono essere
recuperati tramite questa modifica. Non è promesso un totale fisso, né una
versione “definitiva”, “certificata” o “senza bug”. Gli errori permanenti restano
espliciti e i tentativi sono finiti.
