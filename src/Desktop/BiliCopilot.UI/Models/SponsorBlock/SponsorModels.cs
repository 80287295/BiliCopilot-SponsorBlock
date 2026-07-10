// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Models.SponsorBlock;

/// <summary>
/// 赞助片段类别.
/// </summary>
public enum SponsorCategory
{
    /// <summary>
    /// 广告赞助.
    /// </summary>
    Sponsor,

    /// <summary>
    /// 自我推广.
    /// </summary>
    SelfPromo,

    /// <summary>
    /// 独家内容.
    /// </summary>
    ExclusiveAccess,

    /// <summary>
    /// 互动提示（三连）.
    /// </summary>
    Interaction,

    /// <summary>
    /// 高亮时刻.
    /// </summary>
    POI_Highlight,

    /// <summary>
    /// 开场动画.
    /// </summary>
    Intro,

    /// <summary>
    /// 结尾感谢.
    /// </summary>
    Outro,

    /// <summary>
    /// 预告.
    /// </summary>
    Preview,

    /// <summary>
    /// 填充.
    /// </summary>
    Padding,

    /// <summary>
    /// 废话.
    /// </summary>
    Filler,

    /// <summary>
    /// 无关音乐.
    /// </summary>
    Music_Offtopic,

    /// <summary>
    /// 不可跳过.
    /// </summary>
    Unpaid
}

/// <summary>
/// 跳过操作类型.
/// </summary>
public enum SponsorActionType
{
    /// <summary>
    /// 跳过.
    /// </summary>
    Skip,

    /// <summary>
    /// 静音.
    /// </summary>
    Mute,

    /// <summary>
    /// 完整标签.
    /// </summary>
    Full,

    /// <summary>
    /// 兴趣点.
    /// </summary>
    POI
}

/// <summary>
/// 赞助片段数据.
/// </summary>
public sealed class SponsorSegment
{
    /// <summary>
    /// 开始时间（秒）.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// 结束时间（秒）.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// 视频ID.
    /// </summary>
    public string? VideoID { get; set; }

    /// <summary>
    /// 片段唯一标识.
    /// </summary>
    public string? UUID { get; set; }

    /// <summary>
    /// 片段类别.
    /// </summary>
    public SponsorCategory Category { get; set; }

    /// <summary>
    /// 操作类型.
    /// </summary>
    public SponsorActionType Action { get; set; }

    /// <summary>
    /// 是否被锁定.
    /// </summary>
    public int? Locked { get; set; }

    /// <summary>
    /// 用户ID.
    /// </summary>
    public string? UserID { get; set; }

    /// <summary>
    /// 视频时长.
    /// </summary>
    public double? VideoDuration { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 视频的赞助片段响应.
/// </summary>
public sealed class SponsorVideoResponse
{
    /// <summary>
    /// 视频ID.
    /// </summary>
    public string? VideoID { get; set; }

    /// <summary>
    /// 赞助片段列表.
    /// </summary>
    public List<SponsorSegment> Segments { get; set; } = new();
}

/// <summary>
/// 空降助手配置.
/// </summary>
public sealed class SponsorBlockConfig
{
    /// <summary>
    /// 是否启用空降助手.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否自动跳过.
    /// </summary>
    public bool AutoSkip { get; set; } = true;

    /// <summary>
    /// 是否显示跳过提示.
    /// </summary>
    public bool ShowSkipNotice { get; set; } = true;

    /// <summary>
    /// 是否允许用户提交片段.
    /// </summary>
    public bool AllowSubmission { get; set; } = true;

    /// <summary>
    /// 各类别跳过选项.
    /// </summary>
    public Dictionary<SponsorCategory, CategorySkipOption> CategoryOptions { get; set; } = new();

    /// <summary>
    /// 服务器地址.
    /// </summary>
    public string ServerAddress { get; set; } = "https://www.bsbsb.top";

    /// <summary>
    /// 是否使用测试服务器.
    /// </summary>
    public bool UseTestingServer { get; set; }

    /// <summary>
    /// 测试服务器地址.
    /// </summary>
    public string TestingServerAddress { get; set; } = "http://127.0.0.1:9876";
}

/// <summary>
/// 类别跳过选项.
/// </summary>
public enum CategorySkipOption
{
    /// <summary>
    /// 禁用.
    /// </summary>
    Disabled = -1,

    /// <summary>
    /// 显示叠加层（手动跳过）.
    /// </summary>
    ShowOverlay = 0,

    /// <summary>
    /// 手动跳过.
    /// </summary>
    ManualSkip = 1,

    /// <summary>
    /// 自动跳过.
    /// </summary>
    AutoSkip = 2
}
