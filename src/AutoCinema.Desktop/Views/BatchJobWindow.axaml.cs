using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AutoCinema.Desktop.Views;

public partial class BatchJobWindow : Window
{
    public BatchJobWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
