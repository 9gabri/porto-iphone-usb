# rc.7: recupero dopo letture fallite

## Evidenza e ipotesi

Report e inventari autorizzati del collaudo rc.6 confermano una catena di
letture fallite dopo un primo errore. Alcune lunghezze ricevute coincidono con
la dimensione dichiarata della risorsa precedente. È un indizio di stato non
affidabile della sessione, non una prova dell’identità dei byte o della causa
fisica. Nessun telefono o video personale aperto durante lo sviluppo.

Il modello sintetico rc.6 assumeva che Cancel riuscito ripristinasse lo stato.
La nuova riproduzione usa processi figli e pipe reali: l’errore lascia il worker
alterato anche dopo cleanup riuscito. Le risorse sane successive falliscono
finché quel processo non viene sostituito. La baseline rc.6 supera solo 1/14
nuove prove: passa il controllo senza errori, falliscono 13 regressioni.

## Correzione e limiti

ReconnectingUsbSource implementa il segnale di invalidazione dell’engine.
Prima della prossima lettura sostituisce il proprio worker, si collega allo
stesso dispositivo e verifica un inventario completo identico in ogni campo
e molteplicità. Non deduce identità dai soli metadati: ogni contenuto richiede
ancora lettura completa, hash e verifica locale. Se il nuovo inventario cambia,
la risorsa non ancora aperta resta pendente e l’engine riparte con uno nuovo.
Gli errori di un singolo elemento non richiedono una selezione manuale.

La sostituzione avviene nel processo padre: non attende un comando COM di
cleanup del vecchio worker. Il worker vecchio posseduto dall’app viene terminato.
Questa operazione non riguarda processi di altre applicazioni. Nessun comando
scrive sul telefono. Cataloghi, destinazioni, controlli di integrità e criteri
di classificazione restano invariati. Nessun driver, servizio o costo aggiunto.

Con inventario stabile il recupero avviene una volta per lettura rifiutata,
poi il passaggio prosegue; nessun ciclo illimitato sullo stesso video.
Inventari che cambiano a ogni collegamento, timeout e disconnessioni possono
ancora esaurire i tre passaggi esterni lasciando file non raggiunti. L’esito
resta incompleto. Risorse ambigue e originali solo iCloud non sono recuperati
per supposizione. La differenza rispetto al conteggio Foto resta un limite.

## Verifica avversaria

16 nuove prove IPC: errore di apertura, lettura troppo lunga, EOF corto,
disaccordo di hash a dimensione uguale, cinque errori seguiti da tre copie sane,
inventario riordinato, ID rimappati, altri metadati cambiati, inventario incompleto,
molteplicità modificata, nuovi elementi nel controllo finale, percorso sano,
cancellazione, fallimento della creazione del worker, Dispose tardivo del vecchio
stream e osservazione richiesta assente dall’inventario precedente.

Le copie sane sono confrontate con i byte sintetici attesi, non solo contate.
Tutte le 16 prove passano. Una mutazione isolata confronta il vecchio inventario
con se stesso invece che con quello nuovo: la regressione sui metadati fallisce
0/1 dopo compilazione riuscita. I sorgenti di consegna non sono mutati.

Revisione indipendente su recupero, identità, cancellazione e sicurezza:
nessun difetto bloccante confermato nel diff; limite delle riconnessioni con
ID sempre diversi esplicitamente mantenuto. Risultati della suite finale in
[TESTS.md](TESTS.md). Prova fisica sulla nuova versione ancora necessaria.
