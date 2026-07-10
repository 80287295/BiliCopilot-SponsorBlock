// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.SponsorBlock;

namespace BiliCopilot.UI.Services.Interfaces;

/// <summary>
/// SponsorBlock 服务接口.
/// </summary>
public interface ISponsorBlockService
{
    /// <summary>
    /// 获取视频的赞助片段（含缓存）.
    /// </summary>
    /// <param name="videoID">视频ID（BV号）.</param>
    /// <returns>赞助片段列表.</returns>
    Task<List<SponsorSegment>> GetSegmentsAsync(string videoID);

    /// <summary>
    /// 提交用户标记的片段.
    /// </summary>
    /// <param name="segment">片段数据.</param>
    /// <param name="userID">用户ID.</param>
    /// <returns>是否成功.</returns>
    Task<bool> SubmitSegmentAsync(SponsorSegment segment, string userID);

    /// <summary>
    /// 投票（赞成/反对某个片段）.
    /// </summary>
    /// <param name="uuid">片段UUID.</param>
    /// <param name="vote">投票值（1=赞成，-1=反对）.</param>
    /// <returns>是否成功.</returns>
    Task<bool> VoteSegmentAsync(string uuid, int vote);

    /// <summary>
    /// 检查当前位置是否需要跳过.
    /// </summary>
    /// <param name="videoID">视频ID.</param>
    /// <param name="currentTime">当前播放时间（秒）.</param>
    /// <returns>需要跳过的片段，无需跳过返回null.</returns>
    Task<SponsorSegment?> CheckShouldSkipAsync(string videoID, double currentTime);

    /// <summary>
    /// 配置.
    /// </summary>
    SponsorBlockConfig Config { get; set; }

    /// <summary>
    /// 是否启用.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 清除缓存.
    /// </summary>
    void ClearCache();
}
