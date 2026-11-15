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
        SyncFolders();
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
            Application.Current.Dispatcher.BeginInvoke(RebuildAll, DispatcherPriority.Background);
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

    public void RebuildAll()
    {
        var w = S.Widgets;
        Toggle(ref _clock, w.Clock, () => new ClockWidget());
        Toggle(ref _weather, w.Weather, () => new WeatherWidget());
        Toggle(ref _calendar, w.Calendar, () => new CalendarWidget());
        Toggle(ref _player, w.Player, () => new PlayerWidget());
        Toggle(ref _todo, w.Todo, () => new TodoWidget());
        RebuildNotes();
        RebuildFolders();
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
            foreach (var f in S.Folders)
                changed |= f.Items.RemoveAll(p => !File.Exists(p)) > 0;

            if (S.AutoAddNewShortcuts)
            {
                var ignored = new HashSet<string>(S.IgnoredShortcuts, StringComparer.OrdinalIgnoreCase);
                foreach (var path in Shortcuts.FindNew(S.Folders).Where(p => !ignored.Contains(p)))
                {
                    var cat = Shortcuts.Categorize(path);
                    var folder = S.Folders.FirstOrDefault(f => f.Name == cat);
                    if (folder is null) S.Folders.Add(folder = new FolderDef { Name = cat });
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
        w.ClockPos = new(); w.WeatherPos = new(); w.CalendarPos = new(); w.PlayerPos = new(); w.TodoPos = new();
        foreach (var n in w.Notes) n.Pos = new();
        foreach (var f in S.Folders) f.Pos = new();
        SettingsStore.Save();
        RebuildAll();
    }
}
