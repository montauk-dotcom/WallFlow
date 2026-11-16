using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Wallflow.Core;

namespace Wallflow.Widgets;

/// <summary>Creates, refreshes and tears down all desktop widgets.</summary>
public sealed class WidgetHost
{
    ClockWidget? _clock;
    WeatherWidget? _weather;
    CalendarWidget? _calendar;
    PlayerWidget? _player;
    TodoWidget? _todo;
    readonly List<NoteWidget> _notes = new();
    readonly List<FolderWidget> _folders = new();
    readonly List<FileSystemWatcher> _watchers = new();
    readonly DispatcherTimer _debounce = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1.5) };
    HwndSource? _msgWindow;
    int _taskbarCreated;

    public bool EditMode { get; private set; }
    public event Action? EditModeChanged;

    static AppSettings S => SettingsStore.Current;

    public void Start()
    {
        // icons hidden by default (also once for people upgrading); can be switched back on anytime
        if (!S.IconsDefaultApplied) { S.HideDesktopIcons = true; S.IconsDefaultApplied = true; SettingsStore.Save(); }
        SyncFolders();
        PrepareScreen();
        RebuildAll();
        if (S.HideDesktopIcons) Native.SetDesktopIconsVisible(false);

        // new shortcuts on the desktop → put them into folders automatically
        _debounce.Tick += (_, _) => { _debounce.Stop(); if (SyncFolders()) RebuildFolders(); };
        foreach (var dir in Shortcuts.DesktopDirs().Where(Directory.Exists))
        {
            try
            {
                var w = new FileSystemWatcher(dir) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName };
                FileSystemEventHandler h = (_, _) => Application.Current.Dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });
                w.Created += h; w.Deleted += h;
                w.Renamed += (_, _) => Application.Current.Dispatcher.BeginInvoke(() => { _debounce.Stop(); _debounce.Start(); });
                w.EnableRaisingEvents = true;
                _watchers.Add(w);
            }
            catch { /* no access to public desktop – fine */ }
        }

        // monitors changed → re-place; Explorer restarted → re-pin to the new desktop window
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) =>
            Application.Current.Dispatcher.BeginInvoke(() => { PrepareScreen(); RebuildAll(); }, DispatcherPriority.Background);
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _msgWindow = new HwndSource(new HwndSourceParameters("WallflowMsg") { Width = 0, Height = 0, WindowStyle = 0 });
        _msgWindow.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
        {
            if (msg == _taskbarCreated)
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    RebuildAll();
                    if (S.HideDesktopIcons) Native.SetDesktopIconsVisible(false);
                }, DispatcherPriority.Background);
            return IntPtr.Zero;
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int RegisterWindowMessage(string msg);

    IEnumerable<GlassWindow> All()
    {
        if (_clock != null) yield return _clock;
        if (_weather != null) yield return _weather;
        if (_calendar != null) yield return _calendar;
        if (_player != null) yield return _player;
        if (_todo != null) yield return _todo;
        foreach (var n in _notes) yield return n;
        foreach (var f in _folders) yield return f;
    }

    /// <summary>Screen rectangles (DIPs) of all visible widgets except one — used for Shift-snapping.</summary>
    public IEnumerable<Rect> OtherRects(GlassWindow except) =>
        All().Where(w => w != except && w.IsVisible).Select(w => new Rect(w.Left, w.Top, w.ActualWidth, w.ActualHeight)).ToList();

    // ================= screen-aware layout =================
    public bool LayoutPending { get; private set; }

    /// <summary>"1536x816" — the usable size of the main screen in DIPs (already includes Windows scaling).</summary>
    static string ScreenKey()
    {
        var wa = SystemParameters.WorkArea;
        return $"{Math.Round(wa.Width)}x{Math.Round(wa.Height)}+{System.Windows.Forms.Screen.AllScreens.Length}";
    }

    /// <summary>Widget size that fits this screen: 1.0 on big monitors, ~0.75 on a 15" laptop at 150 %.</summary>
    public static double FitScale()
    {
        var wa = SystemParameters.WorkArea;
        return Math.Round(Math.Clamp(Math.Min(wa.Height / 1050.0, wa.Width / 1800.0), 0.65, 1.0), 2);
    }

    /// <summary>
    /// Every screen setup keeps its own arrangement: when the screen changes (laptop ↔ monitor, new resolution,
    /// other scaling) the current positions are saved for the old one and the new one is restored or laid out fresh.
    /// </summary>
    void PrepareScreen()
    {
        var key = ScreenKey();
        if (S.LayoutKey == key) return;
        if (string.IsNullOrEmpty(S.LayoutKey))
        {
            // first run of this version: keep whatever arrangement already exists for the current screen
            S.LayoutKey = key;
            if (AnyPlaced()) SaveLayout(key);
            else if (S.Widgets.AutoScale) S.Widgets.Scale = FitScale();
            SettingsStore.Save();
            return;
        }
        SaveLayout(S.LayoutKey);
        S.LayoutKey = key;
        if (S.Layouts.TryGetValue(key, out var saved))
        {
            RestoreLayout(saved);
            if (S.LayoutScales.TryGetValue(key, out var sc)) S.Widgets.Scale = sc;
        }
        else
        {
            bool firstEver = S.Layouts.Count == 0 && !AnyPlaced();
            if (!firstEver) ClearPositions();          // new screen → fresh layout for it
            if (S.Widgets.AutoScale) S.Widgets.Scale = FitScale();
        }
        SettingsStore.Save();
    }

    IEnumerable<(string id, WidgetPos pos)> PositionSlots()
    {
        var w = S.Widgets;
        yield return ("clock", w.ClockPos); yield return ("weather", w.WeatherPos); yield return ("calendar", w.CalendarPos);
        yield return ("player", w.PlayerPos); yield return ("todo", w.TodoPos);
        foreach (var n in w.Notes) yield return ("note:" + n.Id, n.Pos);
        foreach (var f in S.Folders) yield return ("folder:" + f.Id, f.Pos);
    }

    bool AnyPlaced() => PositionSlots().Any(p => !double.IsNaN(p.pos.Left));

    void SaveLayout(string key)
    {
        S.Layouts[key] = PositionSlots().ToDictionary(p => p.id, p => new WidgetPos { Left = p.pos.Left, Top = p.pos.Top });
        S.LayoutScales[key] = S.Widgets.Scale;
    }

    void RestoreLayout(Dictionary<string, WidgetPos> saved)
    {
        foreach (var (id, pos) in PositionSlots())
        {
            if (saved.TryGetValue(id, out var p)) { pos.Left = p.Left; pos.Top = p.Top; }
            else { pos.Left = double.NaN; pos.Top = double.NaN; }
        }
    }

    void ClearPositions()
    {
        foreach (var (_, pos) in PositionSlots()) { pos.Left = double.NaN; pos.Top = double.NaN; }
    }

    /// <summary>
    /// Places all widgets without overlaps: clock top-left with the folders under it,
    /// weather/calendar/player/to-do/notes stacked in columns from the right edge.
    /// </summary>
    void DoLayout()
    {
        LayoutPending = false;
        var wa = SystemParameters.WorkArea;
        double m = Math.Round(Math.Clamp(wa.Width * 0.022, 16, 48));
        double gap = Math.Round(m * 0.7);

        // right side: columns filled top→bottom, then the next column to the left
        var right = new List<GlassWindow>();
        foreach (var w in new GlassWindow?[] { _weather, _calendar, _player, _todo }) if (w != null) right.Add(w);
        right.AddRange(_notes);
        double colRight = wa.Right - m, y = wa.Top + m, colW = 0, leftEdge = wa.Right - m;
        foreach (var w in right)
        {
            double ww = w.ActualWidth, hh = w.ActualHeight;
            if (y + hh > wa.Bottom - m && y > wa.Top + m)
            {
                colRight -= colW + gap; y = wa.Top + m; colW = 0;
            }
            w.PlaceAt(colRight - ww, y);
            y += hh + gap;
            colW = Math.Max(colW, ww);
            leftEdge = Math.Min(leftEdge, colRight - ww);
        }

        // left side: clock, then a grid of folders in the space that is left
        double x0 = wa.Left + m, top = wa.Top + m;
        if (_clock != null) { _clock.PlaceAt(x0 - 10, top - 10); top += _clock.ActualHeight - 10 + gap; }
        if (_folders.Count > 0)
        {
            double fw = _folders.Max(f => f.ActualWidth), fh = _folders.Max(f => f.ActualHeight);
            double avail = Math.Max(fw, leftEdge - gap - x0);
            int perRow = Math.Max(1, (int)((avail + gap) / (fw + gap)));
            for (int i = 0; i < _folders.Count; i++)
                _folders[i].PlaceAt(x0 + (i % perRow) * (fw + gap), top + (i / perRow) * (fh + gap));
        }
        SettingsStore.Save();
        SaveLayout(S.LayoutKey);
    }

    void ScheduleLayoutIfNeeded()
    {
        if (!LayoutPending) return;
        Application.Current.Dispatcher.BeginInvoke(DoLayout, DispatcherPriority.ApplicationIdle);
    }

    public void RebuildAll()
    {
        var w = S.Widgets;
        // nothing placed yet (first run, new screen, "reset positions") → one tidy layout for everything
        LayoutPending = !AnyPlaced();
        Toggle(ref _clock, w.Clock, () => new ClockWidget());
        Toggle(ref _weather, w.Weather, () => new WeatherWidget());
        Toggle(ref _calendar, w.Calendar, () => new CalendarWidget());
        Toggle(ref _player, w.Player, () => new PlayerWidget());
        Toggle(ref _todo, w.Todo, () => new TodoWidget());
        RebuildNotes();
        RebuildFolders();
        ScheduleLayoutIfNeeded();
    }

    void Toggle<T>(ref T? field, bool on, Func<T> make) where T : GlassWindow
    {
        field?.Close();
        field = null;
        if (!on) return;
        field = make();
        field.EditMode = EditMode;
        field.Show();
    }

    public void RebuildFolders()
    {
        foreach (var f in _folders) f.Close();
        _folders.Clear();
        ShellIcon.ClearCache();
        if (!S.Widgets.Folders) return;
        int i = 0;
        foreach (var def in S.Folders.Where(f => f.Items.Count > 0).ToList())
        {
            var fw = new FolderWidget(def, i++) { EditMode = EditMode };
            fw.Show();
            _folders.Add(fw);
        }
    }

    void RebuildNotes()
    {
        foreach (var n in _notes) n.Close();
        _notes.Clear();
        int i = 0;
        foreach (var def in S.Widgets.Notes)
        {
            var nw = new NoteWidget(def, i++) { EditMode = EditMode };
            nw.Show();
            _notes.Add(nw);
        }
    }

    public void AddNote()
    {
        var def = new NoteDef();
        S.Widgets.Notes.Add(def);
        SettingsStore.Save();
        var nw = new NoteWidget(def, S.Widgets.Notes.Count - 1) { EditMode = EditMode };
        nw.Show();
        _notes.Add(nw);
    }

    public void RemoveNote(NoteDef def)
    {
        S.Widgets.Notes.Remove(def);
        SettingsStore.Save();
        var w = _notes.Find(n => n.Note == def);
        if (w != null) { _notes.Remove(w); w.Close(); }
    }

    /// <summary>Re-applies glass/scale/language without recreating windows.</summary>
    public void RefreshLook()
    {
        foreach (var w in All()) { w.ApplyLook(); w.Refresh(); }
    }

    /// <summary>Only the glass (tint, blur, radius, size) — cheap, used while dragging sliders.</summary>
    public void RefreshGlass()
    {
        foreach (var w in All()) w.ApplyLook();
    }

    public void ReloadWeather() => _weather?.Reload();

    // ---- for the screenshot tour ----
    public FolderWidget? FirstFolder => _folders.FirstOrDefault(f => f.Folder.Items.Count >= 2) ?? _folders.FirstOrDefault();
    public TodoWidget? Todo => _todo;

    public void SetEditMode(bool on)
    {
        EditMode = on;
        foreach (var w in All()) w.EditMode = on;
        EditModeChanged?.Invoke();
    }

    public void CloseAll()
    {
        foreach (var w in All().ToList()) w.Close();
        _clock = null; _weather = null; _calendar = null; _player = null; _todo = null;
        _notes.Clear();
        _folders.Clear();
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        _msgWindow?.Dispose();
    }

    // ---------------- folders data ----------------

    /// <summary>First run: sort everything. Later: add new desktop shortcuts, drop deleted ones. Returns true if changed.</summary>
    public bool SyncFolders()
    {
        bool changed = false;
        if (!S.FoldersInitialized)
        {
            S.Folders = Shortcuts.AutoArrange(Shortcuts.ScanDesktop());
            S.FoldersInitialized = true;
            changed = true;
        }
        else
        {
            // only shortcuts/programs belong in folders (also undoes the version that added plain files)
            foreach (var f in S.Folders)
                changed |= f.Items.RemoveAll(p => !File.Exists(p) || !Shortcuts.IsShortcut(p)) > 0;
            changed |= S.Folders.RemoveAll(f => f.Items.Count == 0 && Shortcuts.FileKindFolders.Contains(f.Name)) > 0;

            if (S.AutoAddNewShortcuts)
            {
                var ignored = new HashSet<string>(S.IgnoredShortcuts, StringComparer.OrdinalIgnoreCase);
                foreach (var path in Shortcuts.FindNew(S.Folders).Where(p => !ignored.Contains(p)))
                {
                    var cat = Shortcuts.Categorize(path);
                    var name = Loc.T(cat);
                    var folder = S.Folders.FirstOrDefault(f => f.Name == name || f.Name == cat);
                    if (folder is null) S.Folders.Add(folder = new FolderDef { Name = name });
                    folder.Items.Add(path);
                    changed = true;
                }
            }
        }
        if (changed) SettingsStore.Save();
        return changed;
    }

    /// <summary>Throws away the current folders and sorts the desktop again.</summary>
    public void Rearrange()
    {
        S.FoldersInitialized = false;
        S.IgnoredShortcuts.Clear();
        SyncFolders();
        RebuildFolders();
    }

    public void Ignore(string path)
    {
        if (!S.IgnoredShortcuts.Contains(path, StringComparer.OrdinalIgnoreCase)) S.IgnoredShortcuts.Add(path);
        SettingsStore.Save();
    }

    public void ResetPositions()
    {
        var w = S.Widgets;
        if (w.AutoScale) w.Scale = FitScale();
        w.ClockPos = new(); w.WeatherPos = new(); w.CalendarPos = new(); w.PlayerPos = new(); w.TodoPos = new();
        foreach (var n in w.Notes) n.Pos = new();
        foreach (var f in S.Folders) f.Pos = new();
        SettingsStore.Save();
        RebuildAll();
    }
}
