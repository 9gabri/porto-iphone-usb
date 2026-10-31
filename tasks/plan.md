# Porto — candidata Windows USB

Applicazione locale Windows 11 x64, C#/.NET 10, WinForms, WPD sola lettura.
Nessun accesso ai dati dell'utente durante lo sviluppo. Nessun servizio a pagamento.

## Decisioni
- Sorgente WPD isolata in un processo: timeout e pausa possono arrestare un driver bloccato senza compromettere catalogo e interfaccia.
- SQLite transazionale di Windows (winsqlite3), stato nella destinazione NTFS; lock esclusivo per evitare importazioni concorrenti.
- Copia a buffer limitato, SHA-256 in streaming, rilettura locale, verifica opzionale di seconda lettura USB.
- Identificatori USB non considerati prova di identità. Riconciliazione tramite hash completo e molteplicità per preservare duplicati preesistenti.
- File temporaneo nello stesso volume; intent di pubblicazione salvato prima di rename esclusivo; recupero verificato dopo crash.
- Classificazione conservativa di Live Photo e versioni modificate; ambiguità esplicite, niente falsa completezza.
- Distribuzione portabile con runtime, repository pubblico, release e GitHub Pages. Niente Actions a pagamento, build locale.

## Sequenza
1. Contratto sorgente, catalogo e copia verificata con test sintetici.
2. Recupero, collisioni, identificatori variabili e spazio insufficiente.
3. Adattatore USB WPD reale e protocollo worker con timeout.
4. UI italiana, report e informazioni sulla copertura.
5. Prove grandi, crash e revisione indipendente.
6. Build autonoma, avvio UI senza USB, pacchetto e pubblicazione verificata.

## Rischi e limiti da misurare
- WPD non certifica originalità né libreria Foto completa. iCloud offline e risorse modificate possono non essere riconoscibili.
- Nessun hardware autorizzato per test; dichiarare esplicitamente compatibilità iPhone/iOS non verificata.
- Fonte con metadata insufficienti: riportare ambigui, non importare versioni note come derivate.
- Rinominare e aggiornare SQLite sono operazioni separate: testare ogni confine.
- ARM, exFAT, rete non supportati nella candidata; blocco conservativo.

L'utente ha già autorizzato tutte le fasi reversibili e la pubblicazione del nuovo progetto; nessuna approvazione intermedia ordinaria.

## Revisione rc.2

Mandato: `HARDENING-PROMPT.md`. Conservare tutte le protezioni esistenti.
La prova del cliente dimostra avvio UI e lista vuota, non dimostra che Windows
riconoscesse l'iPhone. Il problema hardware resta non riproducibile senza dispositivo.

Sequenza verificabile:
1. Baseline e audit indipendenti USB/core; esclusione allegati privati dal repository.
2. Correzioni discovery/heartbeat con fixture sintetiche e errori diagnostici.
3. Report pendenti, recupero e copia oltre errori isolati, controllo finale inventario.
4. Coordinatore con tentativi limitati e cancellabili, UI di riconnessione e DPI.
5. Regressioni, stress, mutazione, revisione finale; build e pubblicazione rc.2.

Non inserire download iCloud, upload, scritture al telefono, driver, privilegi,
pulizia automatica dei temporanei, deduplicazione per soli metadati o retry infiniti.
L'inventario finale confrontato conferma solo stabilità della vista USB, non Foto.
