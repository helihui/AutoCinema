using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoCinema.Desktop.Views;

public partial class SlideshowEditorWindow : Window
{
    public SlideshowEditorWindow()
    {
        InitializeComponent();
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
