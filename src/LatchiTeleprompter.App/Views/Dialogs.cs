using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LatchiTeleprompter.App.Views;

/// <summary>
/// Small dark-styled dialogs (Confirm / Input / Alert) built in code — no
/// default light-theme MessageBox for the main flows. Return values:
/// Confirm → true(yes) / false(no) / null(cancelled by closing).
/// </summary>
public static class Dialogs
{
    private static Window OwnerOf(Window? owner) => owner ?? Application.Current?.MainWindow ?? new Window();

    public static bool? Confirm(Window? owner, string message, string question)
    {
        bool? result = null;
        var win = Base(460, "تأكيد");
        var sp = new StackPanel { Margin = new Thickness(24) };
        sp.Children.Add(new TextBlock
        {
            Text = message, Foreground = Brushes.White, FontSize = 14,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6),
        });
        sp.Children.Add(new TextBlock
        {
            Text = question, Foreground = Res("BrushGold"),
            FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 18),
        });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(MkBtn("نعم", () => { result = true; win.Close(); }, primary: true));
        row.Children.Add(MkBtn("لا", 64, 34, () => { result = false; win.Close(); }));
        row.Children.Add(MkBtn("إلغاء", 64, 34, win.Close));
        sp.Children.Add(row);
        win.Content = sp;
        win.Owner = OwnerOf(owner);
        win.ShowDialog();
        return result;
    }

    public static void Alert(Window? owner, string message, string title)
    {
        var win = Base(460, title);
        var sp = new StackPanel { Margin = new Thickness(24) };
        sp.Children.Add(new TextBlock
        {
            Text = message, Foreground = Brushes.White, FontSize = 14,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20),
        });
        var ok = MkBtn("حسناً", 96, 34, win.Close, primary: true);
        ok.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(ok);
        win.Content = sp;
        win.Owner = OwnerOf(owner);
        win.ShowDialog();
    }

    /// <summary>Returns the entered text, or null when cancelled/empty.</summary>
    public static string? Input(Window? owner, string title, string label, string initial)
    {
        string? value = null;
        var win = Base(480, title);
        var sp = new StackPanel { Margin = new Thickness(24) };
        sp.Children.Add(new TextBlock
        {
            Text = label, Foreground = Res("BrushMuted"),
            FontSize = 12.5, Margin = new Thickness(0, 0, 0, 6),
        });
        var input = new TextBox
        {
            Text = initial, FontSize = 14, Padding = new Thickness(8, 6, 8, 6),
            Background = Res("BrushPanel"), Foreground = Brushes.White,
            BorderBrush = Res("BrushBorder"), CaretBrush = Res("BrushGold"),
        };
        sp.Children.Add(input);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0),
        };
        row.Children.Add(MkBtn("موافق", 96, 34, () =>
        {
            if (input.Text.Trim().Length > 0) { value = input.Text.Trim(); win.Close(); }
        }, primary: true));
        row.Children.Add(MkBtn("إلغاء", 64, 34, win.Close));
        sp.Children.Add(row);
        win.Content = sp;
        win.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        win.Owner = OwnerOf(owner);
        win.ShowDialog();
        return value;
    }

    private static Brush Res(string key) => (Brush)App.Current.FindResource(key);

    private static Window Base(double w, string title) => new()
    {
        Title = title,
        Width = w,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        ResizeMode = ResizeMode.NoResize,
        ShowInTaskbar = false,
        Background = Res("BrushPanel"),
        FlowDirection = FlowDirection.RightToLeft,
        FontFamily = new FontFamily("Segoe UI"),
    };

    private static Button MkBtn(string text, Action onClick, bool primary = false)
        => MkBtn(text, 84, 34, onClick, primary);

    private static Button MkBtn(string text, double w, double h, Action onClick, bool primary = false)
    {
        var btn = new Button
        {
            Content = text,
            Width = w,
            Height = h,
            Margin = new Thickness(5, 0, 5, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Focusable = false,
            Background = primary ? Res("BrushGold") : Brushes.Transparent,
            Foreground = primary ? new SolidColorBrush(Color.FromRgb(0x0A, 0x0D, 0x14)) : Res("BrushMuted"),
            FontWeight = primary ? FontWeights.Bold : FontWeights.Normal,
            BorderThickness = new Thickness(0),
            Template = MkBtnTemplate(),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static ControlTemplate MkBtnTemplate()
    {
        const string xaml = @"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                             xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
            <Border x:Name='bd' Background='{TemplateBinding Background}' CornerRadius='7'>
                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' Margin='10,0'/>
            </Border>
            <ControlTemplate.Triggers>
                <Trigger Property='IsMouseOver' Value='True'>
                    <Setter TargetName='bd' Property='Opacity' Value='0.85'/>
                </Trigger>
            </ControlTemplate.Triggers>
        </ControlTemplate>";
        return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
    }
}
