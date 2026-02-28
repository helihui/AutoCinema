using Avalonia.Controls;
using AutoCinema.Desktop.ViewModels;

namespace AutoCinema.Desktop.Views;

public partial class StoryboardReviewWindow : Window
{
    public StoryboardReviewWindow()
    {
        InitializeComponent();
    }

    public StoryboardReviewWindow(StoryboardReviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.WindowCloseRequested += (_, _) => Close();
    }
}
