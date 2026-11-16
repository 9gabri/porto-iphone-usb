using Porto.Core;

namespace Porto.App;

internal sealed class UsbFolderDialog : Form
{
    private readonly ListBox folders=new(){Dock=DockStyle.Fill,HorizontalScrollbar=true,IntegralHeight=false};
    public UsbFolderChoice? SelectedFolder=>folders.SelectedItem as UsbFolderChoice;
    public UsbFolderDialog(IReadOnlyList<UsbFolderChoice> choices)
    {
        Text="Scegli una cartella del dispositivo";ClientSize=new(660,450);MinimumSize=new(450,300);
        StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;Font=new("Segoe UI",10);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=3};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(layout);
        layout.Controls.Add(new Label{Text="Cartelle fisiche USB con percorso univoco, comprese le sottocartelle.\nGli album delle app Foto o Google Foto potrebbero non comparire.",AutoSize=true,Dock=DockStyle.Fill,Padding=new(0,0,0,12)});
        layout.Controls.Add(folders);foreach(var choice in choices)folders.Items.Add(choice);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};
        var select=new Button{Text="Usa questa cartella",AutoSize=true,Enabled=false,DialogResult=DialogResult.OK};
        var cancel=new Button{Text="Annulla",AutoSize=true,DialogResult=DialogResult.Cancel};
        actions.Controls.Add(select);actions.Controls.Add(cancel);layout.Controls.Add(actions);
        folders.SelectedIndexChanged+=(_,_)=>select.Enabled=SelectedFolder!=null;
        AcceptButton=select;CancelButton=cancel;
    }
}
