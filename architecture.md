# AutoCinema.Pro 系统架构图

```mermaid
graph TB
    %% 样式定义
    classDef core fill:#f9f,stroke:#333,stroke-width:2px;
    classDef service fill:#e1f5fe,stroke:#0277bd,stroke-width:2px;
    classDef model fill:#fff9c4,stroke:#fbc02d,stroke-width:2px;
    classDef external fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px;

    subgraph Application ["应用程序入口"]
        Program[Program.cs]:::core
        Config[appsettings.json]:::core
    end

    subgraph Pipeline ["核心流水线"]
        VideoPipeline[VideoProductionPipeline]:::core
    end

    subgraph DirectorLayer ["导演层 (Director)"]
        StoryboardService[StoryboardService]:::service
        VolcengineLlm[VolcengineLlmService]:::external
    end

    subgraph ActorLayer ["演员层 (Actor)"]
        ImageService[VolcengineImageService]:::service
        SpeechService[MiniMaxSpeechService]:::service
        VolcengineAPI["火山引擎 API"]:::external
        MiniMaxAPI["MiniMax API"]:::external
    end

    subgraph EditorLayer ["剪辑层 (Editor)"]
        SubtitleService[SrtSubtitleService]:::service
        VideoService[FFMpegVideoService]:::service
        AudioAnalysis[NAudioAnalysisService]:::service
        FFmpeg["FFmpeg Binaries"]:::external
    end

    subgraph Models [数据模型]
        VideoProject[VideoProject]:::model
        Storyboard[Storyboard]:::model
        Scene[Scene]:::model
        GeneratedAsset[GeneratedAsset]:::model
    end

    %% 依赖关系
    Program --> Config
    Program --> VideoPipeline
    
    VideoPipeline --> StoryboardService
    VideoPipeline --> ImageService
    VideoPipeline --> SpeechService
    VideoPipeline --> SubtitleService
    VideoPipeline --> VideoService
    VideoPipeline --> AudioAnalysis

    %% 导演层逻辑
    StoryboardService --> VolcengineLlm
    VolcengineLlm --> VolcengineAPI
    StoryboardService -.-> Storyboard
    Storyboard --> Scene

    %% 演员层逻辑
    ImageService --> VolcengineAPI
    SpeechService --> MiniMaxAPI
    ImageService -.-> GeneratedAsset
    SpeechService -.-> GeneratedAsset

    %% 剪辑层逻辑
    VideoService --> FFmpeg
    SubtitleService --> GeneratedAsset
    AudioAnalysis --> GeneratedAsset

    %% 数据流
    VideoProject --> VideoPipeline
    Scene --> ImageService
    Scene --> SpeechService
    GeneratedAsset --> VideoService
```

## 架构说明

### 1. 核心流水线 (Pipeline)

- **VideoProductionPipeline**: 系统的核心控制器，负责编排整个视频制作流程。它按顺序调用导演、演员和剪辑层的服务。

### 2. 导演层 (Director Layer)

- **StoryboardService**: 负责将用户的原始故事文本转换为结构化的分镜脚本。
- **VolcengineLlmService**: 调用火山引擎的大语言模型 (Doubao)，进行文本理解和分镜拆解。

### 3. 演员层 (Actor Layer)

- **VolcengineImageService**: 调用火山引擎 (Seedream) 生成高质量的场景图片。
- **MiniMaxSpeechService**: 调用 MiniMax TTS 生成逼真的语音旁白。

### 4. 剪辑层 (Editor Layer)

- **SrtSubtitleService**: 生成 SRT 字幕文件，确保字幕与语音时间轴对齐。
- **FFMpegVideoService**: 使用 FFmpeg 将图片、语音和字幕合成最终的视频文件。
- **NAudioAnalysisService**: 分析音频文件的时长，用于精确的时间轴控制。

### 5. 外部依赖 (External)

- **火山引擎 API**: 提供 LLM 和图片生成能力。
- **MiniMax API**: 提供语音合成能力。
- **FFmpeg**: 强大的多媒体处理工具，用于视频编码和合成。
