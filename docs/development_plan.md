# AutoCinema 通用任务系统开发计划

## 1. 概述

本文档旨在规划 AutoCinema 从单一的"故事转视频"工具向"通用多媒体处理平台"的演进。我们将重构核心任务系统，使其支持多种类型的后台任务（如图片转视频、视频转译、格式转换等），并提供详细的新功能集成指南。

---

## 2. 阶段一：UI 体验优化 (已完成)

### 目标
解决 `BatchJobWindow` 在无边框模式下缺失标准窗口控制的问题，提升桌面端用户体验。

### 实施内容
- **窗口控制**: 在右上角添加了最小化 (`_`)、最大化/还原 (`□`) 和关闭 (`✕`) 按钮。
- **视觉风格**: 按钮样式与深色主题无缝融合，关闭按钮悬停显示红色警示。
- **交互逻辑**: 实现了标准的窗口操作逻辑，点击关闭仅隐藏窗口而非退出程序。

---

## 3. 阶段二：通用任务系统架构 (Architecture V2)

目前系统强耦合于 `VideoProject` (文本->视频) 模型。我们需要抽象出通用的 `JobItem`。

### 3.1 核心数据模型设计

引入 `JobItem` 作为所有后台任务的基类，`VideoProject` 将只是一种特殊的 Payload。

```csharp
namespace AutoCinema.Core.Models.Jobs;

// 1. 任务类型枚举
public enum JobType
{
    TextOnVideo,    // 经典的"故事转视频" (Director-Actor-Editor)
    ImageToVideo,   // 新增：图片生成视频
    VideoResizing,  // 工具类：视频裁切/比例转换
    AudioDubbing    // 工具类：视频配音
}

// 2. 任务状态枚举 (复用现有，或扩展)
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

// 3. 任务基类
public abstract class JobItem
{
    public string JobId { get; set; } = Guid.NewGuid().ToString("N");
    public JobType Type { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? FinishedAt { get; set; }
    
    public string Title { get; set; }      // 任务标题 (UI显示)
    public string OutputPath { get; set; } // 结果路径
    public string ErrorMessage { get; set; }
    
    // 进度信息 (包含百分比和当前步骤描述)
    public ProductionProgress Progress { get; set; } = new();
}

// 4. 具体任务实现：故事转视频
public class TextToVideoJob : JobItem
{
    public VideoProject ProjectData { get; set; } // 复用现有的 VideoProject 数据结构
}

// 5. 具体任务实现：图片转视频 (新功能)
public class ImageToVideoJob : JobItem
{
    public List<string> InputImagePaths { get; set; } = new();
    public string Prompt { get; set; }         // 运镜/特效提示词
    public double Duration { get; set; } = 5.0; // 目标时长
}
```

### 3.2 服务层重构

将 `JobManager` 改造为通用调度器，配合具体的 `JobHandler`。

```csharp
public interface IJobHandler
{
    JobType SupportedType { get; }
    Task ExecuteAsync(JobItem job, IProgress<ProductionProgress> progress, CancellationToken ct);
}

public class JobManager : IJobManager
{
    // 注册所有的 Handlers
    private readonly IEnumerable<IJobHandler> _handlers;
    private readonly ConcurrentQueue<JobItem> _queue = new();
    
    // ... 调度逻辑 ...
    
    private async Task ProcessJobAsync(JobItem job)
    {
        var handler = _handlers.FirstOrDefault(h => h.SupportedType == job.Type);
        if (handler == null) throw new NotSupportedException($"No handler for {job.Type}");
        
        await handler.ExecuteAsync(job, progress, ct);
    }
}
```

---

## 4. 阶段三：新功能集成指南

### 4.1 场景 A：集成现有的"故事转视频"功能

目前我们直接使用 `VideoProject`。迁移到新架构的步骤：

1. **封装**: 创建一个 `TextToVideoJobHandler`，实现 `IJobHandler`。
2. **迁移逻辑**: 将 `VideoProductionPipeline.ProduceAsync` 的调用逻辑移动到 `ExecuteAsync` 中。
3. **UI 适配**: `MainWindow` 的"添加到队列"按钮，不再直接调 `JobManager.Enqueue(Project)`, 而是：
   ```csharp
   var job = new TextToVideoJob 
   { 
       Type = JobType.TextOnVideo, 
       Title = project.Title,
       ProjectData = project 
   };
   _jobManager.EnqueueAsync(job);
   ```

### 4.2 场景 B：开发"图片转视频"功能 (Image-to-Video)

这是一个全新的功能，假设我们要接入类似 Runway 或 SVD (Stable Video Diffusion) 的 API，或者只是简单的将图片合成 MP4 (Slideshow without AI)。

#### 开发步骤：

1. **定义模型**: 创建 `ImageToVideoJob` 类 (入参：图片列表、FPS、转场特效)。
2. **实现服务**: 创建 `ImageToVideoService` (核心逻辑)。
   - 步骤 1: 图像预处理 (缩放/裁剪)。
   - 步骤 2: (可选) 调用 AI 视频生成 API (如 Runway Gen-2)。
   - 步骤 3: 或调用 FFmpeg 将静态图合成为视频流。
3. **实现 Handler**: 创建 `ImageToVideoHandler`，包装上述服务调用。
4. **UI 开发**:
   - 新建 `ImageToVideoWindow.axaml`。
   - 提供拖拽上传图片区域。
   - 提供参数设置 (时长、FPS)。
   - "开始处理"按钮构建 `ImageToVideoJob` 并提交给 `JobManager`。

---

## 5. 任务通用性思考

为了确保系统的可扩展性，我们在设计时应遵循以下原则：

1. **UI 与 逻辑分离**: `JobManager` 不应感知 UI (如 Avalonia 类型)，只处理数据模型。
2. **进度统一接口**: 所有任务都必须通过 `IProgress<ProductionProgress>` 报告进度，确保 `BatchJobWindow` 可以统一显示进度条。
3. **结果多态**: 虽然大部分结果是文件路径 (`OutputPath`)，如果任务产生多个文件，可以扩展 `ResultPayload` 字段。
4. **持久化**: 考虑到程序关闭重启，`JobItem` 应该能被序列化 (JSON/Database)。建议使用 LiteDB 或 SQLite 存储 `Jobs` 表。

## 6. 后续行动建议

1. **优先重构 Core**: 按照 3.1 和 3.2 定义接口和基类。
2. **适配现有功能**: 确保 `VideoProject` 能无缝切换到 `JobItem` 模式，不破坏现有流程。
3. **开发新功能 Demo**: 尝试做一个简单的 "MP3 转 MP4 (加封面)" 任务，验证通用架构的灵活性。
