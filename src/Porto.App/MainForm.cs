using System.Diagnostics;
using Porto.Core;
using Porto.Usb;
using Porto.Apple;

namespace Porto.App;

public sealed class MainForm:Form
{
    private readonly ComboBox sourceMode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly TextBox sourceFolder=new(){ReadOnly=true,Dock=DockStyle.Fill,PlaceholderText="Cartella o album già esportato sul PC"};
    private readonly ComboBox mediaMode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly ComboBox usbScope=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};
    private readonly Button selectUsbFolder=Button("Scegli cartella USB");
    private readonly Label usbSelection=Label("Tutte le cartelle USB accessibili.",9);
    private UsbFolderChoice? chosenUsbFolder;
    private UsbCollectionChoice? chosenUsbCollection;
    private AppleAlbum? chosenAppleAlbum;
    private readonly AppleCatalogCache appleCatalogCache=new();
    private readonly Label instructions=Label("",10),limits=Label("",9);
    private bool FolderMode=>sourceMode.SelectedIndex==0;
    private bool AppleMode=>sourceMode.SelectedIndex==2;
    private readonly ComboBox phones=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DisplayMember="Name"};
    private readonly TextBox folder=new(){ReadOnly=true,Dock=DockStyle.Fill,PlaceholderText="Scegli dove conservare foto e video"};
    private readonly Button search=Button("Cerca dispositivi"),browse=Button("Scegli cartella"),start=Button("Avvia importazione",true),pause=Button("Pausa"),resume=Button("Riprendi"),reportButton=Button("Apri report");
    private readonly CheckBox deep=new(){Text="Verifica approfondita: rileggi anche la sorgente (richiede più tempo)",AutoSize=true};
    private readonly CheckBox compatibleUsb=new(){Text="USB iPhone compatibile (più lento): usa se la lettura USB dà errori",AutoSize=true};
    private readonly Label status=Label("Scegli la cartella dei video esportati per iniziare.",13,FontStyle.Bold),detail=Label("Nessuna importazione avviata.",10),counts=Label("Video: —",11),bytes=Label("Spazio richiesto: —   ·   Disponibile: —",10),speed=Label("Velocità: —   ·   Tempo rimanente: —",10);
    private readonly ProgressBar progress=new(){Dock=DockStyle.Fill,Height=12,Maximum=1000};
    private readonly Label summary=Label("L’app non modifica né elimina contenuti sul telefono.",10);
    private CancellationTokenSource? cancellation;
    private bool busy,closing;
    private string? reportPath;
    private readonly string? smokePath;
    private static readonly Color Ink=Color.FromArgb(25,42,55),Accent=Color.FromArgb(0,104,94);

    public MainForm(string? smokePath)
    {
        this.smokePath=smokePath;Text=$"Porto {Application.ProductVersion.Split('+')[0]} · Foto e video sul PC";
        ClientSize=new(960,750);MinimumSize=new(720,560);StartPosition=FormStartPosition.CenterScreen;
        if(smokePath!=null){StartPosition=FormStartPosition.Manual;Location=new(-20000,-20000);ShowInTaskbar=false;}
        BackColor=Color.FromArgb(244,247,246);ForeColor=Ink;Font=new("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};Controls.Add(scroll);
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(24),ColumnCount=1,RowCount=7};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<7;i++)layout.RowStyles.Add(new(SizeType.AutoSize));scroll.Controls.Add(layout);
        var heading=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        heading.Controls.Add(Label("Porto",27,FontStyle.Bold));heading.Controls.Add(Label("Le tue foto e i tuoi video, sul PC. Senza conversioni.",12));layout.Controls.Add(heading);
        instructions.Dock=DockStyle.Fill;layout.Controls.Add(instructions);
        var setup=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,BackColor=Color.White,Padding=new(16),ColumnCount=3,RowCount=6};
        setup.ColumnStyles.Add(new(SizeType.AutoSize));setup.ColumnStyles.Add(new(SizeType.Percent,100));setup.ColumnStyles.Add(new(SizeType.AutoSize));
        for(int row=0;row<6;row++)setup.RowStyles.Add(new(SizeType.AutoSize));
        sourceMode.Items.AddRange(["Cartella del PC","Dispositivo USB · cartelle e raccolte","iPhone · libreria Foto via USB"]);sourceMode.SelectedIndex=1;
        mediaMode.Items.AddRange(["Solo video","Solo foto","Foto e video"]);mediaMode.SelectedIndex=0;
        usbScope.Items.AddRange(["Tutto il dispositivo accessibile","Una cartella del dispositivo"]);usbScope.SelectedIndex=0;
        setup.Controls.Add(Label("Importa da",10,FontStyle.Bold),0,0);setup.Controls.Add(sourceMode,1,0);setup.SetColumnSpan(sourceMode,2);
        var sourcePanel=new Panel{Dock=DockStyle.Fill,Height=32,MinimumSize=new(100,32)};
        sourcePanel.Controls.Add(phones);sourcePanel.Controls.Add(sourceFolder);
        setup.Controls.Add(Label("Origine",10,FontStyle.Bold),0,1);setup.Controls.Add(sourcePanel,1,1);setup.Controls.Add(search,2,1);
        setup.Controls.Add(Label("Cosa copiare",10,FontStyle.Bold),0,2);setup.Controls.Add(mediaMode,1,2);setup.SetColumnSpan(mediaMode,2);
        setup.Controls.Add(Label("Ambito USB",10,FontStyle.Bold),0,3);setup.Controls.Add(usbScope,1,3);setup.Controls.Add(selectUsbFolder,2,3);
        setup.Controls.Add(usbSelection,1,4);setup.SetColumnSpan(usbSelection,2);
        setup.Controls.Add(Label("Archivio PC",10,FontStyle.Bold),0,5);setup.Controls.Add(folder,1,5);setup.Controls.Add(browse,2,5);layout.Controls.Add(setup);
        deep.Dock=DockStyle.Fill;layout.Controls.Add(deep);
        compatibleUsb.Dock=DockStyle.Fill;layout.Controls.Add(compatibleUsb);
        var activity=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,BackColor=Color.White,Padding=new(16,10,16,10),ColumnCount=1,RowCount=6};
        activity.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<5;i++)activity.RowStyles.Add(new(SizeType.AutoSize));activity.RowStyles.Add(new(SizeType.Absolute,18));
        foreach(var c in new Control[]{status,detail,counts,bytes,speed,progress}){c.Dock=DockStyle.Fill;activity.Controls.Add(c);}layout.Controls.Add(activity);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new(24,10,24,12),WrapContents=true};
        foreach(var b in new[]{start,pause,resume,reportButton})actions.Controls.Add(b);Controls.Add(actions);
        summary.Dock=DockStyle.Fill;summary.Padding=new(0,8,0,0);layout.Controls.Add(summary);
        limits.Dock=DockStyle.Fill;limits.ForeColor=Color.FromArgb(76,87,94);layout.Controls.Add(limits);
        pause.Enabled=false;resume.Enabled=false;reportButton.Enabled=false;
        sourceMode.SelectedIndexChanged+=(_,_)=>UpdateMode();UpdateMode();
        usbScope.SelectedIndexChanged+=(_,_)=>UpdateScope();
        phones.SelectedIndexChanged+=(_,_)=>{appleCatalogCache.Clear();chosenUsbFolder=null;chosenUsbCollection=null;chosenAppleAlbum=null;UpdateScope();};
        selectUsbFolder.Click+=async(_,_)=>{if(AppleMode)await PickAppleAlbum();else await PickUsbFolder();};
        search.Click+=async(_,_)=>{if(FolderMode)PickSource();else await Discover();};browse.Click+=(_,_)=>PickFolder();
        start.Click+=async(_,_)=>await Import();resume.Click+=async(_,_)=>await Import();
        pause.Click+=(_,_)=>{cancellation?.Cancel();pause.Enabled=false;status.Text="Pausa in corso…";};
        reportButton.Click+=(_,_)=>
        {try{if(reportPath!=null)Process.Start(new ProcessStartInfo(reportPath){UseShellExecute=true});}
            catch(Exception ex)when(ex is IOException or System.ComponentModel.Win32Exception){detail.Text="Report nella cartella Report Porto. Apertura automatica non riuscita.";}};
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;closing=true;cancellation?.Cancel();status.Text="Chiusura sicura in corso…";}};
        FormClosed+=(_,_)=>appleCatalogCache.Clear();
        Shown+=async(_,_)=>
        {
            if(smokePath!=null)
            {
                if(smokePath.Contains("folder-picker",StringComparison.Ordinal))
                {
                    using var picker=new UsbFolderDialog([
                        new("Memoria interna / DCIM / Camera",[new("Memoria interna",null),new("DCIM",null),new("Camera",null)]),
                        new("Memoria interna / Pictures / Vacanze",[new("Memoria interna",null),new("Pictures",null),new("Vacanze",null)])])
                        {StartPosition=FormStartPosition.Manual,Location=new(-20000,-20000)};
                    picker.Show(this);await Task.Delay(350);
                    using var capture=new Bitmap(picker.Width,picker.Height);picker.DrawToBitmap(capture,new(0,0,picker.Width,picker.Height));capture.Save(smokePath);
                    picker.Close();Close();return;
                }
                if(smokePath.Contains("apple-albums",StringComparison.Ordinal))
                {
                    bool associationErrors=smokePath.Contains("associations",StringComparison.Ordinal);
                    using var picker=new AppleAlbumDialog([new("synthetic-1","Vacanze",45,40,5),new("synthetic-2","Famiglia",associationErrors?14:12,4,8,associationErrors?2:0)],associationErrors?3:0)
                        {StartPosition=FormStartPosition.Manual,Location=new(-20000,-20000)};
                    picker.Show(this);await Task.Delay(350);
                    if(associationErrors)picker.VerifySelectionForSmoke();
                    using var capture=new Bitmap(picker.Width,picker.Height);picker.DrawToBitmap(capture,new(0,0,picker.Width,picker.Height));capture.Save(smokePath);
                    picker.Close();Close();return;
                }
                WindowState=FormWindowState.Normal;ClientSize=smokePath.Contains("compact",StringComparison.Ordinal)?new(740,620):new(960,750);Location=new(-20000,-20000);
                sourceFolder.Text=@"C:\Video\Esportazione da Foto";
                if(smokePath.Contains("retry",StringComparison.Ordinal)||smokePath.Contains("incomplete",StringComparison.Ordinal))sourceMode.SelectedIndex=1;
                phones.Items.Add(new DeviceInfo("synthetic-no-device-access","Telefono USB · esempio per verifica UI"));phones.SelectedIndex=0;
                if(phones.SelectedItem is not DeviceInfo||string.IsNullOrEmpty(phones.Text))throw new InvalidOperationException("Selettore telefono non funzionante.");
                folder.Text=@"C:\Video\Archivio";bytes.Text="Spazio richiesto: dopo l’inventario   ·   Disponibile: 114,6 GiB";
                if(smokePath.Contains("retry",StringComparison.Ordinal)){SetBusy(true);ShowProgress(new("Recupero automatico","Tentativo 2/3 tra 15 secondi. Sblocca o ricollega lo stesso iPhone. Pausa interrompe l'attesa.",0,2500,0,null,null));}
                if(smokePath.Contains("paused",StringComparison.Ordinal))
                {
                    SetBusy(true);ShowProgress(new("Copia","Esempio sintetico di pausa",0,3,2*1024*1024,10*1024*1024,null,1024*1024));
                    string activeEstimate=speed.Text;SetBusy(false);status.Text="In pausa su richiesta. Premi Riprendi per continuare.";
                    if(speed.Text==activeEstimate)throw new InvalidOperationException("La stima deve essere rimossa quando il trasferimento si ferma.");
                }
                if(smokePath.Contains("incomplete",StringComparison.Ordinal))
                {
                    SetBusy(true);ShowProgress(new("Importazione","Esempio sintetico",20,20,1024*1024,1024*1024,null));
                    var incomplete=new ImportReport{Status="Importazione incompleta. Consulta il report.",Attempt=3,
                        UsbItemCount=100,ImageCount=70,SidecarCount=10,CandidateCount=20,SuspectedCompanionCount=16,TransferCandidateCount=20};
                    incomplete.Items.Add(new("synthetic","example.mov",Outcome.Error,"Errore sintetico di verifica."));
                    ShowResult(incomplete);SetBusy(false);
                    // A queued callback from the finished worker must not replace the final result.
                    ShowProgress(new("Importazione","Callback sintetico tardivo",20,20,1024*1024,1024*1024,null));
                    if(progress.Value!=0||!counts.Text.Contains("100",StringComparison.Ordinal)||!summary.Text.Contains("Candidati alla copia: 20",StringComparison.Ordinal))
                        throw new InvalidOperationException("L'esito incompleto deve distinguere i conteggi e rimuovere la barra verde.");
                }
                if(smokePath.Contains("folder-result",StringComparison.Ordinal))
                {
                    sourceMode.SelectedIndex=0;
                    var selection=new ImportReport{SelectedFolder=true,SelectedFolderVerified=true,CandidateCount=3,TransferCandidateCount=3,
                        Status="Verificati i 3 video della cartella selezionata. Copertura dell’iPhone non verificata."};
                    selection.Items.Add(new("a","esempio.mov",Outcome.AlreadyPresent,"Sintetico"));
                    selection.Items.Add(new("b","nuovo.mov",Outcome.Imported,"Sintetico"));
                    selection.Items.Add(new("c","altro.mov",Outcome.AlreadyPresent,"Sintetico"));
                    selection.PreviousCopies.Add(new("Video Porto/esempio.mov",100,null,true,true,"Sintetico"));
                    selection.PreviousCopies.Add(new("Video Porto/extra.mov",100,null,false,true,"Sintetico"));
                    ShowResult(selection);
                    if(!summary.Text.Contains("non riutilizzate: 1",StringComparison.Ordinal)||counts.Text.Contains("USB",StringComparison.Ordinal))
                        throw new InvalidOperationException("Confronto cartella non distinto dall’inventario USB.");
                }
                if(smokePath.Contains("media",StringComparison.Ordinal))
                {
                    sourceMode.SelectedIndex=1;mediaMode.SelectedIndex=2;usbScope.SelectedIndex=1;
                    if(!selectUsbFolder.Enabled||chosenUsbFolder!=null)throw new InvalidOperationException("La selezione USB deve essere esplicita.");
                    chosenUsbFolder=new("Memoria interna / Pictures / Vacanze",[new("Memoria interna",null),new("Pictures",null),new("Vacanze",null)]);UpdateScope();
                    SetBusy(true);if(mediaMode.Enabled||selectUsbFolder.Enabled||usbScope.Enabled)throw new InvalidOperationException("Selezione modificabile durante il trasferimento.");
                    SetBusy(false);if(!mediaMode.Enabled||!selectUsbFolder.Enabled)throw new InvalidOperationException("Selezione non riattivata.");
                    status.Text="Pronto: foto e video della cartella Vacanze.";detail.Text="Esempio sintetico: nessun dispositivo consultato.";
                    mediaMode.SelectedIndex=1;if((ImportMedia)mediaMode.SelectedIndex!=ImportMedia.Photos)throw new InvalidOperationException("Filtro foto non corretto.");
                    mediaMode.SelectedIndex=2;
                }
                if(smokePath.Contains("apple-gallery",StringComparison.Ordinal))
                {
                    sourceMode.SelectedIndex=2;phones.Items.Add(new AppleDevice("synthetic-no-device-access","iPhone · esempio sintetico"));phones.SelectedIndex=0;
                    mediaMode.SelectedIndex=0;usbScope.SelectedIndex=1;chosenAppleAlbum=new("synthetic-1","Vacanze",45,40,5);UpdateScope();
                    if(!selectUsbFolder.Enabled||!usbSelection.Text.Contains("Vacanze",StringComparison.Ordinal))throw new InvalidOperationException("Selezione album Foto non funzionante.");
                    SetBusy(true);if(selectUsbFolder.Enabled||sourceMode.Enabled||compatibleUsb.Enabled)throw new InvalidOperationException("Selezione Foto modificabile durante il lavoro.");SetBusy(false);
                    if(!compatibleUsb.Enabled||!compatibleUsb.Visible||compatibleUsb.Checked)throw new InvalidOperationException("Opzione compatibilità USB non disponibile o attiva per impostazione predefinita.");
                    ShowResult(new ImportReport{SelectedGallery=true,SelectedVideoCount=5,CandidateCount=5,
                        GalleryCoverage=new("Catalogo Foto osservato; completezza della libreria non certificata.",0,5,1,2,3),
                        Status="Selezione della galleria incompleta: un originale non è disponibile sul telefono."});
                    if(!summary.Text.Contains("Non risolti all'inizio: 1",StringComparison.Ordinal)||counts.Text.Contains("Risorse USB",StringComparison.Ordinal))throw new InvalidOperationException("Conteggi Foto non distinti da file USB.");
                }
                if(smokePath.Contains("catalog-error",StringComparison.Ordinal))
                {
                    status.Text="Catalogo Foto non disponibile.";
                    detail.Text="[APPLE-CATALOG-TOTAL-LIMIT] La somma di database e WAL supera il limite di memoria. DB: 2146435060 byte; WAL: 65536 byte. Limite: 2146435072 byte (2047 MiB, circa 2 GiB).";
                    scroll.ScrollControlIntoView(detail);
                }
                await Task.Delay(350);if(Width<700||Height<550)throw new InvalidOperationException("UI smoke dimensions invalid");
                foreach(var button in new[]{start,pause,resume,reportButton})
                {var bounds=RectangleToClient(button.RectangleToScreen(button.ClientRectangle));if(bounds.Bottom>ClientSize.Height||bounds.Right>ClientSize.Width)throw new InvalidOperationException("Action button outside visible window");}
                using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new(0,0,Width,Height));bitmap.Save(smokePath);SetBusy(false);Close();
            }
            else
            {
                var area=Screen.FromControl(this).WorkingArea;Size=new(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
                folder.Text=AppPreferences.LoadFolder();
            }
        };
    }
    private void UpdateMode()
    {
        phones.Items.Clear();chosenUsbFolder=null;chosenUsbCollection=null;chosenAppleAlbum=null;
        usbScope.Items.Clear();usbScope.Items.AddRange(AppleMode
            ?new object[]{"Galleria visibile · tutti gli elementi","Un album della libreria Foto"}
            :new object[]{"Tutti i file accessibili via USB","Una cartella o raccolta USB"});usbScope.SelectedIndex=0;
        selectUsbFolder.Text=AppleMode?"Scegli album":"Scegli cartella / raccolta";
        phones.Visible=!FolderMode;sourceFolder.Visible=FolderMode;search.Text=FolderMode?"Scegli origine":"Cerca dispositivi";
        instructions.Text=FolderMode
            ?"1  Scegli una cartella o un album già esportato sul PC.  2  Scegli cosa copiare.\n3  Scegli l’archivio PC, anche quello delle prove precedenti, e avvia."
            :AppleMode?"1  Collega e autorizza l’iPhone.  2  Scegli foto/video e tutta la galleria o un album.\n3  Scegli l’archivio PC e avvia. Serve il collegamento Apple già presente su Windows."
            :"1  Collega e sblocca il telefono. Autorizza l’accesso; su Android scegli Trasferimento file.\nSu iPhone: Impostazioni → App → Foto → Trasferisci su Mac o PC → Mantieni originali.";
        limits.Text=FolderMode
            ?"COPERTURA DELLA CARTELLA\nSono verificati i file presenti nella cartella e nelle sottocartelle. Un album delle app Foto o Google Foto va prima esportato qui. Porto non può confermare la completezza dell’esportazione. Le copie precedenti restano al loro posto."
            :AppleMode?"CATALOGO FOTO IPHONE · CANDIDATA DA COLLAUDARE\nLegge localmente i metadati della libreria Foto per distinguere video, immagini e album; non salva il database del telefono. Live Photo: sola immagine, mai incluse in Solo video. Nascosti ed Eliminati di recente esclusi. Originali solo iCloud non copiabili via USB. Compatibilità con lo schema iOS da verificare."
            :"COPERTURA USB\nCartelle e raccolte esposte da Windows: gli album virtuali di Google Foto o delle altre app possono non essere accessibili. Scegliere una cartella legge solo quel ramo; Tutti i file richiede l’elenco completo. Per distinguere i video dalle Live Photo usa iPhone · libreria Foto via USB.";
        status.Text=FolderMode?"Scegli origine, contenuti e archivio.":"Collega il telefono e premi Cerca dispositivi.";
        detail.Text="Nessun file esistente viene cancellato o sovrascritto.";
        summary.Text=FolderMode?"Per il confronto scegli lo stesso archivio che contiene .porto e Report Porto.":"L’app legge dal telefono e copia sul PC. Non modifica i contenuti del telefono.";
        counts.Text="Foto e video: —";bytes.Text="Spazio richiesto: dopo l’inventario";UpdateScope();
    }
    private void UpdateScope()
    {
        compatibleUsb.Visible=AppleMode;
        usbScope.Enabled=!busy&&!FolderMode;
        selectUsbFolder.Enabled=!busy&&!FolderMode&&usbScope.SelectedIndex==1;
        usbSelection.Text=FolderMode?"La cartella di origine comprende anche le sottocartelle."
            :AppleMode?usbScope.SelectedIndex==0?"Foto e video visibili nel catalogo; Nascosti ed Eliminati di recente esclusi.":chosenAppleAlbum==null?"Premi Scegli album: nessuna esportazione preliminare.":$"{chosenAppleAlbum.Name} · {chosenAppleAlbum.PhotoCount:N0} foto · {chosenAppleAlbum.VideoCount:N0} video"
            :usbScope.SelectedIndex==0?"Tutte le cartelle USB accessibili."
            :chosenUsbCollection!=null?$"Raccolta USB: {chosenUsbCollection.DisplayPath}":chosenUsbFolder?.DisplayPath??"Scegli una cartella o una raccolta esposta via USB prima di avviare.";
    }
    private async Task PickUsbFolder()
    {
        if(phones.SelectedItem is not DeviceInfo phone){status.Text="Prima premi Cerca dispositivi e scegli il telefono.";return;}
        cancellation=new();SetBusy(true);status.Text="Leggo le cartelle accessibili via USB…";
        await Task.Yield();
        try
        {
            var updates=new Progress<int>(n=>{if(busy)detail.Text=$"{n:N0} elementi esaminati; nessun file copiato.";});
            using var source=new ReconnectingUsbSource(Environment.ProcessPath!,phone.Id,n=>((IProgress<int>)updates).Report(n));
            using var dialog=new UsbFolderDialog((parent,token)=>Task.Run(()=>source.ReadLevel(parent,token),token));
            if(dialog.ShowDialog(this)==DialogResult.OK){chosenUsbFolder=dialog.SelectedFolder;chosenUsbCollection=dialog.SelectedCollection;status.Text="Selezione USB pronta. Scegli cosa copiare e l’archivio PC.";}
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {status.Text="Selezione cartella non completata.";detail.Text=ex.Message;}
        finally{cancellation.Dispose();cancellation=null;SetBusy(false);if(closing)Close();}
    }
    private void PickSource()
    {
        using var dialog=new FolderBrowserDialog{Description="Scegli la cartella o l’album esportato sul PC, separato dall’archivio Porto",UseDescriptionForTitle=true};
        if(dialog.ShowDialog(this)==DialogResult.OK)sourceFolder.Text=dialog.SelectedPath;
    }
    private async Task PickAppleAlbum()
    {
        if(phones.SelectedItem is not AppleDevice device){status.Text="Prima premi Cerca dispositivi e scegli l’iPhone.";return;}
        cancellation=new();SetBusy(true);status.Text="Leggo gli album dal catalogo Foto…";
        detail.Text="Metadati letti localmente; nessuna foto o video viene copiato in questa fase.";
        try
        {
            bool compatibility=compatibleUsb.Checked;
            var catalog=await Task.Run(()=>AppleGalleryDiscovery.ListAlbums(device,cancellation.Token,compatibility,appleCatalogCache));
            cancellation.Token.ThrowIfCancellationRequested();
            using var dialog=new AppleAlbumDialog(catalog.Albums,catalog.UnresolvedAssociationCount);
            if(dialog.ShowDialog(this)==DialogResult.OK){chosenAppleAlbum=dialog.SelectedAlbum;status.Text="Album scelto. Scegli l’archivio PC e avvia.";}
            else status.Text="Selezione album annullata.";
            detail.Text=catalog.Albums.Count==0&&catalog.UnresolvedAssociationCount==0?"Nessun album compatibile esposto dal catalogo. La galleria visibile resta una scelta separata.":catalog.ScopeNote;
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {status.Text="Catalogo Foto non disponibile.";detail.Text=ex.Message;}
        finally{cancellation.Dispose();cancellation=null;SetBusy(false);if(closing)Close();}
    }
    private static Button Button(string text,bool primary=false)=>new(){Text=text,AutoSize=true,MinimumSize=new(primary?170:120,36),Padding=new(8,3,8,3),
        FlatStyle=FlatStyle.Flat,BackColor=primary?Accent:Color.White,ForeColor=primary?Color.White:Ink,Margin=new(0,0,10,0),Cursor=Cursors.Hand};
    private static Label Label(string text,float size,FontStyle style=FontStyle.Regular)=>new(){Text=text,AutoSize=true,Margin=new(3,6,3,6),Font=new("Segoe UI",size,style),TextAlign=ContentAlignment.MiddleLeft};
    private void PickFolder()
    {
        using var dialog=new FolderBrowserDialog{Description="Scegli una cartella locale su NTFS",UseDescriptionForTitle=true};
        if(dialog.ShowDialog(this)==DialogResult.OK)
        {folder.Text=dialog.SelectedPath;AppPreferences.SaveFolder(folder.Text);try{bytes.Text=$"Spazio richiesto: dopo l’inventario   ·   Disponibile: {FormatBytes(new LocalDiskSpace().Available(folder.Text))}";}catch(IOException){bytes.Text="Spazio disponibile non leggibile.";}}
    }
    private async Task Discover()
    {
        SetBusy(true);status.Text="Cerco dispositivi portatili collegati via USB…";cancellation=new();
        try
        {
            phones.Items.Clear();
            if(AppleMode)
            {
                var apple=await Task.Run(()=>AppleGalleryDiscovery.Discover(cancellation.Token));
                foreach(var device in apple.Devices)phones.Items.Add(device);
                if(apple.Devices.Count>0){phones.SelectedIndex=apple.Devices.Count==1?0:-1;status.Text="iPhone individuati. Scegli il dispositivo, i contenuti, l’ambito e l’archivio PC.";}
                else status.Text="Nessun iPhone disponibile per la lettura del catalogo.";
                detail.Text=apple.UnavailableReason??"Collegamento Apple presente; il catalogo sarà letto quando scegli un album o avvii.";
                return;
            }
            var discovery=await Task.Run(()=>{using var worker=new WorkerClient(Environment.ProcessPath!);return worker.Discover(cancellation.Token);});
            var devices=discovery.Devices;
            phones.Items.Clear();foreach(var device in devices)phones.Items.Add(device);
            if(devices.Count>0){phones.SelectedIndex=devices.Count==1?0:-1;status.Text="Dispositivi individuati. Scegli il telefono, cosa copiare e l’archivio.";}
            else status.Text="Collega e sblocca il telefono, poi premi Cerca dispositivi.";
            detail.Text=discovery.Detail;
            if(devices.Count==0)summary.Text="Sblocca il telefono e usa un cavo dati. Su Android scegli Trasferimento file dalla notifica USB.\nSu iPhone accetta Autorizza. Se Windows non lo vede, verifica il collegamento con Dispositivi Apple.";
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or OperationCanceledException){status.Text="Ricerca interrotta o dispositivo non disponibile.";detail.Text=ex.Message;}
        finally{cancellation.Dispose();cancellation=null;SetBusy(false);if(closing)Close();}
    }
    private async Task Import()
    {
        var phone=phones.SelectedItem as DeviceInfo;
        var applePhone=phones.SelectedItem as AppleDevice;
        if(!FolderMode&&(AppleMode?applePhone==null:phone==null)){status.Text="Collega il telefono e premi Cerca dispositivi.";return;}
        if(FolderMode&&string.IsNullOrWhiteSpace(sourceFolder.Text)){PickSource();if(string.IsNullOrWhiteSpace(sourceFolder.Text))return;}
        if(string.IsNullOrWhiteSpace(folder.Text)){PickFolder();if(string.IsNullOrWhiteSpace(folder.Text))return;}
        if(!FolderMode&&usbScope.SelectedIndex==1&&(AppleMode?chosenAppleAlbum==null:chosenUsbFolder==null&&chosenUsbCollection==null)){status.Text="Scegli prima l’album, la raccolta o la cartella.";return;}
        string path=folder.Text,origin=sourceFolder.Text;bool deeper=deep.Checked,fromFolder=FolderMode,fromApple=AppleMode,compatibility=compatibleUsb.Checked;
        var media=(ImportMedia)mediaMode.SelectedIndex;var scope=usbScope.SelectedIndex==1?chosenUsbFolder:null;
        var collection=usbScope.SelectedIndex==1?chosenUsbCollection:null;
        string? albumId=usbScope.SelectedIndex==1?chosenAppleAlbum?.Id:null;
        cancellation=new();SetBusy(true);reportPath=null;reportButton.Enabled=false;progress.Value=0;
        var updates=new Progress<ImportProgress>(ShowProgress);
        try
        {
            var result=await Task.Run(()=>
            {
                // Validate source/destination overlap before creating archive state.
                using var selectedSource=fromFolder?new FolderVideoSource(origin,path):null;
                using var destination=new Destination(path);
                using var awake=new AwakeScope();
                if(selectedSource!=null)
                {
                    var engine=new ImportEngine(selectedSource,destination){Media=media,Progress=p=>((IProgress<ImportProgress>)updates).Report(p)};
                    var folderReport=engine.Run(deeper,cancellation.Token);return(report:folderReport,ReportPath:engine.ReportPath);
                }
                IVideoSource CreateSource()=>fromApple?new AppleGallerySource(applePhone!,albumId,media,compatibility,appleCatalogCache):new UsbImportSource(new ReconnectingUsbSource(Environment.ProcessPath!,phone!.Id,
                    n=>((IProgress<ImportProgress>)updates).Report(new("Inventario USB",$"{n:N0} elementi esaminati",0,0,0,null,null))),phone.Id,phone.Name,phone.IsApple,scope,collection);
                var session=new ImportSession(CreateSource,destination)
                    {Media=media,Progress=p=>((IProgress<ImportProgress>)updates).Report(p)};
                var report=session.Run(deeper,cancellation.Token);return(report,session.ReportPath);
            });
            reportPath=result.ReportPath;ShowResult(result.report);
            if(cancellation.IsCancellationRequested)status.Text="In pausa su richiesta. Premi Riprendi per continuare.";
            else if(result.report.RetryRecommended)detail.Text="Tentativi automatici esauriti. Verifica collegamento e report, poi premi Riprendi.";
            reportButton.Enabled=reportPath!=null;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidDataException or OperationCanceledException or OverflowException)
        {status.Text="Importazione in pausa.";detail.Text=ex.Message;summary.Text="Nessun file esistente viene sovrascritto. Risolvi la causa e premi Riprendi.";}
        finally{cancellation.Dispose();cancellation=null;SetBusy(false);resume.Enabled=true;if(closing)Close();}
    }
    private void ShowProgress(ImportProgress p)
    {
        if(!busy)return;
        status.Text=p.Phase;detail.Text=p.Name;
        bool inventory=p.Phase is "Inventario USB" or "Inventario cartella";
        if(AppleMode)
        {
            counts.Text=$"Elementi della selezione Foto valutati: {p.Done:N0} / {(p.Total>0?p.Total.ToString("N0"):"—")} · Byte elaborati: {FormatBytes(p.Bytes)}";
            bytes.Text=$"Dimensioni selezione: {(p.TotalBytes.HasValue?FormatBytes(p.TotalBytes.Value):"da determinare")} · Disponibili: {(p.FreeBytes.HasValue?FormatBytes(p.FreeBytes.Value):"—")}";
        }
        else if(FolderMode)
        {
            counts.Text=$"Elementi della cartella valutati: {p.Done:N0} / {(p.Total>0?p.Total.ToString("N0"):"—")} · Byte copiati: {FormatBytes(p.Bytes)}";
            bytes.Text=$"Dimensioni candidate: {(p.TotalBytes.HasValue?FormatBytes(p.TotalBytes.Value):"da determinare")} · Disponibili: {(p.FreeBytes.HasValue?FormatBytes(p.FreeBytes.Value):"—")}";
        }
        else
        {
        counts.Text=inventory?"Esame dei metadati USB: comprende foto, cartelle e file accessori.":
            $"Elementi USB valutati: {p.Done:N0} / {(p.Total>0?p.Total.ToString("N0"):"—")}   ·   Byte elaborati: {FormatBytes(p.Bytes)}";
        bytes.Text=inventory?"I candidati alla copia saranno distinti al termine dell'inventario.":
            $"Dimensioni candidate USB: {(p.TotalBytes.HasValue?FormatBytes(p.TotalBytes.Value):"da determinare / non note")}   ·   Disponibile: {(p.FreeBytes.HasValue?FormatBytes(p.FreeBytes.Value):"—")}";
        }
        string eta=p.Done<p.Total&&p.TotalBytes.HasValue&&p.BytesPerSecond>0?TimeSpan.FromSeconds(Math.Clamp((p.TotalBytes.Value-p.Bytes)/p.BytesPerSecond,0,31536000)).ToString(@"d\.hh\:mm\:ss"):"—";
        speed.Text=$"Media complessiva: {(p.BytesPerSecond>0?FormatBytes((long)p.BytesPerSecond)+"/s":"—")}   ·   Stima copia: {eta} (verifiche finali escluse)";
        progress.Value=p.Total>0?(int)Math.Clamp(1000L*p.Done/p.Total,0,1000):0;
    }
    private void ShowResult(ImportReport report)
    {
        status.Text=report.Status;detail.Text=report.SourceDescription;
        if(report.SelectedGallery)
        {
            counts.Text=$"Selezionati dal catalogo: {report.SelectedPhotoCount:N0} foto · {report.SelectedVideoCount:N0} video";
            bytes.Text=$"Nuove copie: {report.CurrentCount(Outcome.Imported):N0} · Già presenti verificate: {report.CurrentCount(Outcome.AlreadyPresent):N0}";
            detail.Text=report.GalleryCoverage?.Note??"Lettura del catalogo Foto non completata.";
            summary.Text=$"Pendenti: {report.CurrentCount(Outcome.Pending)} · Mancanti: {report.CurrentCount(Outcome.Missing)} · Ambigui: {report.CurrentCount(Outcome.Ambiguous)} · Errori: {report.CurrentCount(Outcome.Error)}\nCopie precedenti non riutilizzate: {report.PreviousCopies.Count(c=>!c.ReusedForSelection):N0} (conservate). Storico da verificare: {report.HistoricalCount}.";
            if(report.GalleryCoverage is {} coverage)summary.Text+=$"\nEsclusi dal catalogo iniziale: {coverage.HiddenExcluded:N0} nascosti · {coverage.DeletedExcluded:N0} eliminati di recente. Non risolti all'inizio: {coverage.Unresolved:N0}.";
            return;
        }
        if(report.SelectedFolder)
        {
            counts.Text=$"Elementi selezionati: {report.CandidateCount:N0} · Verificati: {report.CurrentCount(Outcome.Imported)+report.CurrentCount(Outcome.AlreadyPresent):N0}";
            bytes.Text=$"Nuove copie: {report.CurrentCount(Outcome.Imported):N0} · Copie già presenti riutilizzate: {report.CurrentCount(Outcome.AlreadyPresent):N0}";
            detail.Text=report.SelectedFolderVerified?"Tutti gli elementi selezionati nella cartella hanno una copia verificata. Consulta anche il confronto nel report.":"Ci sono elementi da completare o verificare: apri il report.";
            summary.Text=$"Copie precedenti non riutilizzate: {report.PreviousCopies.Count(c=>!c.ReusedForSelection):N0} (conservate; non significa Live Photo)\nPendenti: {report.CurrentCount(Outcome.Pending)} · Mancanti: {report.CurrentCount(Outcome.Missing)} · Ambigui: {report.CurrentCount(Outcome.Ambiguous)} · Errori: {report.CurrentCount(Outcome.Error)}\nOsservazioni storiche da verificare: {report.HistoricalCount}. Percorsi e dettagli nel report.";
            return;
        }
        counts.Text=$"Risorse USB: {report.UsbItemCount:N0} · Immagini: {report.ImageCount:N0} · File AAE: {report.SidecarCount:N0}";
        bytes.Text=$"Selezionati: {report.SelectedPhotoCount:N0} foto · {report.SelectedVideoCount:N0} video · Indizi Apple: {report.SuspectedCompanionCount:N0}";
        summary.Text=$"Candidati alla copia: {report.TransferCandidateCount:N0} — Passaggio {report.Attempt}\nCopie verificate: {report.CurrentCount(Outcome.Imported)+report.CurrentCount(Outcome.AlreadyPresent)} · Nuove: {report.CurrentCount(Outcome.Imported)} · Già presenti: {report.CurrentCount(Outcome.AlreadyPresent)}\nPendenti aggiuntivi: {report.CurrentCount(Outcome.Pending)} · Mancanti: {report.CurrentCount(Outcome.Missing)} · Ambigui: {report.CurrentCount(Outcome.Ambiguous)} · Errori: {report.CurrentCount(Outcome.Error)}\nRisorse aggiuntive verificate: {report.CurrentCount(Outcome.Additional)+report.CurrentCount(Outcome.AdditionalAlreadyPresent)} · Osservazioni storiche da verificare: {report.HistoricalCount}";
    }
    private void SetBusy(bool value)
    {
        busy=value;foreach(Control c in new Control[]{sourceMode,sourceFolder,mediaMode,usbScope,selectUsbFolder,search,browse,start,phones,folder,deep,compatibleUsb,resume})c.Enabled=!value;pause.Enabled=value;
        UpdateScope();
        if(!value){speed.Text="Velocità: —   ·   Tempo rimanente: —";progress.Value=0;}
    }
    private static string FormatBytes(long bytes)=>bytes>=1024L*1024*1024?$"{bytes/(1024d*1024*1024):N1} GiB":$"{bytes/(1024d*1024):N1} MiB";
}

