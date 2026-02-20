using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using AutoCinema.Desktop.Services;
using AutoCinema.Pro.Models.Jobs; // Ensure JobType is available

namespace AutoCinema.Desktop.ViewModels.JobCreation;

public partial class JobCreationViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    public event EventHandler? RequestClose;

    [ObservableProperty]
    private bool _isSelectingType = true;

    [ObservableProperty]
    private ViewModelBase? _currentConfigViewModel;

    [ObservableProperty]
    private string _windowTitle = "新建任务";

    public JobCreationViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [RelayCommand]
    private void SelectScriptToVideo()
    {
        IsSelectingType = false;
        WindowTitle = "新建任务 - 文本生成视频";

        // Create the form ViewModel using DI
        var formVm = ActivatorUtilities.CreateInstance<ScriptToVideoFormViewModel>(_serviceProvider);
        formVm.JobCreated += (s, e) => OnJobCreated();
        CurrentConfigViewModel = formVm;
    }

    [RelayCommand]
    private void SelectImageToVideo()
    {
        // TODO: Integrate SlideshowEditorViewModel here
        // For MVP, we might keep it separate or integrate it similarly
        IsSelectingType = false;
        WindowTitle = "新建任务 - 图片生成视频";

        // Placeholder or actual integration
        // For now, let's just show a "Not Implemented" or use the existing Slideshow logic if adaptable
    }

    [RelayCommand]
    private void BackToSelection()
    {
        IsSelectingType = true;
        WindowTitle = "新建任务";
        CurrentConfigViewModel = null;
    }

    private void OnJobCreated()
    {
        // Close the window
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
