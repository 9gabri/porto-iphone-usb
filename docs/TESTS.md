# Verifiche 0.1.0-rc.22

18 novembre 2026. Build Release senza avvisi/errori. Solo cartelle sintetiche nuove, nessun telefono o file personale consultato/modificato.

- Riproduzione iniziale: 0/1, fallimento atteso con gli esatti valori APPLE-CATALOG-MEMORY della schermata. Dopo la modifica sei nuove regressioni passate, comprese nella suite.
- Suite finale con eseguibile Porto.Tests.exe: **565/565 passati**, inclusi processi figli, NTFS, interruzioni, TLS, cataloghi, WAL, album, molteplicità e ripresa.
- Il primo avvio della suite mediante dotnet DLL è stato interrotto: alcuni test richiedono il proprio eseguibile per avviare i figli e non funzionano tramite dotnet. Non è contato come verifica riuscita. Ripetizione completa corretta senza casi ignorati.
- Catalogo grande finale: **1/1 passato**, database sintetico valido 1.426.288.640 byte, WAL 7.337.752 byte, due catture consecutive. Prima cattura 3.001 ms, 9.544.200 byte gestiti allocati; picco finale 1.491.603.456 byte. Memoria privata prima/dopo le due catture 19.062.784/43.200.512 byte. Quattro aperture e 2.867.252.784 byte letti per cattura. Hash sorgenti invariati. Misure locali, non USB; non stima di un'ora.
- Packaging: **4/4 passati**. Confezionamento con due prove senza dispositivi e dieci schermate sintetiche; esiti pubblici annotati dopo il tag in RELEASE-VERIFICATION.md.

Prima prova estesa e spiegazione in REVIEW-RC22.md. Storico test-results-rc21.txt preservato. Revisione locale, revisori delegati indisponibili per quota; nessun fallback a pagamento. Trasporto e motore media non modificati: benchmark USB simulato e scrittura video oltre 4 GiB non ripetuti in questo ciclo. Schema iOS, memoria effettiva e album reale restano da confermare.

---
# Verifiche 0.1.0-rc.21

17 novembre 2026; build Release senza avvisi/errori, .NET 10.0.14. Solo nuovi dati sintetici, nessun telefono, catalogo personale o file delle prove consultato o modificato.

- Riproduzione prima della modifica: **0/2**, fallimento atteso con APPLE-CATALOG-DB-LIMIT e le dimensioni esatte della schermata (DB 1.416.859.648 byte, WAL 7.337.752 byte). Non è un fallimento residuo della candidata.
- Regressioni mirate dopo la correzione: **4/4**, comprese RAM/commit al confine e rifiuto degli input dimensionali non validi. Questi quattro casi sono inclusi nella suite completa.
- Suite ordinaria finale: **559/559 passati** in una sola esecuzione, comprese fixture NTFS hard link/junction, crash dei processi figli, TLS, catalogo/WAL, errori, copie precedenti, molteplicità e ripresa. Nessun caso ignorato.
- Prova estesa rc.21: **1/1 passata**, ripetuta con due catture consecutive complete nello stesso processo. SQLite sintetico valido 1.426.288.640 byte, WAL 7.337.752 byte; 2.867.252.784 byte letti per cattura, quattro aperture e controllo integrità/album/hash. Prima esecuzione 3.767 ms; prima cattura della ripetizione 3.276 ms, poi seconda cattura verificata. Picco circa 2,91 GB, non una misura USB. Fixture conservata; dettagli in REVIEW-RC21.md.
- Packaging: **4/4 passati**. Il pacchetto prevede due smoke senza dispositivi e dieci stati UI sintetici, incluso il messaggio di limite lungo aggiornato. Esiti del pacchetto riscaricato registrati dopo il tag in RELEASE-VERIFICATION.md.

Log in test-results.txt, storico precedente conservato in test-results-rc20.txt. Benchmark del trasporto e prove estese sui media non ripetuti: trasporto e motore di copia non cambiano. Le prove sintetiche non certificano lo schema del telefono reale, la sua disponibilità degli originali o i tempi di trasferimento. Revisori delegati non disponibili per quota, nessun fallback a pagamento.

---
# Verifiche 0.1.0-rc.20

17 novembre 2026; build Release, .NET 10.0.14. Solo fixture sintetiche e nuove directory. Nessun telefono, record di abbinamento o file personale delle prove letto o modificato.

- Suite ordinaria finale: **555/555 passati** in una sola esecuzione, incluse le fixture NTFS hard link/junction e gli arresti reali dei processi figli. Nessun test ignorato. Build senza avvisi/errori.
- AFC: letture complete a 64/256/512 KiB, due blocchi pieni più coda, EOF, hash, numero richieste e rifiuto di risposte oltre la lunghezza richiesta. Il precedente caso a 256 KiB resta esplicito; aggiunto il caso per il default 512 KiB. TLS, catalogo, selezione, quantità, resume e copie precedenti restano nella suite completa.
- Benchmark AFC finale: warmup e tre ripetizioni per variante; tutti i controlli di hash/byte/richieste passati, più dodici guasti sintetici (sei per ciascuna delle due dimensioni principali). Ripetuto dopo la prima misura; intervalli delle varianti con latenza simulata separati in entrambe le esecuzioni. Dettagli, latenze effettive e limiti in REVIEW-RC20.md. Non misura USB o importazione completa.
- Catalogo grande: **1/1 passato**, SQLite valido 566.362.112 byte e WAL 4.227.152 byte, cattura/rilettura/integrità/hash. Cattura sintetica da RAM 706 ms, allocazioni gestite 571.731.952 byte, picco processo 1.744.584.704 byte. Non è un tempo USB.
- Flusso oltre 4 GiB: **1/1 passato**, 4.294.967.313 byte realmente letti e sottoposti a hash verso Stream.Null, 2,35 s, working set 29 MiB. Non misura il salvataggio sul disco né il trasporto AFC.
- Ripetizione del test di copia/replay NTFS oltre 4 GiB: **non completata in questo ciclo**. Un tentativo non ha prodotto l'esito finale dopo l'interruzione della sessione; il secondo è stato fermato esplicitamente, identificando il solo processo sintetico, perché la scrittura locale procedeva molto lentamente (circa 344 MiB dopo 215 s). Nessun esito positivo attribuito. Dati di prova conservati; temporanei DeleteOnClose gestiti normalmente. Core è identico alla rc.19, dove la prova completa era passata; quel risultato precedente non viene contato come ripetizione rc.20. La causa della lentezza locale non è stata determinata.
- Packaging: **4/4 passati** (interruzione, invalido, esistente, successo). Pacchetto e download pubblico sono controllati dopo il tag con avvio autonomo e UI sintetica senza dispositivi; esiti in RELEASE-VERIFICATION.md.

I dodici nuovi test del prototipo Core scartato e le sedici regressioni filtrate erano passati, ma non appartengono al prodotto consegnato e non incrementano il totale finale. Nuove revisioni delegate non disponibili per quota; nessun fallback a pagamento. Test e revisione locale non equivalgono a collaudo hardware o certificazione.

Log finali in test-results.txt; il precedente log è conservato in test-results-rc19.txt. Risultati numerici in performance-rc20/. Di seguito resta la storia dei cicli precedenti.

---
# Verifiche 0.1.0-rc.19

Solo dati sintetici e nuove cartelle isolate; nessun telefono o file personale delle prove consultato. Versione candidata: tempi e compatibilità del trasporto da 256 KiB da confermare sul dispositivo reale.

- Suite ordinaria: 553 casi distinti verificati. Primo giro 545/553; otto fixture hard link/junction bloccate dal sandbox. Ripetuti gli stessi otto casi fuori da quel limite, tutti passati, senza ridurre i controlli o toccare file personali.
- Dopo gli ultimi dettagli dei contatori: 16/16 test rc.19 passati (13 copia/confronto/ripresa e 3 AFC).
- Due test estesi oltre 4 GiB: replay con una sola apertura sorgente e percorso ordinario, entrambi passati. Conservazione del file precedente, SHA-256 e pubblicazione verificati. Memoria osservata a fine prova circa 60/61 MiB. La fixture di replay finale è conservata; il test ordinario elimina solo il proprio payload sintetico come già previsto dal test.
- Catalogo sintetico SQLite da 566.362.112 byte con WAL da 4.227.152: cattura, doppia lettura, integrità, selezione e hash sorgenti passati. Nessuna modifica all'algoritmo di cattura.
- Benchmark con 3 ripetizioni per variante e DLL pubblica rc.18: 24 MiB di payload, vecchia copia non corrispondente, attesa simulata di 5 ms per Read da 64 KiB. Byte sorgente 48 -> 24 MiB; mediana 4,47 -> 2,84 s. Non sono tempi dell'iPhone. Prime misure con la stessa DLL accidentalmente caricata scartate e conservate; variante valida verificata tramite SHA-256.
- Build test e applicazione senza avvisi/errori. Smoke UI compatta iPhone passata e controllata visivamente: nuovo checkbox accessibile, disabilitato mentre lavora e non selezionato per default, azioni raggiungibili.
- Packaging: 4/4 scenari (compressione interrotta, ZIP invalido, archivio precedente, successo). Screenshot del confezionamento salvati nello staging nuovo per preservare quelli precedenti.

Le verifiche pertinenti sono state eseguite per incrementi: non si dichiara una seconda esecuzione completa dopo le sole aggiunte ai contatori/testi UI. La build finale e il pacchetto pubblico vengono verificati separatamente. Nessun collaudo hardware rc.19. Le revisioni indipendenti della diagnosi sono disponibili; una nuova revisione indipendente dell'implementazione non è stata completata per limite di quota, senza fallback a pagamento.

Dettagli in REVIEW-RC19.md; log del ciclo in test-results.txt. Verifiche del pacchetto riscaricato in RELEASE-VERIFICATION.md dopo il tag. Di seguito è conservata la documentazione del ciclo precedente.

---
# Verifiche 0.1.0-rc.18

Solo dati sintetici e cartelle isolate. Nessun telefono, catalogo personale, media o file delle prove aperto o modificato.

## Riproduzione

Contro la DLL pubblica immutabile rc.17, due cataloghi sintetici con un album sano e un riferimento irrisolto indipendente falliscono entrambi con lo stesso messaggio mostrato nella foto: 0/2. Il difetto riprodotto è il blocco globale, non una spiegazione delle associazioni concrete sul telefono.

## Test pertinenti eseguiti

- Build test/app senza avvisi o errori.
- Suite filtrata Apple: 109/109 passati, inclusi i primi sette nuovi casi sulle associazioni, trasporto, TLS, SQLite/WAL, catalogo e filtri.
- Importazione galleria: 12/12 passati.
- Poi aggiunti due casi mirati: verifica finale che diventa incerta e riferimenti irrisolti di album esclusi/di sistema. Suite dedicata finale: 9/9 passati, sette già compresi nei 109. In totale 123 casi distinti, non una nuova esecuzione globale della suite ordinaria.
- I nove casi verificano isolamento senza ampliamento, album sconosciuti, elementi mancanti espliciti, accesso soltanto agli originali selezionati, rifiuto prima di aprire originali incerti, esclusioni note, duplicati, conteggi globali e copia già verificata conservata quando la lettura finale diventa non verificabile.
- Due smoke UI locali: schermata principale e selettore con album integro e bloccato. Provata abilitazione del pulsante sulle righe integre e rifiuto PerformClick sulla riga bloccata; avviso e conteggi visibili, filtro per nome e scorrimento orizzontale restano disponibili. Schermata controllata visivamente.

Log in test-results.txt. La suite generale 528/528 e la prova sintetica grande della rc.17 non sono state ripetute: la cattura e i suoi limiti non sono cambiati. Confezionamento e verifica del download pubblico sono registrati dopo il tag in RELEASE-VERIFICATION.md.

## Limiti

La foto rc.17 dimostra che questa esecuzione è arrivata al controllo delle associazioni dopo cattura e integrità SQLite. Non rivela quali righe siano irrisolte né la causa. Gli album interessati e la galleria completa restano bloccati: non si eliminano associazioni per dichiarare successo. La prova con il dispositivo deve ancora confermare lista album, selezionabilità di quello desiderato, conteggi, originali locali e copia. La correzione non garantisce che tutti gli album diventino utilizzabili.
