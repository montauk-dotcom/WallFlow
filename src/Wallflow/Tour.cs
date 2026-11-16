using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Wallflow;

/// <summary>
/// "Wallflow.exe --tray --tour <folder>": walks through the app and saves screenshots for the user guide
/// (used by CI to produce the PDF manuals in Russian and English).
/// </summary>
public static class Tour
{
    public static async Task Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var app = App.Me;
        async Task Wait(double s) => await Task.Delay(TimeSpan.FromSeconds(s));
        void Shot(string name)
        {
            try
            {
                var b = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
                using var bmp = new System.Drawing.Bitmap(b.Width, b.Height);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                    g.CopyFromScreen(b.Location, System.Drawing.Point.Empty, b.Size);
                bmp.Save(Path.Combine(dir, name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            catch (Exception ex) { App.Log(ex); }
        }

        try
        {
            await Wait(14);                      // widgets, weather, glass
            Shot("01-desktop");

            app.Widgets.SetEditMode(true); await Wait(1.5);
            Shot("02-edit");
            app.Widgets.SetEditMode(false); await Wait(1);

            var folder = app.Widgets.FirstFolder;
            if (folder != null) { folder.Open(); await Wait(1.5); Shot("03-folder"); folder.ClosePopup(); await Wait(1); }

            if (app.Widgets.Todo?.ContextMenu is { } todoMenu)
            {
                todoMenu.PlacementTarget = app.Widgets.Todo; todoMenu.IsOpen = true; await Wait(1);
                Shot("04-todo-menu"); todoMenu.IsOpen = false; await Wait(0.5);
            }

            app.Tray?.ShowMenuForTour(); await Wait(1.2);
            Shot("05-tray");
            app.Tray?.HideMenu(); await Wait(0.8);

            app.OpenSettings("widgets"); await Wait(2.5);
            Shot("06-settings-widgets");
            var sw = FindWindow<Views.SettingsWindow>();
            sw?.ScrollTo(560); await Wait(1); Shot("07-settings-colors");
            sw?.ScrollTo(5000); await Wait(1); Shot("08-settings-glass");
            var picker = Views.ColorPicker.ShowForTour(Color.FromRgb(255, 92, 168)); await Wait(1.2);
            Shot("09-color-picker"); picker.Close(); await Wait(0.5);
            app.OpenSettings("desktop"); sw?.ScrollTo(0); await Wait(1.5); Shot("10-settings-desktop");
            app.OpenSettings("wallpapers"); sw?.ScrollTo(0); await Wait(1.5); Shot("11-settings-wallpapers");
            app.OpenSettings("general"); sw?.ScrollTo(0); await Wait(1.5); Shot("12-settings-general");
            sw?.Close(); await Wait(1);

            app.OpenGallery(); await Wait(12);
            var gw = FindWindow<Views.GalleryWindow>();
            if (gw != null) gw.WindowState = WindowState.Maximized;
            await Wait(3);
            Shot("13-gallery");
            gw?.PreviewFirst(); await Wait(7);
            Shot("14-preview");
            gw?.Close(); await Wait(1);
        }
        catch (Exception ex) { App.Log(ex); }
        File.WriteAllText(Path.Combine(dir, "done.txt"), DateTime.Now.ToString("O"));
        app.Quit();
    }

    static T? FindWindow<T>() where T : Window
    {
        foreach (Window w in Application.Current.Windows) if (w is T t) return t;
        return null;
    }
}
