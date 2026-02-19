using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoCinema.Desktop.ViewModels;

public partial class MainHomeWindowViewModel : ViewModelBase
{
    public MainHomeWindowViewModel()
    {
    }

    [RelayCommand]
    private void OpenVideoGenerator()
    {
        // TODO: Navigation logic to open MainWindow or embed it
    }
}
