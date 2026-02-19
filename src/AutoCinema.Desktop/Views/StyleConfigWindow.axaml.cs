using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoCinema.Desktop.Views;

public partial class StyleConfigWindow : Window
{
    public StyleConfigWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
