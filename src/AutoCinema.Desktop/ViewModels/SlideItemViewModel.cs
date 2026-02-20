using CommunityToolkit.Mvvm.ComponentModel;

namespace AutoCinema.Desktop.ViewModels;

public partial class SlideItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private string? _audioPath;

    [ObservableProperty]
    private double _duration = 3.0;

    [ObservableProperty]
    private int _index;

    // Helper property for display
    public string FileName => System.IO.Path.GetFileName(ImagePath);
}
