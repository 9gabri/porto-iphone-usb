using System.Diagnostics;
using Porto.Core;
using Porto.Usb;

namespace Porto.App;

public sealed class MainForm:Form
{
    private readonly ComboBox phones=new(){DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill,DisplayMember="Name"};
    private readonly TextBox folder=new(){ReadOnly=true,Dock=DockStyle.Fill,PlaceholderText="Scegli dove conservare i video"};
    private readonly Button search=Button("Cerca iPhone"),browse=Button("Scegli cartella"),start=Button("Avvia importazione",true),pause=Button("Pausa"),resume=Button("Riprendi"),reportButton=Button("Apri report");
    private readonly CheckBox deep=new(){Text="Verifica approfondita: rileggi anche la sorgente USB (richiede più tempo)",AutoSize=true};
    private readonly Label status=Label("Collega l’iPhone per iniziare.",13,FontStyle.Bold),detail=Label("Nessuna importazione avviata.",10),counts=Label("Video: —",11),bytes=Label("Spazio richiesto: —   ·   Disponibile: —",10),speed=Label("Velocità: —   ·   Tempo rimanente: —",10);
    private readonly ProgressBar progress=new(){Dock=DockStyle.Fill,Height=12,Maximum=1000};
    private readonly Label summary=Label("L’app non modifica né elimina contenuti sul telefono.",10);
    private CancellationTokenSource? cancellation;
    private bool busy,closing;
    private string? reportPath;
    private readonly string? smokePath;
    private static readonly Color Ink=Color.FromArgb(25,42,55),Accent=Color.FromArgb(0,104,94);

    public MainForm(string? smokePath)
    {
        this.smokePath=smokePath;Text=$"Porto {Application.ProductVersion.Split('+')[0]} · Importa video da iPhone";
        ClientSize=new(960,750);MinimumSize=new(720,560);StartPosition=FormStartPosition.CenterScreen;
        if(smokePath!=null){StartPosition=FormStartPosition.Manual;Location=new(-20000,-20000);ShowInTaskbar=false;}
        BackColor=Color.FromArgb(244,247,246);ForeColor=Ink;Font=new("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};Controls.Add(scroll);
        var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(24),ColumnCount=1,RowCount=7};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<7;i++)layout.RowStyles.Add(new(SizeType.AutoSize));scroll.Controls.Add(layout);
        var heading=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        heading.Controls.Add(Label("Porto",27,FontStyle.Bold));heading.Controls.Add(Label("I tuoi video, sul tuo PC. Tramite USB.",12));layout.Controls.Add(heading);
        var instructions=Label("1  Collega, sblocca e autorizza l’iPhone.     2  Scegli una cartella.     3  Avvia.\nPrima di importare: iPhone → Impostazioni → App → Foto → Trasferisci su Mac o PC → Mantieni originali.",10);
        instructions.Dock=DockStyle.Fill;layout.Controls.Add(instructions);
        var setup=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,BackColor=Color.White,Padding=new(16),ColumnCount=3,RowCount=2};
        setup.ColumnStyles.Add(new(SizeType.AutoSize));setup.ColumnStyles.Add(new(SizeType.Percent,100));setup.ColumnStyles.Add(new(SizeType.AutoSize));
        setup.RowStyles.Add(new(SizeType.AutoSize));setup.RowStyles.Add(new(SizeType.AutoSize));
        setup.Controls.Add(Label("Telefono",10,FontStyle.Bold),0,0);setup.Controls.Add(phones,1,0);setup.Controls.Add(search,2,0);
        setup.Controls.Add(Label("Cartella",10,FontStyle.Bold),0,1);setup.Controls.Add(folder,1,1);setup.Controls.Add(browse,2,1);layout.Controls.Add(setup);
        deep.Dock=DockStyle.Fill;layout.Controls.Add(deep);
        var activity=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,BackColor=Color.White,Padding=new(16,10,16,10),ColumnCount=1,RowCount=6};
        activity.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<5;i++)activity.RowStyles.Add(new(SizeType.AutoSize));activity.RowStyles.Add(new(SizeType.Absolute,18));
        foreach(var c in new Control[]{status,detail,counts,bytes,speed,progress}){c.Dock=DockStyle.Fill;activity.Controls.Add(c);}layout.Controls.Add(activity);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,Padding=new(24,10,24,12),WrapContents=true};
        foreach(var b in new[]{start,pause,resume,reportButton})actions.Controls.Add(b);Controls.Add(actions);
        summary.Dock=DockStyle.Fill;summary.Padding=new(0,8,0,0);layout.Controls.Add(summary);
        var limits=Label("COPERTURA USB\nPossono mancare originali presenti solo su iCloud. L’intera libreria e l’originalità dei contenuti non sono certificabili via USB. Possibili Live Photos e versioni modificate vengono segnalate nel report.",9);
        limits.Dock=DockStyle.Fill;limits.ForeColor=Color.FromArgb(76,87,94);layout.Controls.Add(limits);
        pause.Enabled=false;resume.Enabled=false;reportButton.Enabled=false;
        search.Click+=async(_,_)=>await Discover();browse.Click+=(_,_)=>PickFolder();
        start.Click+=async(_,_)=>await Import();resume.Click+=async(_,_)=>await Import();
        pause.Click+=(_,_)=>{cancellation?.Cancel();pause.Enabled=false;status.Text="Pausa in corso…";};
        reportButton.Click+=(_,_)=>
        {try{if(reportPath!=null)Process.Start(new ProcessStartInfo(reportPath){UseShellExecute=true});}
            catch(Exception ex)when(ex is IOException or System.ComponentModel.Win32Exception){detail.Text="Report nella cartella Report Porto. Apertura automatica non riuscita.";}};
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;closing=true;cancellation?.Cancel();status.Text="Chiusura sicura in corso…";}};
        Shown+=async(_,_)=>
        {
            if(smokePath!=null)
            {
                WindowState=FormWindowState.Normal;ClientSize=smokePath.Contains("compact",StringComparison.Ordinal)?new(740,620):new(960,750);Location=new(-20000,-20000);
                phones.Items.Add(new DeviceInfo("synthetic-no-device-access","iPhone · esempio per verifica UI"));phones.SelectedIndex=0;
                if(phones.SelectedItem is not DeviceInfo||string.IsNullOrEmpty(phones.Text))throw new InvalidOperationException("Selettore telefono non funzionante.");
                folder.Text=@"C:\Video\Archivio";bytes.Text="Spazio richiesto: dopo l’inventario   ·   Disponibile: 114,6 GiB";
                if(smokePath.Contains("retry",StringComparison.Ordinal)){SetBusy(true);ShowProgress(new("Recupero automatico","Tentativo 2/3 tra 15 secondi. Sblocca o ricollega lo stesso iPhone. Pausa interrompe l'attesa.",0,2500,0,null,null));}
                await Task.Delay(350);if(Width<700||Height<550)throw new InvalidOperationException("UI smoke dimensions invalid");
                foreach(var button in new[]{start,pause,resume,reportButton})
                {var bounds=RectangleToClient(button.RectangleToScreen(button.ClientRectangle));if(bounds.Bottom>ClientSize.Height||bounds.Right>ClientSize.Width)throw new InvalidOperationException("Action button outside visible window");}
                using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new(0,0,Width,Height));bitmap.Save(smokePath);SetBusy(false);Close();
            }
            else
            {
                var area=Screen.FromControl(this).WorkingArea;Size=new(Math.Min(Width,area.Width),Math.Min(Height,area.Height));
                folder.Text=AppPreferences.LoadFolder();await Discover();
            }
        };
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
        SetBusy(true);status.Text="Cerco un iPhone collegato via USB…";cancellation=new();
        try
        {
            phones.Items.Clear();
            var discovery=await Task.Run(()=>{using var worker=new WorkerClient(Environment.ProcessPath!);return worker.Discover(cancellation.Token);});
            var devices=discovery.Devices;
            phones.Items.Clear();foreach(var device in devices)phones.Items.Add(device);
            if(devices.Count>0){phones.SelectedIndex=0;status.Text="iPhone individuato. Scegli la cartella e avvia.";}
            else status.Text="Collega e sblocca l’iPhone, poi premi Cerca iPhone.";
            detail.Text=discovery.Detail;
            if(devices.Count==0)summary.Text="1. Sblocca l'iPhone e ricollega il cavo dati.  2. Accetta Autorizza sul telefono.\n3. Apri Dispositivi Apple: se lì compare, chiudilo e riprova Cerca iPhone. Se Porto ancora non lo vede, il collegamento richiede una diagnosi.";
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or OperationCanceledException){status.Text="Ricerca interrotta o dispositivo non disponibile.";detail.Text=ex.Message;}
        finally{cancellation.Dispose();cancellation=null;SetBusy(false);if(closing)Close();}
    }
    private async Task Import()
    {
        if(phones.SelectedItem is not DeviceInfo phone){status.Text="Collega l’iPhone e premi Cerca iPhone.";return;}
        if(string.IsNullOrWhiteSpace(folder.Text)){PickFolder();if(string.IsNullOrWhiteSpace(folder.Text))return;}
        string path=folder.Text;bool deeper=deep.Checked;
        cancellation=new();SetBusy(true);reportPath=null;reportButton.Enabled=false;progress.Value=0;
        var updates=new Progress<ImportProgress>(ShowProgress);
        try
        {
            var result=await Task.Run(()=>
            {
                using var destination=new Destination(path);
                using var awake=new AwakeScope();
                var session=new ImportSession(()=>new ReconnectingUsbSource(Environment.ProcessPath!,phone.Id,
                    n=>((IProgress<ImportProgress>)updates).Report(new("Inventario USB",$"{n:N0} elementi esaminati",0,0,0,null,null))),destination)
                    {Progress=p=>((IProgress<ImportProgress>)updates).Report(p)};
                var report=session.Run(deeper,cancellation.Token);return(report,session.ReportPath);
            });
            reportPath=result.ReportPath;status.Text=result.report.Status;
            summary.Text=$"Ultimo passaggio {result.report.Attempt}/{ImportSession.MaximumAttempts} — Importati: {result.report.Count(Outcome.Imported)} · Già presenti verificati: {result.report.Count(Outcome.AlreadyPresent)}\nDa completare: {result.report.Count(Outcome.Pending)} · Mancanti: {result.report.Count(Outcome.Missing)} · Ambigui: {result.report.Count(Outcome.Ambiguous)} · Errori: {result.report.Count(Outcome.Error)}\nI report di ogni passaggio sono nella cartella scelta.";
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
        status.Text=p.Phase;detail.Text=p.Name;counts.Text=$"Video esaminati: {p.Done:N0} / {(p.Total>0?p.Total.ToString("N0"):"—")}   ·   Copiati: {FormatBytes(p.Bytes)}";
        bytes.Text=$"Dimensioni video USB: {(p.TotalBytes.HasValue?FormatBytes(p.TotalBytes.Value):"da determinare / non note")}   ·   Disponibile: {(p.FreeBytes.HasValue?FormatBytes(p.FreeBytes.Value):"—")}";
        string eta=p.TotalBytes.HasValue&&p.BytesPerSecond>0?TimeSpan.FromSeconds(Math.Clamp((p.TotalBytes.Value-p.Bytes)/p.BytesPerSecond,0,31536000)).ToString(@"d\.hh\:mm\:ss"):"—";
        speed.Text=$"Copia media: {(p.BytesPerSecond>0?FormatBytes((long)p.BytesPerSecond)+"/s":"—")}   ·   Stima copia: {eta} (verifiche escluse)";
        progress.Value=p.Total>0?(int)Math.Clamp(1000L*p.Done/p.Total,0,1000):0;
    }
    private void SetBusy(bool value)
        {busy=value;foreach(Control c in new Control[]{search,browse,start,phones,folder,deep,resume})c.Enabled=!value;pause.Enabled=value;}
    private static string FormatBytes(long bytes)=>bytes>=1024L*1024*1024?$"{bytes/(1024d*1024*1024):N1} GiB":$"{bytes/(1024d*1024):N1} MiB";
}

