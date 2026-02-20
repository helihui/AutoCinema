using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AutoCinema.Pro.Services;
using AutoCinema.Pro.Models.Jobs;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using System.Linq;
using System.Threading.Tasks;

namespace AutoCinema.Desktop.ViewModels;

public partial class SlideshowEditorViewModel : ObservableObject
{
    private readonly IJobManager _jobManager;
    private readonly Window _view; // MVP hack: hold reference to view for StorageProvider

    [ObservableProperty]
    private ObservableCollection<SlideItemViewModel> _items = new();

    [ObservableProperty]
    private string? _backgroundMusicPath;

    [ObservableProperty]
    private string _outputName = "New_Slideshow";

    public SlideshowEditorViewModel(IJobManager jobManager, Window view)
    {
        _jobManager = jobManager;
        _view = view;
    }

    [RelayCommand]
    private async Task AddImages()
    {
        var topLevel = TopLevel.GetTopLevel(_view);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择图片",
            AllowMultiple = true,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                Items.Add(new SlideItemViewModel
                {
                    ImagePath = path,
                    Index = Items.Count
                });
            }
        }
    }

    [RelayCommand]
    private async Task SelectBackgroundMusic()
    {
        var topLevel = TopLevel.GetTopLevel(_view);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择背景音乐",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Audio Files") { Patterns = new[] { "*.mp3", "*.wav", "*.m4a" } } }
        });

        if (files.Count > 0)
        {
            BackgroundMusicPath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void RemoveItem(SlideItemViewModel item)
    {
        if (item != null && Items.Contains(item))
        {
            Items.Remove(item);
            // Re-index
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].Index = i;
            }
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Items.Clear();
        BackgroundMusicPath = null;
    }

    [RelayCommand]
    private async Task CreateJob()
    {
        if (Items.Count == 0) return;

        var job = new SlideshowJob
        {
            Title = !string.IsNullOrWhiteSpace(OutputName) ? OutputName : "Slideshow Job",
            BackgroundMusicPath = BackgroundMusicPath,
            Items = Items.Select(i => new SlideItem
            {
                ImagePath = i.ImagePath,
                AudioPath = i.AudioPath,
                Duration = i.Duration
            }).ToList()
        };

        await _jobManager.EnqueueAsync(job);
        _view.Close();
    }
}
