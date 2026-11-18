using QRCoder;
using System.Diagnostics;
using System.Net;

namespace Porto.Lan;

internal sealed class ReceiveWindow : Form
{
    readonly Button folder = ActionButton("Scegli cartella");
    readonly Button listen = ActionButton("Avvia ricezione");
    readonly Button open = ActionButton("Apri copie");
    readonly Button accept = ActionButton("Accetta");
    readonly Button refuse = ActionButton("Rifiuta");
    readonly ComboBox network = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    readonly Label folderText = Line("Scegli dove salvare foto e video.");
    readonly Label device = Line("");
    readonly Label progress = Line("");
    readonly PictureBox qr = new() { SizeMode=PictureBoxSizeMode.AutoSize, Visible=false };
    readonly TextBox pairing = new() { ReadOnly=true, Visible=false, Width=590 };
    readonly Label sender = Line("");
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 200 };
    string? path, lastCopyFolder, nextMessage;
    Receiver? receiver;
    TaskCompletionSource<bool>? decision;
    bool busy, closing, stoppedForClose;

    static Label Line(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(610, 0), Margin = new Padding(0, 5, 0, 5) };
    static Button ActionButton(string text) => new() { Text = text, AutoSize = true, MinimumSize = new Size(110, 35) };
    static FlowLayoutPanel Row(params Control[] items)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = Padding.Empty };
        row.Controls.AddRange(items);
        return row;
    }

    public ReceiveWindow()
    {
        Text = "Porto · Album iPhone · Prova 1";
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(680, 755);
        MinimumSize = new Size(520, 460);
        StartPosition = FormStartPosition.CenterScreen;
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), AutoScroll = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var title = Line("Ricevi un album da Porto per iPhone");
        title.Font = new Font(Font.FontFamily, 20, FontStyle.Bold);
        device.Font = new Font(Font, FontStyle.Bold);
        body.Controls.AddRange([title, Line("Collega telefono e PC alla stessa rete privata."),
            Row(folder), folderText, Row(Line("Indirizzo del PC"), network),
            Line("Con Avvia ricezione, Porto accetta richieste dalla rete scelta. Autorizzi ogni album qui sul PC."),
            Row(listen, open), device, progress, sender, Row(accept, refuse), qr, pairing]);
        Controls.Add(body);
        foreach (var ip in Receiver.Addresses()) network.Items.Add(ip);
        if (network.Items.Count > 0) network.SelectedIndex = 0;
        listen.Enabled = open.Enabled = false;
        accept.Visible = refuse.Visible = false;
        if (network.Items.Count == 0) progress.Text = "Collega il PC alla rete privata e riapri Porto.";
        folder.Click += (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Scegli la cartella dove ricevere i file", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            path = dialog.SelectedPath; folderText.Text = path;
            listen.Enabled = network.SelectedItem != null;
        };
        listen.Click += async (_, _) => await Toggle();
        accept.Click += (_, _) => decision?.TrySetResult(true);
        refuse.Click += (_, _) => decision?.TrySetResult(false);
        open.Click += (_, _) =>
        {
            if (lastCopyFolder != null && Directory.Exists(lastCopyFolder))
                Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { lastCopyFolder }, UseShellExecute = false });
        };
        refresh.Tick += (_, _) =>
        {
            if (Interlocked.Exchange(ref nextMessage, null) is { } message) progress.Text = message;
            if (receiver?.LastFolder is { } saved) { lastCopyFolder = saved; open.Enabled = true; }
        };
        refresh.Start();
        FormClosing += async (_, e) =>
        {
            if (stoppedForClose || receiver == null && !busy) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            if (!busy) await Toggle();
        };
        FormClosed += (_, _) => refresh.Dispose();
    }

    async Task<bool> Confirm(string request, CancellationToken token)
    {
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => answer.TrySetCanceled(token));
        void Update(Action action) { if (IsHandleCreated && !IsDisposed) BeginInvoke(action); }
        Update(() =>
        {
            if (answer.Task.IsCompleted) return;
            decision = answer;
            sender.Text = request;
            accept.Visible = refuse.Visible = true;
        });
        try { return await answer.Task; }
        finally
        {
            Update(() =>
            {
                if (decision != answer) return;
                accept.Visible = refuse.Visible = false;
                sender.Text = ""; decision = null;
            });
        }
    }

    async Task Toggle()
    {
        if (busy) return;
        busy = true;
        listen.Enabled = folder.Enabled = network.Enabled = false;
        try
        {
            if (receiver == null && !closing && path != null && network.SelectedItem is IPAddress address)
            {
                receiver = new Receiver(path, address, 53317, Confirm, message => Interlocked.Exchange(ref nextMessage, message));
                await receiver.Start();
                device.Text = "Apri Fotocamera sull’iPhone, inquadra il QR e tocca Apri in Porto.";
                using var generator = new QRCodeGenerator();
                using var code = generator.CreateQrCode(receiver.Pair,QRCodeGenerator.ECCLevel.M);
                using var renderer = new PngByteQRCode(code);
                using var imageData = new MemoryStream(renderer.GetGraphic(4));
                using var generatedImage = Image.FromStream(imageData);
                qr.Image = new Bitmap(generatedImage);
                qr.Visible = pairing.Visible = true;
                pairing.Text = receiver.Pair;
                progress.Text = "In attesa della richiesta da Porto…";
            }
            else await Stop();
        }
        catch (Exception ex)
        {
            await Stop();
            progress.Text = $"Ricezione indisponibile. {ex.GetBaseException().Message}";
        }
        finally
        {
            busy = false;
            listen.Text = receiver == null ? "Avvia ricezione" : "Ferma ricezione";
            listen.Enabled = !closing && path != null && network.SelectedItem != null;
            folder.Enabled = network.Enabled = receiver == null;
            if (closing)
            {
                if (receiver != null) await Stop();
                stoppedForClose = true;
                Close();
            }
        }
    }

    async Task Stop()
    {
        if (receiver == null) return;
        decision?.TrySetResult(false);
        lastCopyFolder = receiver.LastFolder ?? lastCopyFolder;
        await receiver.DisposeAsync();
        receiver = null;
        qr.Image?.Dispose(); qr.Image = null; qr.Visible = pairing.Visible = false; pairing.Text = "";
        Interlocked.Exchange(ref nextMessage, null);
        device.Text = ""; progress.Text = "Ricezione terminata.";
    }
}
