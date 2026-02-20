using Avalonia.Controls;
using Avalonia.Interactivity;
using AutoCinema.Desktop.ViewModels.JobCreation;

namespace AutoCinema.Desktop.Views.Controls;

public partial class ScriptToVideoControl : UserControl
{
    public ScriptToVideoControl()
    {
        InitializeComponent();
    }

    private async void OpenVoiceConfig_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel == null) return;

        var window = new VoiceConfigWindow
        {
            DataContext = this.DataContext // Ensure DataContext is passed down
        };
        await window.ShowDialog(topLevel);
    }

    private async void OpenStyleConfig_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this) as Window;
        if (topLevel == null) return;

        var window = new StyleConfigWindow
        {
            DataContext = this.DataContext
        };
        await window.ShowDialog(topLevel);
    }
}
