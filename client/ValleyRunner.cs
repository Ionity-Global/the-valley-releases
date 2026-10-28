// =====================================================================================================================
// THE VALLEY v4.14 "FIELD OF HONOUR" - THE RUNNER (client side)   "The Valley.exe"
// Document: GME-2026-10-041 | Version 1.1 | 28 Oct 2026 (SAST) - v4.14: the built-in updater (ValleyUpdater.cs)
// Author: Johan Wilhelm van Antwerp | Ionity (Pty) Ltd - formerly Antwerp Designs - AEDI
// Governance: Policy 986 AED | License: AED 900 | (c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd - All rights reserved - TM2
// Web: https://www.ionity.today | Ref: https://www.ionity.co.za | Building Tomorrow, Today.
//
// The player's front door. It finds Valley World Servers - the public list (Firestore servers/, the same list as the in-game
// SERVERS tab), your own network (UDP broadcast "VALLEY?" on port 7778) and your recent servers - pings each one over TCP
// (INFO), and starts TheValley.exe connected to the one you pick (-server=host:port). HOST starts The Valley Server.exe on
// this PC and points the game at it. Painted with the game's own art (art\ folder: runner_bg.png, btn*.png, Cinzel fonts).
//
// Build (Windows, the .NET Framework 4 compiler that ships with Windows):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:valley.ico
//       /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll
//       /r:System.IO.Compression.FileSystem.dll /out:"The Valley.exe" ValleyRunner.cs ValleyUpdater.cs
// "The Valley.exe -checkupdate" (exit 10 = update available) and "-update" (install it) run without a window -> update.log
// =====================================================================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("The Valley")]
[assembly: AssemblyProduct("The Valley v4.14 FIELD OF HONOUR")]
[assembly: AssemblyCompany("Ionity (Pty) Ltd")]
[assembly: AssemblyCopyright("(c) 2018-2026 Antwerp Designs | Ionity (Pty) Ltd")]
[assembly: AssemblyVersion("4.14.0.0")]
[assembly: AssemblyFileVersion("4.14.0.0")]

// ---------------------------------------------------------------------------------------------------------------------
public class ServerRow
{
    public string Name = "", Address = "", Version = "", Source = "", World = "";
    public int Players = -1, Max = -1; public long PingMs = -1; public bool Password, Answered, Old;
    public string Key { get { return Address.ToLowerInvariant(); } }
}

/** A painted button: the forge's plate, gold Cinzel text, brighter plate under the mouse. */
public class ArtButton : Control
{
    public static Image Plate, PlateOn; public static FontFamily Face;
    public bool Primary; bool hover, down;
    public ArtButton(string text, bool primary)
    {
        Text = text; Primary = primary; Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        Rectangle r = ClientRectangle; if (down) r.Offset(0, 1);
        Image img = (hover || Primary) && PlateOn != null ? PlateOn : Plate;
        if (img != null) g.DrawImage(img, r);
        else { using (Brush b = new SolidBrush(Color.FromArgb(30, 38, 54))) g.FillRectangle(b, r); using (Pen p = new Pen(Color.FromArgb(214, 170, 82))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1); }
        if (hover && Enabled) using (Brush b = new SolidBrush(Color.FromArgb(28, 255, 220, 150))) g.FillRectangle(b, r);
        float size = Primary ? 14f : 11.5f;
        using (Font f = Face != null ? new Font(Face, size, FontStyle.Bold) : new Font("Georgia", size, FontStyle.Bold))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        using (Brush sh = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
        using (Brush tx = new SolidBrush(!Enabled ? Color.FromArgb(120, 120, 110) : hover ? Color.FromArgb(255, 236, 180) : Color.FromArgb(240, 205, 130)))
        {
            RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            RectangleF rs = rf; rs.Offset(1, 1);
            g.DrawString(Text, f, sh, rs, sf); g.DrawString(Text, f, tx, rf, sf);
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
public class RunnerForm : Form
{
    static readonly Color Ink = Color.FromArgb(18, 23, 33), Gold = Color.FromArgb(214, 170, 82), GoldHi = Color.FromArgb(245, 212, 140), Txt = Color.FromArgb(225, 220, 205), Dim = Color.FromArgb(150, 150, 140), Good = Color.FromArgb(140, 220, 140), Bad = Color.FromArgb(240, 130, 110);
    readonly string root, artDir, game, iniPath;
    readonly Dictionary<string, string> ini = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, ServerRow> rows = new Dictionary<string, ServerRow>();
    readonly object rowsGate = new object();
    PrivateFontCollection fonts = new PrivateFontCollection();
    FontFamily cinzel, marcellus;
    ListView list; TextBox addr, pass; CheckBox windowed, console; Label herald, status;
    ArtButton playBtn, soloBtn, hostBtn, connectBtn, refreshBtn, updateBtn;
    ValleyRelease release; string localVersion = ValleyUpdate.DefaultVersion; volatile bool updating, checking;   // v4.14 the updater
    volatile int refreshGen; string apiKey = "", project = "the-valley-mmo";
    System.Windows.Forms.Timer uiTimer;

    public RunnerForm(string rootDir)
    {
        root = rootDir;
        artDir = Path.Combine(root, "art"); if (!Directory.Exists(artDir)) artDir = Path.Combine(root, "Runner", "art");
        game = Path.Combine(root, "Windows", "TheValley.exe"); if (!File.Exists(game)) game = Path.Combine(root, "TheValley.exe");
        string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TheValley");
        try { Directory.CreateDirectory(appData); } catch (Exception) { }
        iniPath = Path.Combine(appData, "runner.ini");
        LoadIni(Path.Combine(root, "ValleyRunner.ini"));   // shipped settings (Firebase web key)
        LoadIni(iniPath);                                   // this player's choices
        string v; if (ini.TryGetValue("apiKey", out v)) apiKey = v.Trim(); if (ini.TryGetValue("project", out v) && v.Trim().Length > 0) project = v.Trim();

        try { foreach (string f in new string[] { "Cinzel-Bold.ttf", "Marcellus-Regular.ttf" }) { string p = Path.Combine(artDir, f); if (File.Exists(p)) fonts.AddFontFile(p); } } catch (Exception) { }
        foreach (FontFamily ff in fonts.Families) { if (ff.Name.StartsWith("Cinzel")) cinzel = ff; else if (ff.Name.StartsWith("Marcellus")) marcellus = ff; }
        ArtButton.Face = cinzel; ArtButton.Plate = Art("btn.png"); ArtButton.PlateOn = Art("btn_on.png");

        localVersion = ValleyUpdate.LocalVersion(root); ValleyUpdate.CleanOld(root);   // v4.14
        Text = "The Valley - " + localVersion;
        ClientSize = new Size(1100, 700); FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(10, 12, 18); ForeColor = Txt; DoubleBuffered = true;
        Image bg = Art("runner_bg.png"); if (bg != null) { BackgroundImage = bg; BackgroundImageLayout = ImageLayout.None; }
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
        Font body = marcellus != null ? new Font(marcellus, 10.5f) : new Font("Segoe UI", 9.5f);
        Font = body;

        // ---- left: enter the Valley
        playBtn = Btn("PLAY ON SERVER", true, 40, 336, 344, 54, delegate { Play(false); });
        soloBtn = Btn("PLAY ALONE", false, 40, 400, 344, 42, delegate { Play(true); });
        hostBtn = Btn("HOST A WORLD SERVER", false, 40, 450, 344, 42, delegate { Host(); });
        // the client download (ionity.fun) has no server: the button only shows when The Valley Server.exe is beside the runner
        // v4.14 (Johan): the Valley is played on a World Server only - every champion's account data comes from Firebase through
        // the server, so PLAY ALONE and HOST A WORLD SERVER are gone from the runner (the code stays for a later server release)
        soloBtn.Visible = false; hostBtn.Visible = false;
        windowed = Check("Windowed (1600 x 900)", 44, 404, Get("windowed") == "1");
        console = Check("Log window (for testing)", 44, 428, Get("console") == "1");
        Controls.Add(new Label { Text = "Server password (only if the server has one):", Location = new Point(44, 462), Size = new Size(340, 20), ForeColor = Dim, BackColor = Ink });
        pass = new TextBox { Location = new Point(44, 484), Size = new Size(336, 26), BackColor = Color.FromArgb(28, 35, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, UseSystemPasswordChar = true };
        Controls.Add(pass);
        status = new Label { Location = new Point(44, 522), Size = new Size(340, 80), ForeColor = Txt, BackColor = Ink, Text = "Pick a server on the right, then PLAY. Sign in on the title screen, choose your champion and ENTER THE VALLEY - you join the server as you arrive." };
        Controls.Add(status);
        // v4.14 the updater: checks Firebase (then GitHub) for a newer Valley; the button updates in place or checks again
        updateBtn = Btn("CHECKING FOR UPDATES...", false, 40, 612, 344, 44, delegate { if (release != null && ValleyUpdate.Compare(release.Version, localVersion) > 0) DoUpdate(); else ThreadPool.QueueUserWorkItem(delegate { CheckUpdate(); }); });
        updateBtn.Enabled = false;

        // ---- right: servers
        // the column titles are painted labels (a ListView header ignores the theme on many Windows builds)
        list = new ListView { Location = new Point(436, 90), Size = new Size(624, 368), View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, BackColor = Ink, ForeColor = Txt, BorderStyle = BorderStyle.None, OwnerDraw = true, HeaderStyle = ColumnHeaderStyle.None };
        int[] widths = { 214, 70, 70, 72, 180 }; string[] titles = { "SERVER", "PLAYERS", "PING", "FOUND", "ADDRESS" };
        int hx = 436;
        for (int i = 0; i < widths.Length; i++)
        {
            list.Columns.Add(titles[i], widths[i]);
            Controls.Add(new Label { Text = titles[i], Location = new Point(hx + 4, 68), Size = new Size(widths[i] - 4, 20), ForeColor = GoldHi, BackColor = Ink, Font = cinzel != null ? new Font(cinzel, 9.5f, FontStyle.Bold) : new Font("Segoe UI", 9f, FontStyle.Bold), UseCompatibleTextRendering = true });
            hx += widths[i];
        }
        list.DrawItem += delegate (object o, DrawListViewItemEventArgs e) { };
        list.DrawSubItem += delegate (object o, DrawListViewSubItemEventArgs e)
        {
            bool sel = e.Item.Selected;
            using (Brush b = new SolidBrush(sel ? Color.FromArgb(70, 56, 26) : (e.ItemIndex % 2 == 0 ? Ink : Color.FromArgb(22, 28, 40)))) e.Graphics.FillRectangle(b, e.Bounds);
            Color c = e.ColumnIndex == 0 ? (sel ? Color.White : GoldHi) : e.Item.ForeColor;
            if (e.ColumnIndex == 2) { long ms; if (long.TryParse(e.SubItem.Text.Replace(" ms", ""), out ms)) c = ms < 80 ? Good : ms < 180 ? Color.FromArgb(230, 210, 120) : Bad; }
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), c, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        };
        list.SelectedIndexChanged += delegate { if (list.SelectedItems.Count > 0) addr.Text = list.SelectedItems[0].SubItems[4].Text; };
        list.DoubleClick += delegate { if (list.SelectedItems.Count > 0) Play(false); };
        SetRowHeight(list, 26);
        Controls.Add(list);
        addr = new TextBox { Location = new Point(436, 474), Size = new Size(372, 28), BackColor = Color.FromArgb(28, 35, 50), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11f) };
        addr.Text = Get("lastAddress");
        addr.KeyDown += delegate (object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Probe(addr.Text.Trim(), "typed", true); } };
        Controls.Add(addr);
        connectBtn = Btn("CHECK", false, 816, 470, 118, 36, delegate { Probe(addr.Text.Trim(), "typed", true); });
        refreshBtn = Btn("REFRESH", false, 942, 470, 118, 36, delegate { RefreshAll(); });

        // ---- herald
        herald = new Label { Location = new Point(436, 578), Size = new Size(624, 90), ForeColor = Txt, BackColor = Ink,
            Text = "v4.14 FIELD OF HONOUR - the runner keeps your Valley up to date (UPDATE on the left). Play together. Everyone on a World Server walks the same Valley: /s say (40 m), /w world, /p party, /t <name> whisper, " +
                   "/invite <name> to make a party of up to 10, /who to see who is on. Your champion lives on your Valley account - sign in on the title screen." };
        Controls.Add(herald);

        uiTimer = new System.Windows.Forms.Timer { Interval = 500 }; uiTimer.Tick += delegate { RedrawRows(); }; uiTimer.Start();
        Shown += delegate { if (!File.Exists(game)) SetStatus("TheValley.exe was not found - keep this runner beside the 'Windows' folder of the release.", true); RefreshAll(); ThreadPool.QueueUserWorkItem(delegate { CheckUpdate(); }); };
        FormClosing += delegate { SaveIni(); };
    }

    // ------------------------------------------------------------------------------------------------- small helpers
    Image Art(string n) { try { string p = Path.Combine(artDir, n); return File.Exists(p) ? Image.FromFile(p) : null; } catch (Exception) { return null; } }
    ArtButton Btn(string t, bool primary, int x, int y, int w, int h, EventHandler click) { ArtButton b = new ArtButton(t, primary) { Location = new Point(x, y), Size = new Size(w, h) }; b.Click += click; Controls.Add(b); return b; }
    CheckBox Check(string t, int x, int y, bool on) { CheckBox c = new CheckBox { Text = t, Location = new Point(x, y), Size = new Size(330, 22), Checked = on, ForeColor = Txt, BackColor = Ink, FlatStyle = FlatStyle.Flat }; Controls.Add(c); return c; }
    static void SetRowHeight(ListView lv, int h) { ImageList il = new ImageList { ImageSize = new Size(1, h) }; lv.SmallImageList = il; }
    string Get(string k) { string v; return ini.TryGetValue(k, out v) ? v : ""; }
    void SetStatus(string s, bool bad) { if (InvokeRequired) { BeginInvoke(new Action(delegate { SetStatus(s, bad); })); return; } status.Text = s; status.ForeColor = bad ? Bad : Txt; }

    void LoadIni(string p)
    {
        try { if (!File.Exists(p)) return; foreach (string l in File.ReadAllLines(p)) { string t = l.Trim(); if (t.Length == 0 || t.StartsWith(";")) continue; int i = t.IndexOf('='); if (i > 0) ini[t.Substring(0, i).Trim()] = t.Substring(i + 1).Trim(); } }
        catch (Exception) { }
    }
    void SaveIni()
    {
        try
        {
            ini["windowed"] = windowed.Checked ? "1" : "0"; ini["console"] = console.Checked ? "1" : "0"; ini["lastAddress"] = addr.Text.Trim();
            StringBuilder b = new StringBuilder("; The Valley runner - your choices (c) Ionity (Pty) Ltd\r\n");
            foreach (KeyValuePair<string, string> kv in ini) if (kv.Key != "apiKey" && kv.Key != "project") b.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
            File.WriteAllText(iniPath, b.ToString());
        }
        catch (Exception) { }
    }
    List<string> Recent() { return Get("recent").Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList(); }
    void AddRecent(string a) { List<string> r = Recent(); r.RemoveAll(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase)); r.Insert(0, a); ini["recent"] = string.Join(",", r.Take(8).ToArray()); SaveIni(); }

    static string Normalise(string a)
    {
        a = (a ?? "").Trim(); if (a.Length == 0) return "";
        int c = a.LastIndexOf(':'); int port;
        if (c > 0 && int.TryParse(a.Substring(c + 1), out port)) return a;
        return a + ":7777";
    }
    static bool Split(string a, out string host, out int port)
    {
        host = a; port = 7777; int c = a.LastIndexOf(':');
        if (c > 0 && int.TryParse(a.Substring(c + 1), out port)) host = a.Substring(0, c);
        host = host.Trim('[', ']');
        return host.Length > 0 && port > 0 && port < 65536;
    }

    // ------------------------------------------------------------------------------------------------- finding servers
    ServerRow Row(string address, string source)
    {
        address = Normalise(address); if (address.Length == 0) return null;
        lock (rowsGate)
        {
            ServerRow r; if (!rows.TryGetValue(address.ToLowerInvariant(), out r)) { r = new ServerRow { Address = address, Source = source }; rows[r.Key] = r; }
            else if (r.Source != source && !r.Source.Contains(source)) r.Source = source == "lan" ? "lan" : r.Source;
            return r;
        }
    }

    void RefreshAll()
    {
        int gen = ++refreshGen;
        lock (rowsGate) rows.Clear();
        list.Items.Clear();
        SetStatus("Looking for World Servers...", false);
        foreach (string a in Recent()) Probe(a, "recent", false);
        Probe("127.0.0.1:7777", "this PC", false);
        ThreadPool.QueueUserWorkItem(delegate { Lan(gen); });
        ThreadPool.QueueUserWorkItem(delegate { Public(gen); });
    }

    void Probe(string address, string source, bool announce)
    {
        ServerRow r = Row(address, source); if (r == null) { if (announce) SetStatus("Type a server address, e.g. 192.168.1.20:7777", true); return; }
        if (announce) SetStatus("Checking " + r.Address + "...", false);
        ThreadPool.QueueUserWorkItem(delegate
        {
            string host; int port;
            if (!Split(r.Address, out host, out port)) return;
            try
            {
                using (TcpClient t = new TcpClient())
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    IAsyncResult ar = t.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(3000)) throw new TimeoutException();
                    t.EndConnect(ar);
                    long connectMs = sw.ElapsedMilliseconds;
                    NetworkStream ns = t.GetStream(); ns.ReadTimeout = 3000;
                    byte[] q = Encoding.UTF8.GetBytes("INFO\n"); sw.Restart(); ns.Write(q, 0, q.Length);
                    string line = ReadLine(ns);
                    long ms = Math.Min(connectMs, sw.ElapsedMilliseconds);
                    string[] f = line.Split('|');
                    if (f.Length >= 8 && f[0] == "INFO")
                    {
                        r.Name = f[2]; r.Players = ParseInt(f[3]); r.Max = ParseInt(f[4]); r.Version = f[5]; r.Password = f[6] == "1"; r.World = f[7];
                        r.PingMs = ms; r.Answered = true; r.Old = false;
                        if (announce) SetStatus(r.Name + " answered in " + ms + " ms - " + r.Players + " / " + r.Max + " players." + (r.Password ? " It needs a password." : "") + " Press PLAY ON SERVER.", false);
                    }
                    else throw new Exception("not a Valley World Server");
                }
            }
            catch (Exception ex)
            {
                r.Answered = false; r.PingMs = -1;
                if (announce) SetStatus(r.Address + " did not answer (" + (ex is TimeoutException ? "timed out" : ex.Message) + "). Is the server running, and is TCP " + port + " open?", true);
            }
        });
    }

    static int ParseInt(string s) { int v; return int.TryParse(s, out v) ? v : -1; }
    static string ReadLine(NetworkStream ns)
    {
        MemoryStream m = new MemoryStream(); int b;
        while ((b = ns.ReadByte()) >= 0 && b != '\n' && m.Length < 4096) m.WriteByte((byte)b);
        return Encoding.UTF8.GetString(m.ToArray()).TrimEnd('\r');
    }

    void Lan(int gen)
    {
        try
        {
            using (UdpClient u = new UdpClient())
            {
                u.EnableBroadcast = true; u.Client.ReceiveTimeout = 400;
                byte[] q = Encoding.UTF8.GetBytes("VALLEY?");
                List<IPAddress> targets = new List<IPAddress> { IPAddress.Broadcast };
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        byte[] ip = ua.Address.GetAddressBytes(), mask = ua.IPv4Mask.GetAddressBytes(), bc = new byte[4];
                        for (int i = 0; i < 4; i++) bc[i] = (byte)(ip[i] | ~mask[i]);
                        targets.Add(new IPAddress(bc));
                    }
                }
                foreach (IPAddress t in targets.Distinct()) try { u.Send(q, q.Length, new IPEndPoint(t, 7778)); } catch (Exception) { }
                HashSet<string> mine = new HashSet<string>();   // this PC answers on every network card - 127.0.0.1 already covers it
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses) mine.Add(ua.Address.ToString());
                DateTime end = DateTime.UtcNow.AddMilliseconds(1500);
                while (DateTime.UtcNow < end && gen == refreshGen)
                {
                    try
                    {
                        IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                        string[] f = Encoding.UTF8.GetString(u.Receive(ref from)).Split('|');
                        if (f.Length >= 3 && f[0] == "VALLEY!") Probe((mine.Contains(from.Address.ToString()) ? "127.0.0.1" : from.Address.ToString()) + ":" + f[2], mine.Contains(from.Address.ToString()) ? "this PC" : "lan", false);
                    }
                    catch (SocketException) { }
                }
            }
        }
        catch (Exception) { }
    }

    void Public(int gen)
    {
        if (apiKey.Length == 0) { SetStatus("No apiKey in ValleyRunner.ini - only servers on your network and typed addresses are shown.", true); return; }
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
            string body = Http("POST", "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=" + apiKey, "{\"returnSecureToken\":true}", null);
            Dictionary<string, object> au = js.Deserialize<Dictionary<string, object>>(body);
            string token = au != null && au.ContainsKey("idToken") ? (string)au["idToken"] : "";
            if (token.Length == 0) { SetStatus("The public server list is unavailable right now.", true); return; }
            string list2 = Http("GET", "https://firestore.googleapis.com/v1/projects/" + project + "/databases/(default)/documents/servers?pageSize=100", null, token);
            Dictionary<string, object> doc = js.Deserialize<Dictionary<string, object>>(list2);
            int n = 0;
            if (doc != null && doc.ContainsKey("documents"))
                foreach (object o in (ArrayList)doc["documents"])
                {
                    Dictionary<string, object> d = o as Dictionary<string, object>; if (d == null || !d.ContainsKey("fields")) continue;
                    Dictionary<string, object> f = (Dictionary<string, object>)d["fields"];
                    string address = Fs(f, "address"), name = Fs(f, "name"), version = Fs(f, "version"), updated = Fs(f, "updated");
                    DateTime t; if (!DateTime.TryParse(updated, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t) || (DateTime.UtcNow - t).TotalSeconds > 180) continue;
                    if (address.Length == 0 || gen != refreshGen) continue;
                    ServerRow r = Row(address, "public"); if (r == null) continue;
                    r.Name = name; r.Version = version; r.Players = ParseInt(Fs(f, "players")); r.Max = ParseInt(Fs(f, "maxPlayers"));
                    r.Old = string.CompareOrdinal(version, "4.13") < 0 && Fs(f, "kind") != "dedicated";
                    if (!r.Old) Probe(address, "public", false);
                    n++;
                }
            SetStatus(n == 0 ? "No public World Servers right now - host one, or type a friend's address." : "Found " + n + " public server(s). Pick one and press PLAY ON SERVER.", false);
        }
        catch (Exception ex) { SetStatus("Could not read the public server list (" + ex.Message + ").", true); }
    }

    static string Fs(Dictionary<string, object> f, string k)
    {
        if (!f.ContainsKey(k)) return ""; Dictionary<string, object> v = f[k] as Dictionary<string, object>; if (v == null) return "";
        foreach (KeyValuePair<string, object> kv in v) return Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
        return "";
    }

    static string Http(string verb, string url, string body, string bearer)
    {
        HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url); r.Method = verb; r.Timeout = 12000; r.UserAgent = "TheValleyRunner/4.14";
        if (bearer != null) r.Headers["Authorization"] = "Bearer " + bearer;
        if (body != null) { byte[] b = Encoding.UTF8.GetBytes(body); r.ContentType = "application/json"; r.ContentLength = b.Length; using (Stream s = r.GetRequestStream()) s.Write(b, 0, b.Length); }
        using (WebResponse resp = r.GetResponse()) using (StreamReader rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return rd.ReadToEnd();
    }

    void RedrawRows()
    {
        List<ServerRow> all; lock (rowsGate) all = rows.Values.Where(r => r.Answered || r.Source == "public").OrderByDescending(r => r.Answered).ThenBy(r => r.PingMs < 0 ? 99999 : r.PingMs).ToList();
        string sel = list.SelectedItems.Count > 0 ? list.SelectedItems[0].SubItems[4].Text : addr.Text.Trim();
        list.BeginUpdate();
        // update in place so the selection and scroll position survive
        while (list.Items.Count > all.Count) list.Items.RemoveAt(list.Items.Count - 1);
        for (int i = 0; i < all.Count; i++)
        {
            ServerRow r = all[i];
            string[] cells = {
                (r.Name.Length > 0 ? r.Name : r.Address) + (r.Password ? "  (password)" : "") + (r.Old ? "  (old version)" : ""),
                r.Players >= 0 ? r.Players + " / " + r.Max : "-",
                r.PingMs >= 0 ? r.PingMs + " ms" : (r.Old ? "-" : "no answer"),
                r.Source, r.Address };
            ListViewItem it;
            if (i < list.Items.Count) { it = list.Items[i]; for (int c = 0; c < cells.Length; c++) if (it.SubItems[c].Text != cells[c]) it.SubItems[c].Text = cells[c]; }
            else { it = new ListViewItem(cells); list.Items.Add(it); }
            it.ForeColor = r.Answered ? Txt : Dim;
            if (string.Equals(r.Address, Normalise(sel), StringComparison.OrdinalIgnoreCase) && !it.Selected) it.Selected = true;
        }
        list.EndUpdate();
    }

    // ------------------------------------------------------------------------------------------------- starting things
    void Play(bool alone)
    {
        if (alone) { SetStatus("The Valley is played on a World Server - pick one on the right.", true); return; }   // v4.14
        if (!File.Exists(game)) { SetStatus("TheValley.exe was not found next to this runner.", true); return; }
        if (updating) { SetStatus("An update is being installed - the game starts when it is done.", true); return; }
        if (release != null && release.MinVersion.Length > 0 && ValleyUpdate.Compare(localVersion, release.MinVersion) < 0)
        { SetStatus("Your Valley (" + localVersion + ") is too old for the World Servers (they need " + release.MinVersion + " or newer). Press UPDATE on the left.", true); return; }
        StringBuilder args = new StringBuilder();
        string a = Normalise(addr.Text);
        if (!alone)
        {
            if (a.Length == 0) { SetStatus("Pick a server in the list (or type an address) first.", true); return; }
            ServerRow r; lock (rowsGate) rows.TryGetValue(a.ToLowerInvariant(), out r);
            if (r != null && r.Old) { SetStatus("That server runs an older Valley (" + r.Version + ") without a World Server.", true); return; }
            if (r != null && r.Version.Length > 0 && ValleyUpdate.Compare(r.Version, localVersion) > 0)
            {   // v4.14 the server is newer than this client: update first
                SetStatus(r.Name + " runs The Valley " + r.Version + " - you have " + localVersion + ". Press UPDATE on the left first.", true);
                if (release == null && !checking) ThreadPool.QueueUserWorkItem(delegate { CheckUpdate(); });
                return;
            }
            if (r != null && r.Password && pass.Text.Length == 0) { SetStatus(r.Name + " needs a password - type it on the left.", true); return; }
            args.Append("\"-server=").Append(a).Append("\"");
            if (pass.Text.Length > 0) args.Append(" \"-serverpass=").Append(pass.Text.Replace("\"", "")).Append("\"");
            AddRecent(a);
        }
        if (windowed.Checked) args.Append(" -windowed -ResX=1600 -ResY=900");
        if (console.Checked) args.Append(" -log");
        try
        {
            Process.Start(new ProcessStartInfo(game, args.ToString().Trim()) { WorkingDirectory = Path.GetDirectoryName(game), UseShellExecute = false });
            SetStatus(alone ? "The Valley is starting - on your own." : "The Valley is starting - sign in, choose your champion and ENTER THE VALLEY to join " + a + ".", false);
            SaveIni();
        }
        catch (Exception ex) { SetStatus("Could not start the game: " + ex.Message + " - run Windows\\Engine\\Extras\\Redist\\en-us\\UEPrereqSetup_x64.exe once.", true); }
    }

    // ------------------------------------------------------------------------------------------------- v4.14 updates
    void CheckUpdate()
    {
        if (checking || updating) return; checking = true;
        try
        {
            BeginInvoke(new Action(delegate { updateBtn.Text = "CHECKING FOR UPDATES..."; updateBtn.Enabled = false; }));
            string err; ValleyRelease r = ValleyUpdate.Fetch(apiKey, project, out err);
            BeginInvoke(new Action(delegate
            {
                release = r; updateBtn.Enabled = true;
                if (r == null) { updateBtn.Text = "UPDATES OFFLINE - CHECK AGAIN"; updateBtn.Primary = false; updateBtn.Invalidate(); return; }
                if (ValleyUpdate.Compare(r.Version, localVersion) > 0)
                {
                    updateBtn.Text = "UPDATE TO " + r.Version; updateBtn.Primary = true; updateBtn.Invalidate();
                    bool must = r.MinVersion.Length > 0 && ValleyUpdate.Compare(localVersion, r.MinVersion) < 0;
                    SetStatus("The Valley " + r.Version + (r.Title.Length > 0 ? " \"" + r.Title + "\"" : "") + " is out (you have " + localVersion + ")." + (must ? " The World Servers need it - press UPDATE." : " Press UPDATE to install it."), must);
                }
                else { updateBtn.Text = "UP TO DATE  -  " + localVersion; updateBtn.Primary = false; updateBtn.Invalidate(); }
            }));
        }
        catch (Exception) { }
        finally { checking = false; }
    }

    void DoUpdate()
    {
        if (updating || release == null) return;
        if (ValleyUpdate.GameRunning()) { SetStatus("Close The Valley first - the update replaces the game's files.", true); return; }
        List<ValleyPatch> chain = ValleyUpdate.Chain(release, localVersion);
        if (chain == null)
        {
            SetStatus("There is no patch from " + localVersion + " to " + release.Version + " - the full download opens in your browser (run TheValley_Setup.exe beside the parts).", true);
            string u = release.FullSetup.Length > 0 ? release.FullSetup : release.FullFolder; if (u.Length > 0) try { Process.Start(new ProcessStartInfo(u) { UseShellExecute = true }); } catch (Exception) { }
            return;
        }
        updating = true; updateBtn.Enabled = false; playBtn.Enabled = false;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                string v = ValleyUpdate.Apply(root, chain, delegate (string text, int pct) { SetStatus(text, false); BeginInvoke(new Action(delegate { updateBtn.Text = "UPDATING  " + pct + "%"; })); });
                SetStatus("The Valley is now " + v + ". The runner restarts...", false);
                Thread.Sleep(1200);
                BeginInvoke(new Action(delegate
                {
                    try { Process.Start(new ProcessStartInfo(Path.Combine(root, "The Valley.exe")) { WorkingDirectory = root, UseShellExecute = false }); } catch (Exception) { }
                    Close();
                }));
            }
            catch (Exception ex)
            {
                updating = false;
                SetStatus("The update stopped: " + ex.Message + " - nothing was changed that the next try will not fix. Press UPDATE again.", true);
                BeginInvoke(new Action(delegate { updateBtn.Enabled = true; playBtn.Enabled = true; updateBtn.Text = "UPDATE TO " + release.Version; }));
            }
        });
    }

    void Host()
    {
        if (!hostBtn.Visible) return;   // v4.14 hosting is not offered in the client release
        string exe = Path.Combine(root, "The Valley Server.exe");
        if (!File.Exists(exe)) exe = Path.Combine(root, "Server", "The Valley Server.exe");
        if (!File.Exists(exe)) { SetStatus("The Valley Server.exe was not found beside the runner.", true); return; }
        bool running = Process.GetProcessesByName("The Valley Server").Length > 0;
        if (!running)
        {
            try { Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true }); }
            catch (Exception ex) { SetStatus("Could not start the World Server: " + ex.Message, true); return; }
        }
        addr.Text = "127.0.0.1:7777";
        SetStatus(running ? "Your World Server is already running - press PLAY ON SERVER." : "Your World Server is starting (its window shows players and the log). Press PLAY ON SERVER to join it. Friends: your public address is on the server window; forward TCP 7777.", false);
        ThreadPool.QueueUserWorkItem(delegate { for (int i = 0; i < 6; i++) { Thread.Sleep(1000); Probe("127.0.0.1:7777", "this PC", false); } });
    }
}

// ---------------------------------------------------------------------------------------------------------------------
public static class ValleyRunner
{
    [STAThread]
    public static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string root = AppDomain.CurrentDomain.BaseDirectory;
        // "The Valley.exe -direct" keeps the old behaviour: start the game at once, no runner window
        // v4.14: only with a server (-server=host:port) - the Valley is not played alone
        if (args.Any(a => a.Equals("-direct", StringComparison.OrdinalIgnoreCase) || a.Equals("/direct", StringComparison.OrdinalIgnoreCase)) && args.Any(a => a.ToLowerInvariant().StartsWith("-server=")))
        {
            string g = Path.Combine(root, "Windows", "TheValley.exe"); if (!File.Exists(g)) g = Path.Combine(root, "TheValley.exe");
            try { Process.Start(new ProcessStartInfo(g, string.Join(" ", args.Where(a => !a.ToLowerInvariant().EndsWith("direct")).ToArray())) { WorkingDirectory = Path.GetDirectoryName(g), UseShellExecute = false }); return 0; }
            catch (Exception ex) { MessageBox.Show("Could not start the game:\n" + ex.Message, "The Valley", MessageBoxButtons.OK, MessageBoxIcon.Error); return 2; }
        }
        // v4.14 "The Valley.exe -update" updates without a window (scripts, CI, support): writes update.log, exit 0 = up to date or updated
        if (args.Any(a => a.Equals("-update", StringComparison.OrdinalIgnoreCase) || a.Equals("-checkupdate", StringComparison.OrdinalIgnoreCase)))
        {
            bool apply = args.Any(a => a.Equals("-update", StringComparison.OrdinalIgnoreCase));
            string logp = Path.Combine(root, "update.log");
            Action<string> say = delegate (string t) { try { File.AppendAllText(logp, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + t + "\r\n"); } catch (Exception) { } };
            try
            {
                Dictionary<string, string> kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try { foreach (string l in File.ReadAllLines(Path.Combine(root, "ValleyRunner.ini"))) { int i = l.IndexOf('='); if (i > 0 && !l.TrimStart().StartsWith(";")) kv[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim(); } } catch (Exception) { }
                string key, proj; kv.TryGetValue("apiKey", out key); if (!kv.TryGetValue("project", out proj)) proj = "the-valley-mmo";
                string have = ValleyUpdate.LocalVersion(root), err;
                ValleyRelease rel = ValleyUpdate.Fetch(key ?? "", proj, out err);
                if (rel == null) { say("check failed: " + err); return 3; }
                say("installed " + have + ", latest " + rel.Version + " (min " + rel.MinVersion + ", from " + rel.Source + ")");
                if (ValleyUpdate.Compare(rel.Version, have) <= 0) { say("up to date"); return 0; }
                if (!apply) return 10;
                if (ValleyUpdate.GameRunning()) { say("the game is running - close it first"); return 4; }
                List<ValleyPatch> chain = ValleyUpdate.Chain(rel, have);
                if (chain == null) { say("no patch chain from " + have + " - full download needed: " + rel.FullSetup); return 5; }
                int lastPct = -1;
                string v = ValleyUpdate.Apply(root, chain, delegate (string t, int pct) { if (pct / 10 != lastPct / 10 || pct >= 82) { say(pct + "% " + t); lastPct = pct; } });
                say("updated to " + v); return 0;
            }
            catch (Exception ex) { say("update failed: " + ex.Message); return 2; }
        }
        Application.Run(new RunnerForm(root));
        return 0;
    }
}
