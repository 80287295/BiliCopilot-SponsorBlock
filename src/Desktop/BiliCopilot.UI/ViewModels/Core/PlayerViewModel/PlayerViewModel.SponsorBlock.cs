// Copyright (c) Bili Copilot. All rights reserved.

#pragma warning disable IDE0005
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Models.SponsorBlock;
using BiliCopilot.UI.Services;
using BiliCopilot.UI.Toolkits;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
#pragma warning restore IDE0005

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 播放器视图模型的空降助手部分.
/// </summary>
public sealed partial class PlayerViewModel
{
    private List<SponsorSegment> _sponsorSegments = new();
    private SponsorSegment? _currentSponsorSegment;
    private bool _isSkipping = false;
    private DispatcherTimer? _sponsorNoticeTimer;

    /// <summary>
    /// 赞助片段列表.
    /// </summary>
    public IReadOnlyList<SponsorSegment> SponsorSegments => _sponsorSegments.AsReadOnly();

    /// <summary>
    /// 是否启用空降助手.
    /// </summary>
    [ObservableProperty]
    private bool _isSponsorBlockEnabled = true;

    /// <summary>
    /// 是否自动跳过赞助片段.
    /// </summary>
    [ObservableProperty]
    private bool _autoSkipSponsor = true;

    /// <summary>
    /// 当前跳过的赞助片段.
    /// </summary>
    public SponsorSegment? CurrentSponsorSegment => _currentSponsorSegment;

    /// <summary>
    /// 是否显示空降助手跳过提示.
    /// </summary>
    [ObservableProperty]
    private bool _isSponsorSkipNoticeVisible;

    /// <summary>
    /// 空降助手跳过提示文本.
    /// </summary>
    [ObservableProperty]
    private string _sponsorSkipNoticeText = string.Empty;

    /// <summary>
    /// 跳过通知事件发生.
    /// </summary>
    public event EventHandler<string>? SponsorSkipOccurred;

    /// <summary>
    /// 加载赞助片段数据.
    /// </summary>
    private async Task LoadSponsorSegmentsAsync()
    {
        // 每次加载时同步设置页面的最新值
        IsSponsorBlockEnabled = SettingsToolkit.ReadLocalSetting(SettingNames.SponsorBlockEnabled, true);
        AutoSkipSponsor = SettingsToolkit.ReadLocalSetting(SettingNames.AutoSkipSponsor, true);

        if (!IsSponsorBlockEnabled)
        {
            return;
        }

        try
        {
            var videoID = Connector switch
            {
                VideoConnectorViewModel videoVM => videoVM.BvId,
                PgcConnectorViewModel => null, // PGC 暂不支持
                _ => null,
            };

            if (string.IsNullOrEmpty(videoID))
            {
                return;
            }

            var sponsorService = this.Get<SponsorBlockService>();
            _sponsorSegments = await sponsorService.GetSegmentsAsync(videoID);
            OnPropertyChanged(nameof(SponsorSegments));

            if (_sponsorSegments.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Loaded {_sponsorSegments.Count} sponsor segments");

                // 订阅跳过事件
                SponsorSkipOccurred -= OnSponsorSkipOccurred;
                SponsorSkipOccurred += OnSponsorSkipOccurred;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load sponsor segments: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查是否需要跳过赞助片段.
    /// </summary>
    private async Task CheckAndSkipSponsorAsync(double position)
    {
        if (!IsSponsorBlockEnabled || !AutoSkipSponsor || _sponsorSegments.Count == 0 || _isSkipping)
        {
            return;
        }

        foreach (var segment in _sponsorSegments)
        {
            // 如果当前位置在赞助片段的开始时间内（允许 0.5 秒误差）
            if (position >= segment.StartTime - 0.5 && position < segment.EndTime)
            {
                // 如果已经在跳过这个片段，不再重复跳过
                if (_currentSponsorSegment?.UUID == segment.UUID)
                {
                    return;
                }

                // 防止重入：ChangePositionAsync 会触发 Position 变化 → 再次进入本方法
                _isSkipping = true;
                try
                {
                    // 跳过该片段
                    _currentSponsorSegment = segment;
                    await ChangePositionAsync(segment.EndTime);
                    SponsorSkipOccurred?.Invoke(this, GetSponsorCategoryName(segment.Category));
                }
                finally
                {
                    _isSkipping = false;
                }
                return;
            }
        }

        // 如果不在任何赞助片段内，清除当前片段
        if (_currentSponsorSegment != null && position >= _currentSponsorSegment.EndTime)
        {
            _currentSponsorSegment = null;
        }
    }

    /// <summary>
    /// 获取赞助类别名称.
    /// </summary>
    private string GetSponsorCategoryName(SponsorCategory category)
    {
        return category switch
        {
            SponsorCategory.Sponsor => "赞助广告",
            SponsorCategory.SelfPromo => "自我推广",
            SponsorCategory.Intro => "开场动画",
            SponsorCategory.Outro => "结尾感谢",
            SponsorCategory.Interaction => "互动提示",
            SponsorCategory.POI_Highlight => "高亮时刻",
            _ => "赞助片段",
        };
    }

    /// <summary>
    /// 处理跳过事件，显示提示.
    /// </summary>
    private void OnSponsorSkipOccurred(object? sender, string categoryName)
    {
        ShowSponsorSkipNotice($"已跳过: {categoryName}");
    }

    /// <summary>
    /// 显示空降助手跳过提示（自动 2 秒后消失）.
    /// </summary>
    private void ShowSponsorSkipNotice(string message)
    {
        SponsorSkipNoticeText = message;
        IsSponsorSkipNoticeVisible = true;

        // 重置计时器
        if (_sponsorNoticeTimer != null)
        {
            _sponsorNoticeTimer.Stop();
            _sponsorNoticeTimer.Tick -= OnSponsorNoticeTimerTick;
        }

        _sponsorNoticeTimer = new DispatcherTimer();
        _sponsorNoticeTimer.Interval = TimeSpan.FromSeconds(2);
        _sponsorNoticeTimer.Tick += OnSponsorNoticeTimerTick;
        _sponsorNoticeTimer.Start();
    }

    private void OnSponsorNoticeTimerTick(object? sender, object e)
    {
        if (_sponsorNoticeTimer != null)
        {
            _sponsorNoticeTimer.Stop();
            _sponsorNoticeTimer.Tick -= OnSponsorNoticeTimerTick;
            _sponsorNoticeTimer = null;
        }

        IsSponsorSkipNoticeVisible = false;
    }
}
