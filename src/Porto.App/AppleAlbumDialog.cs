using Porto.Apple;

namespace Porto.App;

internal sealed class AppleAlbumDialog : Form
{
    private sealed record Row(AppleAlbum Album)
    {
        public override string ToString()=>Album.IsSelectable?$"{Album.Name} — {Album.PhotoCount:N0} foto · {Album.VideoCount:N0} video":
            $"{Album.Name} — NON SELEZIONABILE: {Album.UnresolvedAssociations:N0} associazioni non risolte · riconosciuti {Album.PhotoCount:N0} foto e {Album.VideoCount:N0} video";
    }
    private readonly ListBox albums=new(){Dock=DockStyle.Fill,HorizontalScrollbar=true,IntegralHeight=false};
    public AppleAlbum? SelectedAlbum=>(albums.SelectedItem as Row)?.Album;
    public AppleAlbumDialog(IReadOnlyList<AppleAlbum> choices,int unresolvedAssociations=0)
    {
        Text="Scegli un album di Foto";ClientSize=new(660,430);MinimumSize=new(480,300);
        StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;Font=new("Segoe UI",10);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=4};Controls.Add(layout);
        layout.ColumnStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(new Label{Text="Album letti dal catalogo Foto. Verrà copiato il tipo scelto (foto/video).\nNascosti ed Eliminati di recente sono esclusi; gli originali iCloud possono mancare."+
            (unresolvedAssociations>0?$"\nAssociazioni non risolte: {unresolvedAssociations:N0}. Gli album coinvolti non sono selezionabili.":""),AutoSize=true,Dock=DockStyle.Fill});
        var filter=new TextBox{PlaceholderText="Cerca un album per nome",Dock=DockStyle.Fill};layout.Controls.Add(filter);layout.Controls.Add(albums);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        var select=new Button{Text="Usa questo album",AutoSize=true,Enabled=false,DialogResult=DialogResult.OK};
        var cancel=new Button{Text="Annulla",AutoSize=true,DialogResult=DialogResult.Cancel};actions.Controls.Add(select);actions.Controls.Add(cancel);layout.Controls.Add(actions);
        void Fill(){albums.BeginUpdate();try{albums.Items.Clear();foreach(var album in choices.Where(a=>a.Name.Contains(filter.Text,StringComparison.CurrentCultureIgnoreCase)))albums.Items.Add(new Row(album));}finally{albums.EndUpdate();}}
        filter.TextChanged+=(_,_)=>Fill();albums.SelectedIndexChanged+=(_,_)=>select.Enabled=SelectedAlbum is {IsSelectable:true};
        Fill();AcceptButton=select;CancelButton=cancel;
    }
    internal void VerifySelectionForSmoke()
    {
        var select=(Button)AcceptButton!;
        for(int i=0;i<albums.Items.Count;i++)
        {
            albums.SelectedIndex=i;
            if(select.Enabled!=SelectedAlbum!.IsSelectable)throw new InvalidOperationException("Protezione selezione album incoerente.");
            if(!SelectedAlbum.IsSelectable)
            {
                select.PerformClick();
                if(DialogResult==DialogResult.OK)throw new InvalidOperationException("Album non verificabile accettato.");
            }
        }
    }
}
