namespace AutoCinema.Pro.Models.Screenplay;

/// <summary>
/// 完整结构化剧本
/// </summary>
public record ScreenplayDocument
{
    /// <summary>故事标题</summary>
    public string Title { get; init; } = "";

    /// <summary>需求说明（用户意图的提炼）</summary>
    public string Requirements { get; init; } = "";

    /// <summary>故事梗概（3-5行）</summary>
    public string Synopsis { get; init; } = "";

    /// <summary>包装风格描述（画面整体美术方向）</summary>
    public string WrappingStyle { get; init; } = "";

    /// <summary>角色设定列表（含旁白 R0）</summary>
    public List<CharacterSetup> Characters { get; init; } = [];

    /// <summary>场景设定列表</summary>
    public List<SceneSetup> Scenes { get; init; } = [];

    /// <summary>道具设定列表</summary>
    public List<PropSetup> Props { get; init; } = [];

    /// <summary>
    /// 分镜列表（数量由 LLM 根据内容自决，非用户预设）
    /// </summary>
    public List<Shot> Shots { get; init; } = [];

    /// <summary>制片说明/备注</summary>
    public string Notes { get; init; } = "";
}

/// <summary>角色设定</summary>
public record CharacterSetup
{
    /// <summary>角色编号，如 R0（旁白）、R1（主角）、R2（配角）</summary>
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";
    public string Appearance { get; init; } = "";       // 外观描述
    public string Role { get; init; } = "";             // 角色定位
    public string Personality { get; init; } = "";      // 性格特点

    /// <summary>声音描述（用于指导 TTS 和配音风格）</summary>
    public string VoiceDescription { get; init; } = "";
}

/// <summary>场景设定</summary>
public record SceneSetup
{
    /// <summary>场景编号，如 L1、L2</summary>
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";
    public string Atmosphere { get; init; } = "";       // 氛围描述
    public string TimeOfDay { get; init; } = "";        // 时间段（晨/暮/夜/日）
    public string VisualDescription { get; init; } = ""; // 视觉描述
}

/// <summary>道具设定</summary>
public record PropSetup
{
    /// <summary>道具编号，如 P1、P2</summary>
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string VisualEffect { get; init; } = "";     // 动态视觉效果描述
}

/// <summary>
/// 分镜（Shot）
/// 时长由 LLM 按情节紧凑度分配，非均分。
/// 通过 Id 引用角色/场景/道具，AIGC Prompt 构建时自动注入其描述。
/// </summary>
public record Shot
{
    /// <summary>镜头序号（从 1 开始）</summary>
    public int Index { get; init; }

    /// <summary>该镜头时长（秒），由 LLM 按起承转合节奏分配</summary>
    public int DurationSeconds { get; init; }

    /// <summary>景别：远景/全景/中景/近景/特写/大特写</summary>
    public string FrameType { get; init; } = "";

    /// <summary>摄像机视角：平视/仰拍/俯拍/侧拍</summary>
    public string CameraAngle { get; init; } = "";

    /// <summary>画面动作描述（人物/镜头运动）</summary>
    public string Action { get; init; } = "";

    /// <summary>转场方式：淡入/淡出/切换/溶解/推拉</summary>
    public string Transition { get; init; } = "切换";

    /// <summary>完整的 AIGC 视频/图像生成提示词（已拼入全局风格和被引用元素的描述）</summary>
    public string AigcPrompt { get; set; } = "";

    /// <summary>旁白文本（用于 TTS 合成），使用原文或 LLM 生成的叙述文字</summary>
    public string NarrationText { get; init; } = "";

    /// <summary>
    /// 配音归属者名称（如"旁白"、"红衣女子"）。
    /// 用于区分哪个角色的声音在该镜头配音。
    /// </summary>
    public string NarrationOwner { get; init; } = "旁白";

    /// <summary>
    /// 配音角色编号（对应 CharacterSetup.Id，如 R0/R1）。
    /// 旁白默认为 R0。
    /// </summary>
    public string VoiceCharacterId { get; init; } = "R0";

    /// <summary>角色台词（若有对话，即角色自己说的话）</summary>
    public string? CharacterDialogue { get; init; }

    /// <summary>所在场景编号（对应 SceneSetup.Id，如 L1）</summary>
    public string? SceneId { get; init; }

    /// <summary>所在场景名称（human-readable，辅助显示用）</summary>
    public string? SceneName { get; init; }

    /// <summary>
    /// 本镜头出现的角色 ID 列表（如 ["R1"]）。
    /// AIGC Prompt 构建时自动注入这些角色的外观描述。
    /// </summary>
    public List<string> CharacterIds { get; init; } = [];

    /// <summary>
    /// 本镜头出现的道具 ID 列表（如 ["P1", "P2"]）。
    /// AIGC Prompt 构建时自动注入这些道具的视觉效果描述。
    /// </summary>
    public List<string> PropIds { get; init; } = [];
}
