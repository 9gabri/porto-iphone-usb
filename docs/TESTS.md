# Prove della candidata 0.1.0-rc.8

Windows 11 x64, SDK .NET 10.0.403 e runtime 10.0.14. Cartelle NTFS isolate,
dati sintetici e processi di prova. Nessun telefono o video personale letto.

## Risultati

**322 casi superati nella suite completa: 318 ordinari e quattro estesi.**
Dopo un ultimo aggiustamento circoscritto alla classificazione (una risorsa
esplicitamente video con estensione immagine non deve abbinarsi a se stessa),
sono state rieseguite sul codice finale **48 verifiche pertinenti**, tutte passate:
22 parser/classificazione, 20 copertura e recupero, sei integrazioni correlate.
Non sono 370 casi distinti e non viene dichiarato un unico run completo successivo
all'ultimo aggiustamento. Nessun difetto o fallimento finale ignorato.

Un tentativo preliminare è stato interrotto: ha rivelato fixture obsolete sui
marcatori testuali, una verifica di versione rimasta rc.7 e creazione di hard link
sintetici impedita dalla sandbox. Le fixture sono state rese realmente malformate
mantenendo le asserzioni; versione corretta e suite finale eseguita con i permessi
necessari esclusivamente per i test isolati.

Build senza avvisi/errori. Quattro prove di packaging separate superate.
[Output](test-results.txt) e [revisione indipendente](REVIEW-RC8.md).
Riproduzione: `scripts/dev.ps1 test -Large`; senza `-Large` 318 casi ordinari.

## Correzioni e regressioni

- Tutti i candidati video letti, foto e AAE mai aperti; hash e byte confrontati.
- Parser strutturato QuickTime: gerarchia, namespace, chiavi, ID locali, padding,
  troncamento, marker opachi, identificatore isolato, limiti e cancellazione.
- Copie precedenti integre riutilizzate senza spostarle; classificazione aggiuntiva
  separata e numero di copie distinte mantenuto per contenuti identici.
- Quarantined validi riclassificati tramite nuova copia, vecchio .part invariato.
  Morte reale del processo a intento, flush, Ready, rename e Complete in entrambe
  le cartelle; collisione con file estraneo conservato.
- Vecchio Complete ora non valido resta ambiguo e intatto.
- Sei blocchi Open/Read/disconnessioni: file sani successivi raggiunti e copiati.
  ID variabili, nomi e dimensioni uguali; continuazioni limitate oltre tre passaggi.
  Cancellazione, inventario incompleto e disconnessione permanente senza loop.
- Report corrente/storico, nuovi elementi finali, copie precedenti mancanti e
  vecchi errori senza prova: nessun debito nascosto per ottenere un esito verde.

Mantenute le prove preesistenti su SHA-256, doppia lettura divergente, troncamenti,
letture native corte/S_FALSE, dimensioni discordanti, cleanup, frame IPC, timeout,
identificatori variabili, molteplicità minima storica, corruzione, spazio e permessi,
lock, schema/journal/cataloghi interrotti, percorsi ostili, NUL, hard link,
symlink/junction e conservazione dei file estranei.

Due revisori indipendenti hanno controllato reciprocamente USB/media e i cambi
engine/storage. In copia isolata, ripristinando il limite rigido di tre passaggi:
build riuscita, test dei sei ostacoli con ID variabili fallito 0/1 per risorse sane
non raggiunte. Lo stesso test passa nel codice consegnato. Nessuna mutazione nel
pacchetto; nessun bloccante confermato rimasto irrisolto nel ciclo.

## Carico

50.000 elementi, 250 GB logici senza scrivere quel volume, 2.000 copie fisiche
seguite da rilettura senza duplicati; stream virtuale e file NTFS oltre 4 GiB.
Misure del run, non promesse di prestazioni USB:

```text
LARGE stream: 4294967313 bytes, 3,07s, working set 68 MiB
LARGE inventory: 50000 items, 250 GB logical, 8,87s, working set 226 MiB
STRESS full loop: 2000 physical files twice verified, 50000 entries, 381,19s, working set 203 MiB
LARGE NTFS: 4294971392 bytes written and reread, 25,30s, working set 117 MiB
```

## Interfaccia, pacchetto e limiti

Quattro stati UI sintetici: normale, recupero, pausa e incompleto, più controllo
visivo del riepilogo ampio. Pulsanti accessibili, scorrimento disponibile, conteggi
distinti, barra azzerata nell'esito incompleto e callback tardivo innocuo. Nessuna
ricerca di telefono negli smoke. Non certifica ogni DPI o interazione manuale.
Quattro test packaging: compressione interrotta, ZIP invalido, archivio precedente
conservato e successo. Download pubblico, versione, manifest, checksum e sei smoke
sono registrati dopo la pubblicazione in [RELEASE-VERIFICATION.md](RELEASE-VERIFICATION.md).

Da confermare: recupero degli errori sullo specifico iPhone e corrispondenza con
il totale dell'album Video. Eliminazioni, originali solo iCloud, componenti Live
Photo e varianti impediscono di dedurre un totale Foto dai soli file WPD.
Il parser non decodifica i fotogrammi. Nessuna prova fisica da 250 GB.
Windows ARM, rete ed exFAT non supportati. Candidata non firmata; nessuna riduzione
delle protezioni Windows richiesta. Nessuna spesa, servizio o driver aggiunto.
