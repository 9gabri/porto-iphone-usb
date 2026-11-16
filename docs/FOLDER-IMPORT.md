# Importazione da cartella — rc.9

Questa modalità separa la scelta dei video dalla copia verificata. La selezione dei soli video viene fatta in Foto sull’iPhone; Porto non ricostruisce quell’informazione dagli indizi USB.

## Preparare l’esportazione

Apple documenta la [raccolta per tipo di contenuto](https://support.apple.com/en-mide/guide/iphone/iph8530ff6a2/ios) e l’[esportazione di originali non modificati verso una posizione nell’app File](https://support.apple.com/en-ca/111762). Scegliere la raccolta **Video**, selezionare gli elementi desiderati insieme ed esportarne gli originali non modificati. Non esportare tutta la libreria o una DCIM per cercare poi di escludere le Live Photo in base al nome.

L’esportazione deve arrivare in una cartella locale separata sul PC, ad esempio `C:\Video\Esportazione da Foto`. Per usare solo iPhone e Windows senza acquisti, l’app File può accedere a un server/cartella condivisa: [guida Apple](https://support.apple.com/en-ie/guide/iphone/iphe9aff429a/ios). La disponibilità della destinazione condivisa nel flusso concreto di esportazione e il trasferimento completo devono essere verificati sul telefono. Non sono stati provati in questo ciclo.

Porto non crea condivisioni. Configurare una condivisione amplia l’accesso alla cartella: richiede consenso specifico, una rete fidata, accesso autenticato limitato alla sola cartella di esportazione e nessuna riduzione delle protezioni. Non condividere l’intero archivio o la cartella personale; non attivare accesso ospite o disabilitare password/antivirus/firewall. Nessun servizio a pagamento o componente aggiuntivo è necessario a Porto. Non acquistare dispositivi per questa prova.

Attendere il completamento dell’esportazione prima di avviare Porto; non aggiungere o modificare file mentre importa. Conservare i file esportati fino al risultato verificato. Se gli originali sono solo su iCloud, la loro disponibilità e lo spazio necessario vanno risolti nell’esportazione, non da Porto. Non cancellare nulla dall’iPhone sulla base del solo esito della cartella.

## Impostare Porto

- **Origine**: nuova cartella dei video esportati. MOV, MP4 e M4V possono stare anche in sottocartelle.
- **Archivio**: cartella già usata con Porto, che contiene `Video Porto`, `.porto`, `Report Porto` ed eventualmente `Risorse video aggiuntive`.
- Le cartelle non possono coincidere o contenersi, né usare collegamenti. Locale NTFS; destinazione fino a 120 caratteri.
- **Avvia importazione** confronta e copia. **Verifica approfondita** rilegge anche la sorgente. **Pausa** interrompe in modo controllato; **Riprendi** fa un inventario nuovo e verifica i contenuti.

Il confronto legge i file, quindi può richiedere tempo anche senza nuove copie. I byte elaborati per verifica non sono indicati come nuovi byte copiati. File non leggibili nell’archivio impediscono di dichiarare un confronto completo; il report esplicita l’errore.

## Leggere l’esito

Ogni file selezionato è associato nel report al percorso della copia verificata, nuova o già presente. Le copie riutilizzate possono restare in cartelle diverse: Porto non le sposta e non crea una cartella duplicata di tutti i video.

La sezione **Confronto delle copie precedenti** distingue riutilizzate, conservate non riutilizzate e da verificare. Una copia non riutilizzata potrebbe essere un duplicato ulteriore con lo stesso contenuto, una versione diversa, una risorsa Live Photo, un video non esportato oppure un file il cui confronto non è terminato. Non prova l’assenza del contenuto dalla selezione e non autorizza la cancellazione.

`SelectedFolderVerified` nel JSON verifica solo il passaggio attuale: almeno un candidato supportato, tutti risolti, nessun errore corrente, inventari completi e stabili. Le osservazioni USB storiche restano separate: una selezione ridotta non cancella debiti o errori precedenti. Il report e l’interfaccia non chiamano completata l’intera libreria.

Un video convertito e il suo originale hanno byte differenti; possono essere conservati come copie distinte. Non è garantita l’assenza di duplicati visivi. Porto riutilizza una copia identica verificata disponibile per quell’elemento invece di crearne un’altra, rispettando il numero dei file distinti selezionati.

## Prova ancora necessaria

Confermare il percorso di esportazione sullo specifico iPhone, il completamento di tutti gli elementi selezionati (inclusi eventuali originali cloud), e il confronto con l’archivio reale. Non è stato letto alcun video personale in questo ciclo. Il numero finale deriva dall’esportazione e dal confronto, non da un totale impostato nel codice.
