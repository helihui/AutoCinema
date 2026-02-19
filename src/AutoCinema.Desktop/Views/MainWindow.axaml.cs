using Avalonia.Controls;
using Avalonia.Interactivity;


namespace AutoCinema.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OpenVoiceConfig_Click(object? sender, RoutedEventArgs e)
    {
        var window = new VoiceConfigWindow
        {
            DataContext = DataContext
        };
        await window.ShowDialog(this);
    }

    private async void OpenStyleConfig_Click(object? sender, RoutedEventArgs e)
    {
        var window = new StyleConfigWindow
        {
            DataContext = DataContext
        };
        await window.ShowDialog(this);
    }
}