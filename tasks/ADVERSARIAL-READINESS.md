# Revisione avversaria di Porto

Richiesta: chiudere analisi, correzioni, prove e consegna in un solo ciclo autonomo,
senza costi, accesso a dati personali o cambiamenti alle protezioni.

## Contratto e ipotesi da smentire

- Una copia fisica non deve far scomparire due video distinti già osservati,
  anche dopo pause, ID variabili, riapparizioni e nuovi inventari.
- Una connessione USB persa invalida gli ID del suo inventario. Pausa e timeout
  devono coprire anche l'invio di comandi al worker.
- Scritture locali limitate ai file di proprietà dell'app; niente sovrascritture
  attraverso link o adozione di cataloghi estranei.
- Report finali pubblicati soltanto dopo scrittura e flush completi.
- La classificazione riconosce la struttura del contenitore, senza promettere
  decodifica o riproducibilità. Integrità significa corrispondenza dei byte ricevuti.
- Il pacchetto deve contenere soltanto gli output della build corrente.

## Metodo

Baseline d9aea26: 70/70 prove ordinarie superate. Tre revisori con contesto nuovo
esaminano obblighi di copia, USB/interfaccia e storage/parser/pacchetto. Nessuna
chiamata a modelli o servizi esterni: cross-model esterno omesso nel lavoro autonomo.
Ogni finding viene valutato sul contratto, riprodotto quando possibile e coperto
da regressione. Implementazione in gruppi di file separati; build e test seriali.
Revisione finale sulle modifiche, con limite di tre cicli per artefatto.

## Criterio di consegna

Nessun difetto bloccante confermato lasciato aperto, suite ordinaria e prove estese,
build senza warning, avvio pacchetto senza USB, tag e manifesto coerenti, download
pubblico identico. Conservare le release precedenti e documentare il residuo.
La prova con iPhone fisico, iOS, cavo e driver della cliente resta un requisito
per confermare compatibilità; non trasformare una candidata in certificazione.
