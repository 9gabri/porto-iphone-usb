# Confronto sintetico rc.18 / candidata

Solo nuove cartelle sotto `artifacts`, mai archivi personali. Richiede SDK/runtime .NET già presenti. Nessuna dipendenza aggiuntiva. Il programma crea MOV sintetici strutturali, non contenuti audiovisivi.

Compilare `PerfCompare.csproj` indicando `-p:CoreDll=<percorso assoluto Porto.Core.dll rc.18>` e un output nuovo. Copiare quell'output in due directory nuove (`before-bin`, `after-bin`); sostituire **soltanto la DLL Core nella nuova after-bin** con la candidata. Verificare gli SHA-256 prima di eseguire: il controllo dei byte effettivamente caricati è anche nel JSON del programma. Non affidarsi a un rebuild incrementale cambiando solo HintPath: la cache MSBuild può ricopiare la stessa assembly.

Eseguire prima `before-bin/PerfCompare.exe <nuova-directory/perf-rc19> before`, poi `after-bin/PerfCompare.exe <stessa-directory/perf-rc19> after`. La directory di risultati deve terminare con `perf-rc19`. Il programma rifiuta report e archivi già esistenti. Verifica i byte dei tre nuovi file e che il precedente sia invariato. Conservare output e directory.

Configurazione: 3 file da 8 MiB; copia precedente non corrispondente da 32 KiB; tre ripetizioni per variante; sorgente RAM con richieste massime di 64 KiB e attesa attiva artificiale di 5 ms per lettura non vuota. Il tempo di attesa effettivo è registrato. Destinazione NTFS, ImportEngine reale, verifica completa e report. Cambia la DLL Core, non il trasporto Apple: questo confronto isola il riuso locale della prima lettura. Non rappresenta USB, iPhone o catalogo Foto reali.

Risultati verificati del ciclo rc.19: `artifacts/rc19-verified/perf-rc19/{before,after}.json`. Le prime prove in `artifacts/perf-rc19` sono conservate ma scartate: entrambe avevano caricato la DLL rc.18, come dimostrato dal relativo SHA-256. Misure autorevoli e limiti in `docs/REVIEW-RC19.md`.
