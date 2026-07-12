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
public sealed partial class PlayerViewModel : IDisposable
{
    private List<SponsorSegment> _sponsorSegments = new();
    private SponsorSegment? _currentSponsorSegment;
    private bool _isSkipping;
    private DispatcherTimer? _sponsorNoticeTimer;
    
    // ========== 性能优化：节流机制 ==========
    private DateTime _lastSponsorCheckTime = DateTime.MinValue;
    private const int SponsorCheckIntervalMs = 500; // 500ms 检查一次（从 10-60次/秒 → 2次/秒）
    
    // ========== 性能优化：Settings 缓存 ==========
    private static bool _cachedSponsorBlockEnabled = true;
    private static bool _cachedAutoSkipSponsor = true;

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
    /// 从缓存获取设置值（无 I/O 开销）.
    /// </summary>
    public static bool CachedSponsorBlockEnabled => _cachedSponsorBlockEnabled;
    public static bool CachedAutoSkipSponsor => _cachedAutoSkipSponsor;

    // ========== 性能优化：DispatcherTimer 单例模式 ==========
    
    /// <summary>
    /// 初始化 DispatcherTimer（仅调用一次）.
    /// </summary>
    private void InitializeSponsorNoticeTimer()
    {
        if (_sponsorNoticeTimer == null)
        {
            _sponsorNoticeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _sponsorNoticeTimer.Tick += OnSponsorNoticeTimerTick;
        }
    }

    /// <summary>
    /// 加载赞助片段数据（使用缓存设置值）.
    /// </summary>
    private async Task LoadSponsorSegmentsAsync()
    {
        // ✅ 优化：使用缓存值（零 I/O 开销）
        if (!CachedSponsorBlockEnabled)
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
    /// 检查是否需要跳过赞助片段（优化版：节流 + 二分查找）.
    /// 
    /// 性能提升：
    /// - 调用频率：10-60 次/秒 → 2 次/秒（节流机制）
    /// - 查找复杂度：O(n) → O(log n)（二分查找）
    /// </summary>
    private async Task CheckAndSkipSponsorAsync(double position)
    {
        // ✅ 使用缓存设置值
        if (!CachedSponsorBlockEnabled || !CachedAutoSkipSponsor || 
            _sponsorSegments.Count == 0 || _isSkipping)
        {
            return;
        }

        // ✅ 节流机制：限制检查频率为 2 次/秒
        var now = DateTime.UtcNow;
        if ((now - _lastSponsorCheckTime).TotalMilliseconds < SponsorCheckIntervalMs)
        {
            return;
        }
        _lastSponsorCheckTime = now;

        try
        {
            // ✅ 使用二分查找替代线性扫描
            var segment = FindCurrentSegmentBinary(position);
            
            if (segment != null && _currentSponsorSegment?.UUID != segment.UUID)
            {
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
            }
            else if (_currentSponsorSegment != null && position >= _currentSponsorSegment.EndTime)
            {
                // 如果不在任何赞助片段内，清除当前片段
                _currentSponsorSegment = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in CheckAndSkipSponsor: {ex.Message}");
        }
    }

    /// <summary>
    /// 二分查找当前位置对应的赞助片段.
    /// 时间复杂度: O(log n) 替代 O(n).
    /// 前提条件：_sponsorSegments 已按 StartTime 排序（在 GetSegmentsAsync 中已排序）.
    /// </summary>
    private SponsorSegment? FindCurrentSegmentBinary(double position)
    {
        if (_sponsorSegments.Count == 0)
            return null;

        int left = 0, right = _sponsorSegments.Count - 1;
        
        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            var midSegment = _sponsorSegments[mid];
            
            if (position >= midSegment.StartTime - 0.5 && position < midSegment.EndTime)
            {
                return midSegment; // 找到匹配片段
            }
            else if (position < midSegment.StartTime - 0.5)
            {
                right = mid - 1; // 在左半部分继续查找
            }
            else
            {
                left = mid + 1;  // 在右半部分继续查找
            }
        }

        return null; // 未找到匹配片段
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
    /// 显示空降助手跳过提示（优化版：Timer 复用）.
    /// 
    /// 内存优化：
    /// - Timer 对象数：N 个 → 1 个（整个生命周期只创建一次）
    /// - GC 压力：显著减少（避免短生命周期对象频繁分配）
    /// </summary>
    private void ShowSponsorSkipNotice(string message)
    {
        // 确保 Timer 已初始化（单例模式）
        InitializeSponsorNoticeTimer();

        SponsorSkipNoticeText = message;
        IsSponsorSkipNoticeVisible = true;

        // ✅ 复用同一个 Timer 实例（重置并启动）
        _sponsorNoticeTimer!.Stop();
        _sponsorNoticeTimer!.Start();
    }

    private void OnSponsorNoticeTimerTick(object? sender, object e)
    {
        IsSponsorSkipNoticeVisible = false;
        _sponsorNoticeTimer?.Stop(); // 停止但不销毁
    }

    /// <summary>
    /// 当空降助手开关状态改变时（优化版：更新缓存 + 持久化）.
    /// </summary>
    partial void OnIsSponsorBlockEnabledChanged(bool value)
    {
        // ✅ 更新静态缓存
        _cachedSponsorBlockEnabled = value;
        
        // 持久化到存储
        SettingsToolkit.WriteLocalSetting(SettingNames.SponsorBlockEnabled, value);

        // 如果关闭空降助手，清除当前片段并隐藏跳过提示
        if (!value)
        {
            _currentSponsorSegment = null;
            _sponsorSegments.Clear();
            OnPropertyChanged(nameof(SponsorSegments));
            IsSponsorSkipNoticeVisible = false;

            System.Diagnostics.Debug.WriteLine("空降助手已关闭");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("空降助手已开启");
            // 重新加载赞助片段
            _ = LoadSponsorSegmentsAsync();
        }
    }

    /// <summary>
    /// 当自动跳过开关状态改变时（新增：更新缓存 + 持久化）.
    /// </summary>
    partial void OnAutoSkipSponsorChanged(bool value)
    {
        // ✅ 更新静态缓存
        _cachedAutoSkipSponsor = value;
        
        // 持久化到存储
        SettingsToolkit.WriteLocalSetting(SettingNames.AutoSkipSponsor, value);

        System.Diagnostics.Debug.WriteLine($"自动跳过设置更新为: {value}");
    }

    // ========== IDisposable 实现 ==========
    
    /// <summary>
    /// 释放资源（防止内存泄漏）.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的具体实现.
    /// </summary>
    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 清理 Timer 资源
            if (_sponsorNoticeTimer != null)
            {
                _sponsorNoticeTimer.Stop();
                _sponsorNoticeTimer.Tick -= OnSponsorNoticeTimerTick;
                _sponsorNoticeTimer = null;
            }

            // 取消事件订阅
            SponsorSkipOccurred -= OnSponsorSkipOccurred;
            
            // 清理数据
            _sponsorSegments.Clear();
            _currentSponsorSegment = null;
            
            System.Diagnostics.Debug.WriteLine("PlayerViewModel.SponsorBlock 资源已释放");
        }
    }
}
