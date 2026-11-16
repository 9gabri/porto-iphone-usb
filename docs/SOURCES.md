# Fonti primarie consultate

## rc.7: nessuna nuova dipendenza o API sul telefono

Il recupero sostituisce il worker dal processo padre tramite il percorso già
usato per timeout e cancellazione. Usa le stesse API WPD di sola lettura;
nessuna nuova operazione sul telefono. L'uguaglianza del nuovo inventario
è un controllo di continuità, non una prova dell'identità dei contenuti.
Le prove e i limiti osservati sono documentati in [REVIEW-RC7.md](REVIEW-RC7.md).


Consultazione: 30 ottobre 2026. Nessun codice Rollport, AFC/libimobiledevice o DLL Apple non ufficiale riutilizzato.

Verifiche aggiuntive rc.6, 15 novembre 2026:
- [Microsoft IPortableDeviceResources.Cancel](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-cancel): annulla operazioni pendenti sul relativo handle. La rc.6 usa una sola lettura alla volta e non apre risorse in scrittura. Slot 7 verificato nell'header WinSDK PortableDeviceApi.h; slot 6 Delete non utilizzato.
- [Microsoft GetStream](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-getstream): proprietà del riferimento COM e rilascio dello stream.
- [Microsoft IPortableDeviceDataStream.GetObjectID](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledevicedatastream-getobjectid): documentato dopo Commit di un oggetto scritto; non usato per autenticare letture.
- Il driver che conserva uno stato dopo l'abbandono è una riproduzione sintetica motivata dai report, non una diagnosi certa del comportamento iPhone.

Verifiche aggiuntive rc.5, 15 novembre 2026:
- [Microsoft GetResourceAttributes](https://learn.microsoft.com/en-us/windows/win32/api/portabledeviceapi/nf-portabledeviceapi-iportabledeviceresources-getresourceattributes) e [WPD_RESOURCE_DEFAULT](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/wpd-resource-default): attributi della risorsa aperta, distinti dalle proprietà dell'oggetto inventariato.
- [PortableDevice.h ufficiale](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/PortableDevice.h): WPD_RESOURCE_ATTRIBUTE_TOTAL_SIZE, GUID 1EB6F604-9278-429F-93CC-5BB8C06656B6, PID 2, tipo VT_UI8.
- [Microsoft ISequentialStream::Read](https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-isequentialstream-read): conteggio restituito e S_FALSE alla fine del flusso. Conservare gli ultimi byte senza richiedere un'altra lettura dopo EOF.
- Queste specifiche e le regressioni sintetiche dimostrano i contratti dell'implementazione; non provano che tutte le anomalie della prima prova fisica avessero la stessa causa.

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
- [Apple: metadati Live Photo](https://developer.apple.com/documentation/avfoundation/avcapturephotosettings/livephotomoviemetadata?language=objc): associazione movie/foto tramite content identifier. La rc.8 copia le risorse con indizi nelle risorse aggiuntive, senza certificarle come Live Photo.
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

## rc.8: struttura QuickTime (fonti Apple)

- https://developer.apple.com/documentation/quicktime-file-format/timed_metadata_media — traccia meta.
- https://developer.apple.com/documentation/quicktime-file-format/timed_metadata_sample_descriptions — mebx e tabella keys.
- https://developer.apple.com/documentation/quicktime-file-format/metadata_key_table_atom — una chiave può esistere senza valore scritto.
- https://developer.apple.com/documentation/quicktime-file-format/metadata_key_atom — ID locali, zero inutilizzato e FFFFFFFF riservato.
- https://developer.apple.com/documentation/quicktime-file-format/metadata_key_declaration_atom — namespace e stringa keyd.
- https://developer.apple.com/documentation/quicktime-file-format/sample_description_atom — intestazione e padding finale.
- https://developer.apple.com/documentation/avfoundation/avcapturephotosettings/livephotomoviemetadata — content.identifier associa foto e movie; la presenza del solo identificatore non certifica che la risorsa sia una Live Photo.

La classificazione aggiuntiva è una scelta conservativa di Porto, non una garanzia
Apple di corrispondenza uno-a-uno con l’album Video.


## Selezione ed esportazione da Foto — rc.9 (2026-11-16)

- https://support.apple.com/en-mide/guide/iphone/iph8530ff6a2/ios — raccolte per tipo di contenuto; selezione da Video invece dell’intera DCIM.
- https://support.apple.com/en-ca/111762 — Export Unmodified Originals e scelta della destinazione nell’app File; pagina consultata per la nuova guida.
- https://support.apple.com/en-ie/guide/iphone/iphe9aff429a/ios — connessione dell’app File a server/cartelle condivise. Riferimento della guida; in questa verifica web ha restituito HTTP 429, quindi nessuna attestazione nuova del percorso completo di esportazione verso SMB.

Queste indicazioni non costituiscono un collaudo del flusso iPhone → cartella Windows. La disponibilità della posizione condivisa nel foglio di esportazione e il completamento sul telefono restano da confermare. Porto non configura condivisioni né amplia permessi.


## rc.10 — dispositivi, album e immagini

- [Android, trasferire file con USB](https://support.google.com/android/answer/9064445): sblocco, notifica USB, Trasferimento file. Le modalità dipendono dal dispositivo.
- [Microsoft WPD overview](https://learn.microsoft.com/en-us/windows-hardware/drivers/portable/wpd-drivers-overview): protocolli e infrastruttura Windows Portable Devices.
- [Microsoft WPD content types](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/content-types) e [header ufficiale](https://raw.githubusercontent.com/microsoft/win32metadata/main/generation/WinSDK/RecompiledIdlHeaders/um/PortableDevice.h): GUID audio/document/generic file. I tipi ignoti non vengono trattati come cartelle implicitamente.
- [Apple PhotoKit collections](https://developer.apple.com/documentation/photos/phassetcollection) e [album](https://developer.apple.com/documentation/photokit/browsing-and-modifying-photo-albums): album logici nella libreria, distinti dalla gerarchia USB. Nessun componente PhotoKit installato in questa candidata.
- [ITU T.81 JPEG, copia W3C](https://www.w3.org/Graphics/JPEG/itu-t81.pdf): marker, segmenti, frame e scan.
- [W3C PNG](https://www.w3.org/TR/png-3/): chunk, CRC, zlib e scanline, limiti di lavoro imposti da Porto.
- [Nokia HEIF technical](https://nokiatech.github.io/heif/technical.html): contenitore, item primario, proprietà e immagine grid. Il parser supporta un sottoinsieme conservativo HEVC, senza certificare la decodifica.

Le fonti documentano i protocolli/formati; non sostituiscono prove di compatibilità con telefoni reali. La scelta di cartelle fisiche è un limite esplicito del prodotto; non viene affermata la lettura universale degli album delle app.

## rc.11 — catalogo Foto e navigazione per livello

Le fonti e le distinzioni fra protocolli, fatti osservati e inferenze sono raccolte in [GALLERY-IMPORT.md](GALLERY-IMPORT.md). Lo schema privato di Photos.sqlite non è una promessa di compatibilità Apple; nessuna implementazione GPL è incorporata.

- [Microsoft WPD_OBJECT_REFERENCES](https://learn.microsoft.com/en-us/windows/win32/wpd_sdk/object-properties): appartenenze dichiarate degli oggetti riferiti; nessuna inferenza da nomi cartella.
- [SQLite WAL format](https://www.sqlite.org/fileformat2.html#walformat): header, checksum, salt e commit. La validità del WAL non dimostra atomicità di una copia live.
- [SQLite deserialize](https://www.sqlite.org/c3ref/deserialize.html): database in memoria in modalità di sola lettura, senza persistenza del catalogo del telefono.
- [BouncyCastle 2.7.0 su NuGet](https://www.nuget.org/packages/BouncyCastle.Cryptography/2.7.0) e [sorgente ufficiale](https://github.com/bcgit/bc-csharp/tree/4007498b13582d90ee1eda5d9920c324428b98b3): implementazione TLS gestita, licenze e versione fissata nel progetto. Uso locale, nessuna API a consumo.

## rc.12 — JPEG con dati aggiuntivi

- [Android Motion Photo format](https://developer.android.com/media/platform/motion-photo-format): un file può contenere una immagine primaria JPEG seguita da dati video, con metadati per individuarli.
- [Android Ultra HDR image format](https://developer.android.com/media/platform/hdr-image-format): immagine primaria JPEG e immagine ausiliaria successiva.

Queste fonti dimostrano che la fine dell'immagine JPEG principale non coincide necessariamente con la fine dell'intero file. La scelta di Porto è conservativa rispetto ai byte: conserva tutta la coda opaca, senza interpretarla, decodificarla o attribuirle un formato verificato. Non afferma che qualsiasi coda sia un'estensione conforme a tali specifiche. SHA-256 riguarda l'intero file; il controllo strutturale JPEG riguarda la sola immagine primaria. Non è un controllo antimalware né una decodifica completa dei pixel.

## rc.13 — revisione del percorso iPhone

- [libimobiledevice AFC](https://github.com/libimobiledevice/libimobiledevice/blob/master/src/afc.c): operazioni, modalità di apertura, framing e status. Confronto del protocollo; codice non incorporato.
- [libusbmuxd](https://github.com/libimobiledevice/libusbmuxd/blob/master/src/libusbmuxd.c): messaggi di ricerca, abbinamento e connessione, endian della porta.
- [Lockdown](https://github.com/libimobiledevice/libimobiledevice/blob/master/src/lockdown.c) e [certificati di abbinamento](https://github.com/libimobiledevice/libimobiledevice/blob/master/common/userpref.c): avvio della sessione/servizio e distinzione fra certificato e firma dell'handshake. Non è una promessa Apple di compatibilità.
- [SQLite rollback journal](https://sqlite.org/fileformat2.html#rollbackjournal): intestazione e modalità PERSIST. La nuova prova positiva usa un journal prodotto da SQLite; non deriva soltanto da una fixture modellata sul parser.
- [OSXPhotos, sorgente del lettore](https://github.com/RhetTbull/osxphotos/blob/main/osxphotos/photosdb/photosdb.py) e [ricerca dell'autore dei parser iLEAPP](https://theforensicscooter.com/2024/05/18/ileapp-parsers-photos-sqlite-queries/): esistenza di stati di visibilità e membri di raffiche non visualizzabili. Sono evidenze di schemi osservati, non una specifica pubblica Apple valida per ogni iOS. Porto non attribuisce significati universali ai valori nonzero: li mantiene incerti, senza saltare righe o copiarle implicitamente.
- [Apple: importazione su PC](https://support.apple.com/en-la/120267), consultata il 16 novembre 2026: componenti Apple, sblocco/autorizzazione e necessità di originali locali per USB. La guida ufficiale riguarda il flusso Apple/Microsoft, non certifica l'implementazione Porto. Nessuna installazione eseguita durante la revisione.

Scelte progettuali Porto: conservare le incertezze, non dichiarare completa una selezione ridotta fra tentativi automatici, ammettere un journal PERSIST solo con intestazione interamente azzerata e stabile, e mantenere il contratto TLS esistente. Fonti e test sintetici non sostituiscono la prova con l'iPhone della cliente.