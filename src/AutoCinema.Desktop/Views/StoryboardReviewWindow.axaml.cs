using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AutoCinema.Desktop.ViewModels;

namespace AutoCinema.Desktop.Views;

public partial class StoryboardReviewWindow : Window
{
    public StoryboardReviewWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        this.Opened += OnWindowOpened;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // 窗口打开后设置 ParentWindow 引用，供 ViewModel 关闭窗口使用
        if (DataContext is StoryboardReviewViewModel vm)
        {
            vm.ParentWindow = this;
        }
    }
}
