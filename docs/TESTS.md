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
