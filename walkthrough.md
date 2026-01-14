# AutoCinema.Pro - 项目完成 Walkthrough

## 概述

成功实现了基于 .NET 8 的 **AutoCinema.Pro** 自动化视频生成系统，采用**导演-演员-剪辑模型 (Director-Actor-Editor Model)**。

## 项目结构

```
e:\100.Work\NestCoreProject\制作图片视频\
├── AutoCinema.Pro.sln                    # 解决方案文件
└── src/AutoCinema.Pro/
    ├── AutoCinema.Pro.csproj             # 项目配置
    ├── Program.cs                        # 应用入口
    ├── appsettings.json                  # 配置文件
    ├── Models/                           # 领域模型
    │   ├── Scene.cs
    │   ├── Storyboard.cs
    │   ├── GeneratedAsset.cs
    │   └── VideoProject.cs
    ├── Configuration/                    # 配置类
    │   ├── LlmOptions.cs
    │   ├── VolcengineOptions.cs
    │   ├── MiniMaxOptions.cs
    │   └── PipelineOptions.cs
    ├── Services/
    │   ├── Director/                     # 导演层
    │   │   ├── IStoryboardService.cs
    │   │   └── StoryboardService.cs
    │   ├── Actor/                        # 演员层
    │   │   ├── IImageGenerationService.cs
    │   │   ├── VolcengineImageService.cs
    │   │   ├── ISpeechGenerationService.cs
    │   │   └── MiniMaxSpeechService.cs
    │   └── Editor/                       # 剪辑层
    │       ├── IAudioAnalysisService.cs
    │       ├── NAudioAnalysisService.cs
    │       ├── ISubtitleService.cs
    │       ├── SrtSubtitleService.cs
    │       ├── IVideoCompositionService.cs
    │       └── FFMpegVideoService.cs
    ├── Pipeline/                         # 流水线
    │   ├── IVideoProductionPipeline.cs
    │   └── VideoProductionPipeline.cs
    └── Infrastructure/
        └── Resilience/
            └── PollyPolicies.cs          # 重试策略
```

## 核心组件

### 1. 导演层 (Director Layer)

- [StoryboardService.cs](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/Services/Director/StoryboardService.cs)
- 使用 `IChatClient` 调用 LLM
- 将故事文本解析为结构化的场景列表
- 自动注入全局视觉风格前缀

### 2. 演员层 (Actor Layer)

- [VolcengineImageService.cs](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/Services/Actor/VolcengineImageService.cs): 火山引擎 Seedream 图片生成
- [MiniMaxSpeechService.cs](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/Services/Actor/MiniMaxSpeechService.cs): MiniMax TTS 语音合成
- 并发控制 (SemaphoreSlim)
- 指数退避重试 (Polly)
- 降级策略（生成占位图）

### 3. 剪辑层 (Processing Layer)

- [NAudioAnalysisService.cs](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/Services/Editor/NAudioAnalysisService.cs): 精确音频时长分析
- [SrtSubtitleService.cs](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/Services/Editor/SrtSubtitleService.cs): SRT 字幕生成，实现音画对齐算法
- [FFMpegVideoService.cs](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/Services/Editor/FFMpegVideoService.cs): 视频合成和字幕烧录

### 4. 流水线编排

- [VideoProductionPipeline.cs](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/Pipeline/VideoProductionPipeline.cs)
- 四阶段流程：解析 → 生成素材 → 生成字幕 → 合成视频
- 支持进度回调
- 并行素材生成 (Task.WhenAll)

## 技术栈

| 组件 | 技术 |
|------|------|
| 运行时 | .NET 8 (C# 12) |
| AI 抽象层 | Microsoft.Extensions.AI.OpenAI 10.1.1-preview |
| 图片生成 | 火山引擎 Ark REST API (Seedream) |
| 语音合成 | MiniMax TTS HTTP API |
| 音频分析 | NAudio 2.2.1 |
| 视频处理 | FFMpegCore 5.1.0 |
| 弹性重试 | Polly 8.4.0 |

## 构建验证

```
✅ 构建成功
   - 0 个错误
   - 1 个警告 (可忽略的 null 警告)
```

## 使用方法

### 1. 配置 API 密钥

编辑 [appsettings.json](file:///e:/100.Work/NestCoreProject/制作图片视频/src/AutoCinema.Pro/appsettings.json)：

```json
{
  "Llm": {
    "ApiKey": "<YOUR_OPENAI_API_KEY>",
    "Model": "gpt-4o"
  },
  "Volcengine": {
    "ApiKey": "<YOUR_VOLCENGINE_API_KEY>"
  },
  "MiniMax": {
    "ApiKey": "<YOUR_MINIMAX_API_KEY>",
    "GroupId": "<YOUR_GROUP_ID>"
  }
}
```

### 2. 安装 FFmpeg

确保 FFmpeg 已安装并添加到系统 PATH。

### 3. 运行程序

```bash
cd e:\100.Work\NestCoreProject\制作图片视频
dotnet run --project src/AutoCinema.Pro
```

## 下一步行动

1. **配置真实 API 密钥** 进行端到端测试
2. **安装 FFmpeg** 确保视频合成功能正常
3. **可选扩展**：
   - 添加 Web API 接口
   - 实现更多 LLM 提供商支持
   - 增加背景音乐混音功能
