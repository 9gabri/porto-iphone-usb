# Revisione rc.2

Mandato operativo in `tasks/HARDENING-PROMPT.md`. Sviluppo e prove solo con dati
generati nel progetto; nessuna enumerazione, connessione o lettura di iPhone reali.

## Difetti riprodotti e correzioni

| Difetto | Correzione | Evidenza |
|---|---|---|
| GetDevices S_FALSE accettato come elenco completo | Retry limitato e diagnosi | Vtable nativa sintetica, cleanup anche su HRESULT negativo |
| 64 letture di metadati senza heartbeat | Progressi per oggetto, emissione limitata | Eventi tra letture native simulate e timeout reale di processo |
| Primo video illeggibile arresta il lotto | Continuazione per file e stop su disconnessione | Tre video illeggibili seguiti da due sani |
| Candidati mai raggiunti spariscono alla ripresa | Registro persistente Pending | Nuovo inventario vuoto dopo errore |
| Copia di A reclamata da B cancella debito A | Aggiorna associazione fisica prima di riconciliare | Duplicati identici, pausa e inventario ridotto |
| Nuovo `.part` per ogni interruzione | Confronto prefisso e append | Tre tentativi, un solo payload, file estraneo intatto |
| Stessa dimensione causa letture inutili | Solo copie ancora reclamabili | Cinque video: cinque letture normali o dieci approfondite |
| Frame anomalo lascia worker riutilizzabile | Chiusura worker su errore protocollo | JSON nullo/malformato, tipo errato e progressi malformati |
| Inventario può cambiare durante la copia | Controllo finale e nuovo passaggio | Sorgente sintetica con nuovo video a fine giro |

Due revisioni indipendenti, su core e USB, hanno esaminato correttezza, sicurezza,
prestazioni e prove. I problemi bloccanti rilevati sono stati corretti con regressioni.
Nessuna revisione statica o suite sintetica dimostra compatibilità con uno specifico
iPhone/iOS o assenza assoluta di errori.

## Confini intenzionali

- Nessuna scrittura sulla sorgente; GENERIC_READ/STGM_READ invariati.
- Nessun acquisto, firma a pagamento, nuovo servizio o accesso a dati personali.
- Nessuna cancellazione automatica: temporanei non associabili o divergenti rimangono.
- ID cambiati prima di verificare il contenuto possono lasciare un debito storico.
- I due inventari non sono uno snapshot atomico e non includono necessariamente iCloud.
- I test dei contenitori non decodificano i fotogrammi e non certificano originalità Foto.
- Richiesta contro la sospensione solo per inattività; non modifica blocco schermo o protezioni.
