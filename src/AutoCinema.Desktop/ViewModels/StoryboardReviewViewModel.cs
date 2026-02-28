using System;
using System.Collections.ObjectModel;
using System.Linq;
using AutoCinema.Pro.Models.Screenplay;
using AutoCinema.Pro.Pipeline.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoCinema.Desktop.ViewModels;

/// <summary>
/// 分镜审阅对话框 ViewModel（CoDesign 模式）
/// 允许用户在剧本生成后查看并修改每个分镜，然后确认继续生成。
/// </summary>
public partial class StoryboardReviewViewModel : ViewModelBase
{
    private readonly StoryboardReviewGate _gate;
    public event EventHandler? WindowCloseRequested;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _synopsis = "";

    [ObservableProperty]
    private string _wrappingStyle = "";

    [ObservableProperty]
    private ObservableCollection<ShotEditItem> _shots = new();

    [ObservableProperty]
    private string _notes = "";

    [ObservableProperty]
    private ObservableCollection<string> _characterSummaries = new();

    [ObservableProperty]
    private ObservableCollection<string> _sceneSummaries = new();

    public StoryboardReviewViewModel(StoryboardReviewGate gate)
    {
        _gate = gate;
        LoadScreenplay(gate.PendingScreenplay);
    }

    private void LoadScreenplay(ScreenplayDocument screenplay)
    {
        Title = screenplay.Title;
        Synopsis = screenplay.Synopsis;
        WrappingStyle = screenplay.WrappingStyle;
        Notes = screenplay.Notes;

        CharacterSummaries = new ObservableCollection<string>(
            screenplay.Characters.Select(c => $"{c.Name}（{c.Role}）：{c.Appearance}"));

        SceneSummaries = new ObservableCollection<string>(
            screenplay.Scenes.Select(s => $"{s.Name}·{s.TimeOfDay}：{s.Atmosphere}"));

        Shots = new ObservableCollection<ShotEditItem>(
            screenplay.Shots.Select(s => new ShotEditItem
            {
                Index = s.Index,
                DurationSeconds = s.DurationSeconds,
                FrameType = s.FrameType,
                CameraAngle = s.CameraAngle,
                Action = s.Action,
                Transition = s.Transition,
                NarrationText = s.NarrationText,
                CharacterDialogue = s.CharacterDialogue ?? "",
                AigcPrompt = s.AigcPrompt
            }));
    }

    /// <summary>
    /// 用户点击「确认生成 ▶」：将修改后的分镜回写到剧本，解锁 pipeline 继续执行。
    /// </summary>
    [RelayCommand]
    private void ConfirmAndGenerate()
    {
        var pending = _gate.PendingScreenplay;

        var editedShots = Shots.Select((item, i) => new Shot
        {
            Index = item.Index,
            DurationSeconds = item.DurationSeconds,
            FrameType = item.FrameType,
            CameraAngle = item.CameraAngle,
            Action = item.Action,
            Transition = item.Transition,
            NarrationText = item.NarrationText,
            CharacterDialogue = string.IsNullOrWhiteSpace(item.CharacterDialogue) ? null : item.CharacterDialogue,
            AigcPrompt = item.AigcPrompt,
            SceneName = pending.Shots.Count > i ? pending.Shots[i].SceneName : null
        }).ToList();

        var approvedScreenplay = pending with
        {
            Title = Title,
            Synopsis = Synopsis,
            Notes = Notes,
            Shots = editedShots
        };

        _gate.Approve(approvedScreenplay);
        WindowCloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 用户点击「取消」：取消 pipeline 执行。
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _gate.Cancel();
        WindowCloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// 单个分镜的可编辑视图模型项
/// </summary>
public partial class ShotEditItem : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private int _durationSeconds;
    [ObservableProperty] private string _frameType = "";
    [ObservableProperty] private string _cameraAngle = "";
    [ObservableProperty] private string _action = "";
    [ObservableProperty] private string _transition = "";
    [ObservableProperty] private string _narrationText = "";
    [ObservableProperty] private string _characterDialogue = "";
    [ObservableProperty] private string _aigcPrompt = "";

    public string DisplayTitle => $"分镜 {Index}  [{FrameType}·{CameraAngle}]  {DurationSeconds}s";
}
