# Ultima revisione avversaria richiesta dall'utente

Baseline: ea5021f, release pubblica rc.3. Suite ordinaria rieseguita: 120/120.
Il mandato è implementare e consegnare, senza costi, accesso a dati personali,
hardware reale o modifiche alle protezioni. Conservare release e file precedenti.

## Contratto da mettere alla prova

Una copia deve corrispondere ai byte ricevuti; nessuna osservazione irrisolta può
sparire dal report. Le copie storiche e i duplicati restano obblighi verificabili.
Un file che blocca il worker non deve impedire di tentare quelli sani nei passaggi
successivi. Dopo una perdita della sessione serve un inventario nuovo. Il recupero
non può spostare o modificare file estranei indicati da righe di catalogo alterate.

## Metodo e responsabilità

Tre revisori indipendenti con contesto nuovo: contabilità/copie, USB/interfaccia,
storage/sicurezza. Prima lettura e controesempi; poi regressioni sulla baseline,
correzioni circoscritte, riesame con limite di tre cicli per area. Nessun ricorso
a CLI o modelli esterni: revisione cross-model esterna omessa nel contesto autonomo.
Il coordinatore confronta ogni riscontro con il codice e orchestra build seriali.

## Piano verificabile

- [x] Baseline e revisione iniziale.
- [x] Riproduzioni sintetiche dei difetti confermati.
- [x] Correzioni con regressioni e tre cicli di riesame indipendente; verifica conclusiva del coordinatore.
- [x] Suite finale: 188 ordinari + quattro estesi sulla stessa logica, mutazione isolata e tre smoke UI.
- [x] Versione/tag/pacchetto coerenti, staging nuovo, CRC, checksum e controllo contenuti.
- [x] Pubblicazione autorizzata, download pubblico, cinque smoke e tre immagini del pacchetto scaricato.

Consegna: rc.4, sorgente 4a60abe, ZIP 51.602.369 byte. I dettagli verificabili
sono in docs/RELEASE-VERIFICATION.md. Release precedenti conservate.

La consegna resta candidata senza prova su iPhone fisico. Nessuna promessa di
zero bug, originalità Foto o copertura degli elementi presenti soltanto su iCloud.
