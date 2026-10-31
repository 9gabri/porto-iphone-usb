# Fonti primarie consultate

Consultazione: 30 ottobre 2026. Nessun codice Rollport, AFC/libimobiledevice o DLL Apple non ufficiale riutilizzato.

Verifiche aggiuntive rc.4, 31 ottobre 2026:
- [Microsoft errori WPD](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/error-constants), [errori COM/RPC](https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-3) e [errori COM generali](https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-1): distinzione dei codici che richiedono una nuova sessione dagli errori del singolo elemento.
- [Microsoft CoCreateInstance](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-cocreateinstance): diagnostica del componente COM non registrato.
- [SQLite schema table](https://www.sqlite.org/schematab.html): definizioni SQL delle strutture riconosciute, oltre ai metadati delle colonne e degli indici.

Verifiche aggiuntive rc.3, 31 ottobre 2026:
- [Microsoft BY_HANDLE_FILE_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/ns-fileapi-by_handle_file_information) e [GetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle): numero di collegamenti NTFS prima di scritture sui file di stato.
- [SQLite PRAGMA](https://www.sqlite.org/pragma.html): application_id, user_version e schema; sono controlli di compatibilità, non firme di autenticità.
- [SQLite formato journal](https://sqlite.org/fileformat2.html#the_rollback_journal), [hot journal](https://www.sqlite.org/lockingv3.html#dealing_with_hot_journals) e [pager ufficiale](https://github.com/sqlite/sqlite/blob/master/src/pager.c): validazione prudenziale e recupero su copia locale prima dell'apertura in scrittura.

- [Apple: trasferire foto e video su PC](https://support.apple.com/en-us/120267): collegamento, autorizzazione, Dispositivi Apple e necessità di originali locali per l'importazione USB. Non documenta una garanzia di inventario Foto completo via WPD.
- [Apple: HEIF/HEVC e Mantieni originali](https://support.apple.com/en-ie/116944): il trasferimento USB può convertire il formato se non si sceglie Keep Originals. Ciò non è una certificazione indipendente di originale non modificato.
- [Apple: metadati Live Photo](https://developer.apple.com/documentation/avfoundation/avcapturephotosettings/livephotomoviemetadata?language=objc): associazione movie/foto tramite content identifier. La candidata tratta gli indizi incompleti come ambigui.
- [Microsoft: apertura dispositivo WPD](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevice-open): richiesta esplicita GENERIC_READ.
- [Microsoft: GetStream](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-getstream): modalità STGM_READ e WPD_RESOURCE_DEFAULT.
- [Microsoft: mapping degli identificatori](https://learn.microsoft.com/en-us/windows-hardware/drivers/portable/mapping-object-identifiers): ID di oggetto validi per sessione, diverse implementazioni dei PUID. Non assumiamo affidabilità specifica iPhone senza prove hardware.
- [Microsoft: panoramica driver WPD](https://learn.microsoft.com/en-us/windows-hardware/drivers/portable/wpd-drivers-overview): infrastruttura per PTP/MTP e protocolli diversi; non un normale disco.
- [Microsoft: SQLite Windows](https://learn.microsoft.com/en-us/windows/apps/develop/data-access/sqlite-data-access): versione SQLite di sistema mantenuta da Microsoft. Wrapper P/Invoke locale senza pacchetti applicativi.
- [Microsoft: pubblicazione .NET](https://learn.microsoft.com/en-us/dotnet/core/deploying/): pacchetto self-contained, runtime incluso.
- [Header Microsoft WinSDK](https://github.com/microsoft/win32metadata/tree/main/generation/WinSDK/RecompiledIdlHeaders/um): PortableDeviceApi.h, PortableDeviceTypes.h, PortableDevice.h per GUID, vtable e firme ABI. Copie di studio solo nella cartella strumenti esclusa dal repository.
- [GitHub: piani](https://docs.github.com/en/get-started/learning-about-github/githubs-plans), [Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/what-is-github-pages), [distribuzione binari](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github): repository pubblico e Pages disponibili gratuitamente; binari distribuiti come release, senza Git LFS o runner a pagamento.

## Inferenze e aspetti non dimostrati

Verifiche aggiuntive rc.2:
- [Microsoft GetDevices](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicemanager-getdevices): S_FALSE indica buffer troncato; count indica gli elementi scritti; CoTaskMemFree per gli identificatori.
- [Microsoft IEnumPortableDeviceObjectIDs.Next](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-ienumportabledeviceobjectids-next): elaborare il batch finale e poi terminare su S_FALSE.
- [Microsoft SetThreadExecutionState](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate): richiesta ES_CONTINUOUS | ES_SYSTEM_REQUIRED per sospensione inattiva, ripristinata alla fine sullo stesso thread. Non impedisce sospensione manuale o blocco schermo.
- [Apple accessori USB](https://support.apple.com/en-in/111806): sblocco prima del collegamento e riconnessione se necessario; non viene modificata l'impostazione di sicurezza degli accessori.

WPD è la scelta minima per utilizzare lo stack già presente su Windows. La presenza di un'interfaccia e la correttezza ABI non dimostrano compatibilità con uno specifico iPhone o iOS. Originalità, associazione a Live Photo, risorse modificate e quantità degli originali solo cloud non sono certificabili con i soli dati generici usati qui. Per questo la UI non dichiara mai la libreria intera verificata.
