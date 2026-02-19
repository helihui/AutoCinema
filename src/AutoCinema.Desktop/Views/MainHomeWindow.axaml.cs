using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using AutoCinema.Desktop.ViewModels;

namespace AutoCinema.Desktop.Views;

public partial class MainHomeWindow : Window
{
    public MainHomeWindow()
    {
        InitializeComponent();
    }

    private void OpenVideoGenerator_Click(object? sender, RoutedEventArgs e)
    {
        var app = (App)Avalonia.Application.Current!;
        var viewModel = app.Services?.GetRequiredService<MainWindowViewModel>();

        if (viewModel != null)
        {
            var mainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            mainWindow.Show();
        }
    }

    private void OpenBatchJobManager_Click(object? sender, RoutedEventArgs e)
    {
        var app = (App)Avalonia.Application.Current!;
        var viewModel = app.Services?.GetRequiredService<BatchJobWindowViewModel>();

        if (viewModel != null)
        {
            var batchWindow = new BatchJobWindow
            {
                DataContext = viewModel
            };
            batchWindow.Show();
        }
    }
}
