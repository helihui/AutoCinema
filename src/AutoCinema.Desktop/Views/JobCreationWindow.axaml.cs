using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AutoCinema.Desktop.ViewModels.JobCreation;

namespace AutoCinema.Desktop.Views;

public partial class JobCreationWindow : Window
{
    public JobCreationWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is JobCreationViewModel vm)
        {
            vm.RequestClose += (s, args) => Close();
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
