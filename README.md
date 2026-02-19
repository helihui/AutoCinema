# AutoCinema.Pro

> 🎬 **文本生成图片视频+自动字幕** - 基于 AI 的自动化视频生成系统

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

AutoCinema.Pro 是一个创新的自动化视频生成系统，采用**导演-演员-剪辑模型 (Director-Actor-Editor Model)**，能够将简单的文本故事自动转换为带有图片、语音和字幕的完整视频。

本项目现已包含功能强大的桌面客户端，支持可视化操作和批量任务管理。

## ✨ 核心特性

- 🖥️ **可视化桌面应用**: 基于 Avalonia UI 的跨平台客户端，操作直观便捷。
- 📋 **批量任务队列**: 支持添加多个视频生成任务，后台自动排队处理，高效稳定。
- 🎭 **导演层 (Director)**: 使用 LLM (火山引擎 Doubao) 将故事文本智能拆解为结构化分镜脚本。
- 🎨 **演员层 (Actor)**: 并行生成高质量图片 (火山引擎 Seedream) 和逼真语音 (MiniMax)。
- ✂️ **剪辑层 (Editor)**: 自动合成视频、生成字幕并实现音画对齐。
- 🔄 **弹性重试**: 内置 Polly 重试策略，确保 API 调用稳定性。
- 📊 **实时进度**: 桌面客户端提供详细的进度追踪和状态显示。

## 🏗️ 系统架构

本项目采用分层架构设计，确保各模块职责清晰，易于扩展。

```mermaid
graph TB
    subgraph Client ["客户端层"]
        DesktopApp[Desktop App (Avalonia)]
        BatchManager[Batch Job Manager]
    end

    subgraph Core ["核心业务层"]
        Director[Director Service]
        Actor[Actor Service]
        Editor[Editor Service]
        ProjectService[Project Service]
    end

    subgraph Infrastructure ["基础设施层"]
        LlmClient[Volcengine LLM]
        ImageClient[Volcengine Image]
        SpeechClient[MiniMax Speech]
        FFmpeg[FFmpeg Processing]
    end

    Client --> Core
    BatchManager --> Core
    Core --> Infrastructure
```

详细架构说明请参考 [architecture.md](architecture.md)。

## 🚀 快速开始

### 前置要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [FFmpeg](https://ffmpeg.org/download.html) (已包含在 `src/ffmpeg` 目录中)
- API 密钥:
  - **火山引擎**: 用于 LLM 和 图片生成
  - **MiniMax**: 用于 语音合成

### 安装与运行 (Desktop 客户端)

1. **克隆仓库**
   ```bash
   git clone https://github.com/helihui/AutoCinema.git
   cd AutoCinema
   ```

2. **配置 API 密钥**
   编辑 `src/AutoCinema.Desktop/appsettings.json`，填入你的 API 密钥：
   ```json
   {
     "Llm": { ... },
     "Volcengine": { ... },
     "MiniMax": { ... }
   }
   ```
   *或者直接在桌面客户端的设置界面中配置（开发中）。*

3. **运行桌面应用**
   ```bash
   cd src/AutoCinema.Desktop
   dotnet run
   ```

## 📖 文档指南

- **用户手册**: [docs/manual.md](docs/manual.md) - 详细的功能介绍和操作指南。
- **更新日志**: [updates/history.md](updates/history.md) - 查看项目更新历史。
- **API 文档**: [API.md](API.md) - 后端服务接口说明 (供开发者参考)。

## 🛠️ 技术栈

- **UI 框架**: Avalonia UI 11.0+
- **核心运行时**: .NET 8.0 / C# 12
- **AI 服务**: Volcengine (Doubao/Seedream), MiniMax
- **多媒体处理**: FFmpeg, NAudio
- **数据存储**: SQLite (本地项目数据)

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件。
