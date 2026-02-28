using System;
using System.Text.Json;
using System.Threading.Tasks;
using AutoCinema.Pro.Models;
using AutoCinema.Pro.Models.Jobs;
using AutoCinema.Pro.Models.Screenplay;
using AutoCinema.Pro.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;

namespace AutoCinema.Desktop.ViewModels;

public partial class StoryboardReviewViewModel : ViewModelBase
{
    private readonly JobItem _jobItem;
    private readonly IJobManager? _jobManager;

    [ObservableProperty]
    private string _windowTitle = "剧本查看与编辑";

    [ObservableProperty]
    private string _screenplayJsonText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "就绪";
    
    [ObservableProperty]
    private bool _isReadOnly = false;

    [ObservableProperty]
    private bool _canContinuePipeline = false;

    // 当前操作的窗口引用
    public Window? ParentWindow { get; set; }

    /// <summary>
    /// 支持直接通过包含了 ScreenplayRawData 的任务打开（用于随时查看和修改）
    /// </summary>
    public StoryboardReviewViewModel(JobItem jobItem, IJobManager jobManager)
    {
        _jobItem = jobItem;
        _jobManager = jobManager;

        if (jobItem is TextToVideoJob t && t.ProjectData != null)
        {
            WindowTitle = $"剧本查看 - {jobItem.Title}";
            LoadAndFormatJson(t.ProjectData.ScreenplayRawData);
        }
        else
        {
            StatusMessage = "未找到可用的剧本数据";
            IsReadOnly = true;
        }

        // 如果任务正在等待审阅卡点，则允许点击“继续”
        if (_jobItem.HasPendingReview)
        {
            CanContinuePipeline = true;
            WindowTitle = "剧本审阅 (等待您的确认以继续)";
        }
    }

    private void LoadAndFormatJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            ScreenplayJsonText = "{}";
            return;
        }

        try
        {
            // 尝试格式化 JSON 以便阅读
            var parsed = JsonDocument.Parse(rawJson);
            var opt = new JsonSerializerOptions { 
                WriteIndented = true, 
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
            };
            ScreenplayJsonText = JsonSerializer.Serialize(parsed, opt);
        }
        catch
        {
            // 如果解析失败，直接显示原始内容
            ScreenplayJsonText = rawJson;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_jobItem is not TextToVideoJob textJob || textJob.ProjectData == null)
            return;

        try
        {
            // 校验一下是不是合法 JSON
            JsonDocument.Parse(ScreenplayJsonText);

            // 更新对象中的数据
            textJob.ProjectData.ScreenplayRawData = ScreenplayJsonText;

            // 调用队列管理器落库
            if (_jobManager != null)
            {
                await _jobManager.UpdateJobAsync(_jobItem);
            }

            StatusMessage = "✅ 剧本保存成功";
        }
        catch (JsonException)
        {
            StatusMessage = "❌ JSON 格式错误，请检查后再存";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ 保存出错：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAndContinueAsync()
    {
        // 先走一遍验证和保存逻辑
        await SaveAsync();
        
        if (!StatusMessage.Contains("✅")) return; // 如果保存没成功，就不继续
        
        // 只有流水线在等的时候才能“继续”
        if (_jobItem.ReviewGate != null && !_jobItem.ReviewGate.IsCompleted)
        {
            try 
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var doc = JsonSerializer.Deserialize<ScreenplayDocument>(ScreenplayJsonText, options);
                
                if (doc != null) 
                {
                    _jobItem.ReviewGate.Approve(doc);
                }
                else
                {
                    StatusMessage = "❌ 转换到验证强类型失败，结构不匹配。";
                    return;
                }
            } 
            catch (Exception ex)
            {
                StatusMessage = $"❌ 无法将当前剧本用于放行审核: {ex.Message}";
                return;
            }
        }

        StatusMessage = "✅ 剧本已确认，流水线继续运行...";
        
        // 延时关闭窗口
        await Task.Delay(500);
        ParentWindow?.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        if (_jobItem.ReviewGate != null && _jobItem.Status == JobStatus.WaitingForReview && !_jobItem.ReviewGate.IsCompleted)
        {
            // 取消则拒绝管线放行
            _jobItem.ReviewGate.Cancel();
        }
        ParentWindow?.Close();
    }
}
