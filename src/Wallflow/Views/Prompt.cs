using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Wallflow;

/// <summary>Tiny input dialog.</summary>
public static class Prompt
{
    public static string? Ask(string question, string value = "")
    {
        var w = new Window
        {
            Title = "Wallflow", Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true, ShowInTaskbar = false,
            Background = (System.Windows.Media.Brush)Application.Current.Resources["Bg"]
        };
        UiKit.DarkTitleBar(w);
        var sp = new StackPanel { Margin = new Thickness(18) };
        sp.Children.Add(new TextBlock { Text = question, Margin = new Thickness(0, 0, 0, 8) });
        var tb = new TextBox { Text = value };
        sp.Children.Add(tb);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, Style = (Style)Application.Current.Resources["Primary"], MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Отмена", IsCancel = true, MinWidth = 80 };
        ok.Click += (_, _) => w.DialogResult = true;
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        sp.Children.Add(buttons);
        w.Content = sp;
        w.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); Keyboard.Focus(tb); };
        return w.ShowDialog() == true ? tb.Text : null;
    }
}
