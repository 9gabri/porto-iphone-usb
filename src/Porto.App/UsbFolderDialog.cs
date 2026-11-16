using Porto.Core;

namespace Porto.App;

internal sealed class UsbFolderDialog : Form
{
    private readonly ListBox folders=new(){Dock=DockStyle.Fill,HorizontalScrollbar=true,IntegralHeight=false};
    private readonly Label location=new(){AutoSize=true,Dock=DockStyle.Fill,Padding=new(0,8,0,8)};
    private readonly Label message=new(){AutoSize=true,Dock=DockStyle.Fill,Padding=new(0,8,0,8)};
    private readonly Button up=new(){Text="Indietro",AutoSize=true};
    private readonly Button open=new(){Text="Apri",AutoSize=true};
    private readonly Button select=new(){Text="Usa questa cartella",AutoSize=true};
    private readonly Func<UsbFolderChoice?,CancellationToken,Task<UsbNavigationLevel>> list;
    private readonly CancellationTokenSource cancellation=new();
    private UsbFolderChoice? current;
    private bool loading,closing,validLevel;
    public UsbFolderChoice? SelectedFolder {get;private set;}
    public UsbCollectionChoice? SelectedCollection {get;private set;}
    public UsbFolderDialog(Func<UsbFolderChoice?,CancellationToken,Task<UsbNavigationLevel>> list)
    {
        this.list=list;
        Text="Scegli una cartella del dispositivo";ClientSize=new(690,470);MinimumSize=new(490,340);
        StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;Font=new("Segoe UI",10);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=5};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Percent,100));
        layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(layout);
        layout.Controls.Add(new Label{Text="Apri le cartelle fino a quella desiderata. Leggo solo il livello aperto.\nSono cartelle fisiche USB: gli album virtuali delle app possono non comparire.",AutoSize=true,Dock=DockStyle.Fill});
        layout.Controls.Add(location);layout.Controls.Add(folders);layout.Controls.Add(message);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        var cancel=new Button{Text="Annulla",AutoSize=true};
        foreach(var button in new[]{up,open,select,cancel})actions.Controls.Add(button);layout.Controls.Add(actions);
        folders.SelectedIndexChanged+=(_,_)=>RefreshButtons();
        folders.DoubleClick+=async(_,_)=>await OpenSelected();open.Click+=async(_,_)=>await OpenSelected();
        up.Click+=async(_,_)=>await LoadLevel(ParentFolder(current));
        select.Click+=(_,_)=>{if(loading||!validLevel)return;SelectedCollection=folders.SelectedItem as UsbCollectionChoice;SelectedFolder=SelectedCollection==null?folders.SelectedItem as UsbFolderChoice??current:null;if(SelectedFolder!=null||SelectedCollection!=null){DialogResult=DialogResult.OK;Close();}};
        cancel.Click+=(_,_)=>Close();CancelButton=cancel;
        Shown+=async(_,_)=>await LoadLevel(null);
        FormClosing+=(_,e)=>{if(loading){e.Cancel=true;closing=true;cancellation.Cancel();message.Text="Chiusura della lettura USB…";}};
        FormClosed+=(_,_)=>cancellation.Dispose();RefreshButtons();
    }
    // Synthetic UI fixture: never accesses a real device.
    public UsbFolderDialog(IReadOnlyList<UsbFolderChoice> choices):this((_,_)=>Task.FromResult(new UsbNavigationLevel(choices,[]))){}
    private Task OpenSelected()=>folders.SelectedItem is UsbFolderChoice choice?LoadLevel(choice):Task.CompletedTask;
    private static UsbFolderChoice? ParentFolder(UsbFolderChoice? choice)
    {
        if(choice==null||choice.Segments.Count<=1)return null;
        var segments=choice.Segments.Take(choice.Segments.Count-1).ToArray();
        return new(string.Join(" / ",segments.Select(segment=>segment.Name)),segments);
    }
    private async Task LoadLevel(UsbFolderChoice? target)
    {
        if(loading||closing)return;
        loading=true;validLevel=false;folders.Items.Clear();message.Text="Lettura delle cartelle di questo livello…";RefreshButtons();
        try
        {
            var level=await list(target,cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();
            var choices=level.Folders;var albums=level.Collections;
            current=target;location.Text=current?.DisplayPath??"Telefono · memorie disponibili";
            foreach(var choice in choices)folders.Items.Add(choice);
            foreach(var album in albums)folders.Items.Add(album);
            validLevel=true;message.Text=choices.Count==0&&albums.Count==0?"Nessuna sottocartella o raccolta. Puoi usare la cartella aperta.":$"{choices.Count:N0} cartelle · {albums.Count:N0} raccolte USB. Doppio clic apre una cartella; Usa seleziona.";
            if(level.Warnings is {Count:>0})message.Text+="\n"+string.Join("\n",level.Warnings);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {message.Text=ex is OperationCanceledException?"Lettura annullata.":ex.Message;}
        finally{loading=false;RefreshButtons();if(closing)Close();}
    }
    private void RefreshButtons()
    {
        folders.Enabled=!loading;up.Enabled=!loading&&current!=null;
        open.Enabled=!loading&&validLevel&&folders.SelectedItem is UsbFolderChoice;
        select.Enabled=!loading&&validLevel&&(folders.SelectedItem is UsbFolderChoice or UsbCollectionChoice||current!=null);
        select.Text=folders.SelectedItem is UsbCollectionChoice?"Usa questa raccolta":"Usa questa cartella";
    }
}
