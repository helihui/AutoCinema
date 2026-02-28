using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AutoCinema.Desktop.ViewModels;
using AvaloniaEdit;

namespace AutoCinema.Desktop.Views;

public partial class StoryboardReviewWindow : Window
{
    private TextEditor? _jsonEditor;

    public StoryboardReviewWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _jsonEditor = this.FindControl<TextEditor>("JsonEditor");
        if (_jsonEditor != null)
        {
            _jsonEditor.Options.HighlightCurrentLine = true;
            _jsonEditor.Options.ShowTabs = true;
            _jsonEditor.Options.ShowBoxForControlCharacters = true;
            
            // 初次加载 ViewModel 时同步文本一次
            this.DataContextChanged += OnDataContextChanged;
            
            // 响应编辑器内文字变动回写到 ViewModel 里
            _jsonEditor.TextChanged += OnEditorTextChanged;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is StoryboardReviewViewModel vm && _jsonEditor != null)
        {
            vm.ParentWindow = this;
            _jsonEditor.Text = vm.ScreenplayJsonText;
            _jsonEditor.IsReadOnly = vm.IsReadOnly;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (DataContext is StoryboardReviewViewModel vm && _jsonEditor != null)
        {
            vm.ScreenplayJsonText = _jsonEditor.Text;
        }
    }
}
