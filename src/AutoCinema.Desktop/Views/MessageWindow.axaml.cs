using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace AutoCinema.Desktop.Views;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message) : this()
    {
        Title = title;
        MessageText.Text = message;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    public static Task ShowAsync(string title, string message, Window? owner)
    {
        var dialog = new MessageWindow(title, message);
        if (owner != null)
        {
            return dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
            return Task.CompletedTask;
        }
    }
}
