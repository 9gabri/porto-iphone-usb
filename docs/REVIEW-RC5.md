# Correzioni dopo la prima prova fisica

La rc.4 ha evidenziato problemi durante una prova manuale su iPhone. Questa
revisione usa i report per la diagnosi e dati sintetici per riprodurre i difetti.
Nessun video personale è stato aperto o copiato nel progetto; nessun file della
destinazione reale è stato modificato. Nessun accesso automatico al telefono.

## Problemi confermati e cambiamenti

- P2, `WpdSource.Inventory`/`MediaInspector`: file `.AAE` privi di indicazioni
  video erano Unknown, producevano errori e provocavano nuovi passaggi. Sono
  ora file accessori esclusi con motivazione. Tipo Video o formato video
  contrastanti prevalgono sull'estensione; altri Unknown restano irrisolti.
- P1, `WpdSource.WpdReadStream`: un ultimo blocco con HRESULT S_FALSE veniva
  consegnato ma seguito da un'altra chiamata al driver. Un driver sintetico
  che chiude la risorsa alla fine restituisce poi errore di trasferimento:
  vecchia versione fallisce, nuova versione conserva il blocco e termina.
  Un blocco corto con S_OK continua invece la lettura. Il buffer rispetta
  anche valori ottimali inferiori a 4096; nessuna chiamata per Read(count=0).
- P1, `ImportEngine`: si usava solo la dimensione dell'oggetto inventariato.
  Ora GetResourceAttributes recupera TOTAL_SIZE della risorsa predefinita,
  trasmessa dal worker e verificata per ogni apertura. Se differisce
  dall'inventario sono obbligatorie due letture complete con uguale hash e
  lunghezza, oltre alla rilettura locale e al controllo del contenitore.
  Se la proprietà manca, resta il controllo sulla dimensione dell'inventario.
  Non viene accettata semplicemente una lettura più corta o più lunga.
- P1 trovato nel riesame: mantenere il filtro di riuso sulla dimensione
  dell'inventario creava una nuova copia a ogni passaggio se la risorsa aveva
  dimensione diversa. Il confronto con copie precedenti ora avviene quando
  esistono copie disponibili, senza quel filtro. Test su due passaggi.
- P2, diagnostica: errori di lunghezza riportano byte attesi e ricevuti;
  letture native interrotte riportano fase, posizione e dimensione risorsa.
  Le differenze inventario/risorsa restano esplicite nei risultati dei file.
- P2, UI: separati elementi USB, immagini, file AAE, risorse video, possibili
  componenti Live Photo/modifiche e candidati alla copia. Byte elaborati non
  equivale a file pubblicati. Barra azzerata quando il lavoro si ferma;
  aggiornamenti tardivi non sostituiscono il risultato finale.

## Verifica e limiti

Revisione indipendente di classificazione/UI e USB/integrità, con riesame del
percorso modificato. Nessun servizio esterno a pagamento o driver installato.
Regressioni iniziali: 3/5 sulla vecchia classificazione AAE e 5/9 sulla vecchia
gestione delle dimensioni. Le prove nuove includono ABI nativa e IPC con veri
processi figli, risorse oltre 4 GB, riferimenti COM anche su errore, EOF, letture
corte, troncamento, seconda lettura discordante, riuso e ripresa.

La baseline rc.4 è stata ricostruita in una directory isolata per riprodurre
la lettura aggiuntiva dopo il blocco S_FALSE, incluso il percorso dell'engine.
Una copia isolata dei sorgenti rc.5 è stata poi mutata invertendo il rilevamento
di EOF: la regressione fallisce sulla chiamata aggiuntiva al driver (0/1), non
per errori di compilazione. Nessuna mutazione nella sorgente distribuita.

I report reali non dimostrano che tutte le discrepanze dipendessero dalla
dimensione dell'oggetto o da una lettura dopo EOF. Errori persistenti del driver
o risorse non disponibili rimangono possibili. La rc.5 è una candidata per una
nuova prova controllata, non una soluzione fisicamente già validata.

La scansione continua a leggere i metadati dell'inventario USB completo;
non legge il contenuto delle foto o degli AAE per copiarli. Le risorse sospette
Live Photo/modificate restano ambigue e visibili: non possono essere dichiarate
video autonomi o ignorate per ottenere un esito verde. Il numero di file USB
non coincide necessariamente con gli elementi mostrati da Foto.

Usare una nuova cartella per il prossimo collaudo. Cataloghi rc.4 compatibili
rimangono leggibili, ma i vecchi obblighi Unknown non vengono cancellati
retroattivamente sulla sola base dell'estensione. Il trasferimento completo,
la corrispondenza con i video autonomi attesi e il comportamento di iOS
(compreso l'episodio Note) restano da confermare con l'utente.
