using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoCinema.Desktop.Views;

public partial class VoiceConfigWindow : Window
{
    public VoiceConfigWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
