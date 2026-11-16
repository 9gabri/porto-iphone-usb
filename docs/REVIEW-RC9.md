# Revisione della candidata rc.9 — 2026-11-16

## Ambito e metodo

Importazione da cartella selezionata dall’utente, confronto con archivio precedente, nessuna cancellazione/sovrascrittura dei media. Una collaborazione circoscritta ha implementato il source adapter con test; un secondo revisore indipendente, in sola lettura e senza accesso a media personali, ha esaminato engine, cataloghi, molteplicità, report e interfaccia in tre cicli. Nessun nuovo servizio o API a pagamento, driver o modifica delle protezioni.

## Controesempi affrontati

- Debito USB storico di tre copie identiche, una copia fisica superstite, selezione attuale di un solo file: riuso della superstite senza copia ridondante; debito storico conservato e nessun retry automatico della selezione.
- Due selezionati identici, una sola copia precedente: un riuso e una copia nuova, poi due riusi nel passaggio successivo.
- Copia precedente con indizi di Live Photo nella cartella aggiuntiva, corrispondente a un video della nuova selezione: riuso senza spostarla; nessuna esclusione dal confronto per l’indizio.
- Catalogo mancante: confronto anche dei file non registrati. I percorsi estranei non possono essere scritti nel catalogo né diventare bersagli di sostituzione.
- File estraneo scomparso dopo inventario: non viene tentata una mutazione del catalogo con ID esterno. Confronto dei file grandi con nome, fase e byte letti visibili.
- Copia danneggiata: conservata, nuova copia con altro nome, difetto storico esplicito. Anche i file pubblicati Replaced/Abandoned rimangono elencati nei confronti successivi.
- Inventario finale cambiato e doppia lettura discordante: selezione non verificata. Una corrispondenza della prima lettura non certifica un riuso se la seconda diverge.
- AVI/MKV e altri contenitori comuni non supportati: restano ambigui e impediscono una selezione verificata; gli altri video proseguono.
- Radici sovrapposte, path forgiati, root scomparsa, file sostituito con stessa lunghezza/data, link/hardlink, permessi e cancellazione: test del source adapter.

## Esito della revisione

Nessun difetto bloccante confermato rimasto nel diff esaminato. Non è una certificazione o una prova di assenza di bug. I risultati effettivi dei test e gli eventuali limiti della loro sequenza sono in [TESTS.md](TESTS.md).

`ReusedForSelection` significa assegnato a un elemento nel passaggio corrente, non “unico file con questo contenuto”. Una seconda copia fisica identica può restare non riutilizzata pur avendo contenuto presente nella selezione. Il testo evita di chiamarla Live Photo o assente dalla selezione.

## Limiti residui

Non eseguite: esportazione reale da Foto, scelta della posizione SMB sullo specifico iPhone, download di originali cloud, confronto con l’archivio personale. Non letto alcun telefono o media personale. La verifica riguarda i byte della cartella e il suo inventario; non la completezza dell’esportazione o l’appartenenza PhotoKit. Il filesystem e la libreria non sono snapshot atomici; cambiamenti successivi restano possibili. Versioni convertite/modificate con byte differenti possono essere conservate come file distinti.
