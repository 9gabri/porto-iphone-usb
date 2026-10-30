# Dipendenze e licenze

- **Porto 0.1.0-rc.1:** codice originale del progetto, MIT, file LICENSE.
- **.NET Runtime e Windows Desktop Runtime 10.0.14 x64:** Microsoft, distribuiti nel pacchetto self-contained; MIT e avvisi di terze parti del runtime nei file `LICENSE-DOTNET.txt`, `THIRD-PARTY-DOTNET.txt` e avvisi inclusi dal publish. SDK di build 10.0.403, non incluso nell'archivio dell'app.
- **Windows WPD, COM, Win32, winsqlite3.dll:** componenti del sistema Windows, non ridistribuiti e non installati da Porto. SQLite è public domain; la copia Windows è aggiornata tramite Windows.
- **Dispositivi Apple / supporto USB Apple:** eventuale prerequisito ufficiale separato, soggetto alle condizioni Apple/Microsoft Store. Nessun componente Apple distribuito o installato da Porto.

Nessun pacchetto NuGet applicativo, servizio cloud, SDK di analisi, AFC/libimobiledevice, decoder multimediale o codice Rollport. Il parser locale legge soltanto la struttura contenitore. NuGet viene usato in build per ottenere i runtime Microsoft necessari alla distribuzione.

L'app non contiene codice per eseguire acquisti, consumare API fatturabili o caricare video online. Il sito statico è ospitato su GitHub Pages; si applicano le normali informative di GitHub ai visitatori del sito.
