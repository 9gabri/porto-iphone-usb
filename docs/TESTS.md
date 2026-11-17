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
