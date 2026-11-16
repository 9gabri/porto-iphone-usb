# Dipendenze e licenze

- **Porto 0.1.0-rc.11:** codice originale del progetto, MIT, file LICENSE.
- **BouncyCastle.Cryptography 2.7.0:** TLS gestito del collegamento Apple, licenza MIT con componente Bzip2 Apache-2.0. Avvisi originali e licenze inclusi nella cartella `licenses`. Pacchetto ufficiale NuGet, versione e contentHash fissati in `src/Porto.Apple/packages.lock.json`; repository bcgit/bc-csharp, commit upstream `4007498b13582d90ee1eda5d9920c324428b98b3`. Nessun servizio, account o costo a consumo: è una libreria locale distribuita nell'app.
- **.NET Runtime e Windows Desktop Runtime 10.0.14 x64:** Microsoft, distribuiti nel pacchetto autonomo; licenze e avvisi nei file LICENSE-DOTNET.txt, THIRD-PARTY-DOTNET.txt e LICENSE-WINDOWSDESKTOP.txt. SDK di build 10.0.403 non incluso.
- **Windows WPD, COM, Win32, winsqlite3.dll:** componenti del sistema Windows, non ridistribuiti e non installati da Porto. SQLite è public domain; la copia Windows è aggiornata tramite Windows.
- **Dispositivi Apple / supporto USB Apple:** prerequisito separato per la modalità catalogo iPhone, soggetto alle condizioni Apple/Microsoft Store. Nessun componente Apple distribuito o installato. Porto usa solo un servizio locale già disponibile e un abbinamento esistente.

Il protocollo USB Apple e il lettore dei metadati sono implementazioni indipendenti di Porto. Non sono incorporati codice o binari GPL di iPhone Manager, pymobiledevice3, libimobiledevice o ifuse. Le fonti pubbliche sono state consultate per i fatti del protocollo e la fattibilità; non costituiscono una garanzia di compatibilità Apple.

I parser locali verificano contenitori e metadati senza conversione; PNG include decompressione limitata delle scanline. NuGet è usato in build per i runtime Microsoft e la libreria sopra indicata. L'app non contiene acquisti, API fatturabili, telemetria o upload di media. Il sito statico segue le normali informative di GitHub Pages.

Fonti: https://www.nuget.org/packages/BouncyCastle.Cryptography/2.7.0 · https://github.com/bcgit/bc-csharp · https://www.apache.org/licenses/LICENSE-2.0
