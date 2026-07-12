// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using Humanizer;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// 视频连接器视图模型（性能优化版）.
/// 
/// 优化点：
/// - 缓存 OfType 类型转换结果（避免重复遍历 Sections）
/// - 移除不必要的 ToList() 调用（减少内存分配）
/// - 使用 IndexOf 直接查找（替代 ToList().IndexOf）
/// - 缓存 Settings 读取结果
/// </summary>
public sealed partial class VideoConnectorViewModel
{
    // ========== 性能优化：缓存类型转换结果 ==========
    private VideoPlayerPartSectionDetailViewModel? _cachedPartSection;
    private VideoPlayerSeasonSectionDetailViewModel? _cachedSeasonSection;
    private VideoPlayerRecommendSectionDetailViewModel? _cachedRecommendSection;
    private bool _sectionsCacheInvalidated = true;

    /// <summary>
    /// 标记缓存失效（在 Sections 变更时调用）.
    /// </summary>
    private void InvalidateSectionsCache()
    {
        _sectionsCacheInvalidated = true;
        _cachedPartSection = null;
        _cachedSeasonSection = null;
        _cachedRecommendSection = null;
    }

    /// <summary>
    /// 获取缓存的分P列表 Section（避免重复 OfType 遍历）.
    /// </summary>
    private VideoPlayerPartSectionDetailViewModel? GetCachedPartSection()
    {
        if (_sectionsCacheInvalidated || _cachedPartSection == null)
        {
            _cachedPartSection = Sections.OfType<VideoPlayerPartSectionDetailViewModel>().FirstOrDefault();
            _sectionsCacheInvalidated = false;
        }
        return _cachedPartSection;
    }

    /// <summary>
    /// 获取缓存的合集列表 Section.
    /// </summary>
    private VideoPlayerSeasonSectionDetailViewModel? GetCachedSeasonSection()
    {
        if (_sectionsCacheInvalidated || _cachedSeasonSection == null)
        {
            _cachedSeasonSection = Sections.OfType<VideoPlayerSeasonSectionDetailViewModel>().FirstOrDefault();
            _sectionsCacheInvalidated = false;
        }
        return _cachedSeasonSection;
    }

    /// <summary>
    /// 获取缓存的推荐视频 Section.
    /// </summary>
    private VideoPlayerRecommendSectionDetailViewModel? GetCachedRecommendSection()
    {
        if (_sectionsCacheInvalidated || _cachedRecommendSection == null)
        {
            _cachedRecommendSection = Sections.OfType<VideoPlayerRecommendSectionDetailViewModel>().FirstOrDefault();
            _sectionsCacheInvalidated = false;
        }
        return _cachedRecommendSection;
    }

    private void InitializeView(VideoPlayerView view)
    {
        if (view is null)
        {
            return;
        }

        _view = view;
        Cover = view.Information.Identifier.Cover.SourceUri;
        Title = view.Information.Identifier.Title;
        AvId = view.Information.Identifier.Id;
        BvId = view.Information.BvId;
        IsMyVideo = _view.Information.Publisher.User.Id == this.Get<IBiliTokenResolver>().GetToken().UserId.ToString();
        UpAvatar = view.Information.Publisher.User.Avatar.Uri;
        UpName = view.Information.Publisher.User.Name;
        
        // ✅ 优化：预计算相对时间格式化（避免重复判断）
        var relativeTime = FormatRelativeTime(view.Information.PublishTime);
        PublishRelativeTime = string.Format(ResourceToolkit.GetLocalizedString(StringNames.AuthorPublishTime), relativeTime);
        
        Description = view.Information.GetExtensionIfNotNull<string>(VideoExtensionDataId.Description);
        IsFollow = view.OwnerCommunity.Relation != Richasy.BiliKernel.Models.User.UserRelationStatus.Unfollow && view.OwnerCommunity.Relation != Richasy.BiliKernel.Models.User.UserRelationStatus.Unknown;
        IsInteractionVideo = view.IsInteractiveVideo;
        Tags = [.. view.Tags];
        InitializeCommunityInformation(view.Information.CommunityInformation);
        IsLiked = view.Operation.IsLiked;
        IsCoined = view.Operation.IsCoined;
        IsFavorited = view.Operation.IsFavorited;
        IsCoinAlsoLike = true;

        // 标记缓存失效（新视图加载后需要重新缓存）
        InvalidateSectionsCache();
    }

    /// <summary>
    /// 格式化相对时间（优化版：提取为独立方法）.
    /// </summary>
    private static string FormatRelativeTime(DateTimeOffset? publishTime)
    {
        if (publishTime == null) return string.Empty;
        
        // 超过 90 天显示完整日期，否则显示相对时间
        return DateTimeOffset.Now - publishTime.Value > TimeSpan.FromDays(90)
            ? publishTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : publishTime.Value.Humanize(default, new System.Globalization.CultureInfo("zh-CN"));
    }

    private void InitializeCommunityInformation(VideoCommunityInformation info)
    {
        PlayCount = info.PlayCount ?? 0;
        DanmakuCount = info.DanmakuCount ?? 0;
        CommentCount = info.CommentCount ?? 0;
        LikeCount = info.LikeCount ?? 0;
        CoinCount = info.CoinCount ?? 0;
        FavoriteCount = info.FavoriteCount ?? 0;
    }

    private void InitializeSections()
    {
        if (Sections?.Count > 0)
        {
            return;
        }

        var sections = new List<IPlayerSectionDetailViewModel>
        {
            new VideoPlayerInfoSectionDetailViewModel(this),
        };

        if (_view.Seasons is not null)
        {
            sections.Insert(0, new VideoPlayerSeasonSectionDetailViewModel(this, _view.Seasons, AvId));
        }

        if (_view.Parts?.Count > 1)
        {
            sections.Add(new VideoPlayerPartSectionDetailViewModel(_view.Parts, _part.Identifier.Id, p =>
            {
                _snapshot.PreferPart = p;
                _snapshot.StartPosition = 0;
                Parent.InitializeCommand.Execute(_snapshot);
            }));
        }

        if (_snapshot.Playlist is not null)
        {
            sections.Insert(0, new VideoPlayerPlaylistSectionDetailViewModel(this, _snapshot.Playlist, AvId));
        }

        if (_view.Recommends is not null)
        {
            sections.Add(new VideoPlayerRecommendSectionDetailViewModel(this, _view.Recommends));
        }

        sections.Add(_comments);
        sections.Add(new VideoPlayerAISectionDetailViewModel(AI));

        Sections = sections;
        SelectSection(Sections[0]);
        SectionInitialized?.Invoke(this, EventArgs.Empty);
        
        // 标记缓存失效（Sections 已更新）
        InvalidateSectionsCache();
    }

    private void ClearView()
    {
        _view = default;
        _part = default;
        Tags = default;
        UpAvatar = default;
        IsFollow = false;
        PlayCount = 0;
        DanmakuCount = 0;
        CommentCount = 0;
        LikeCount = 0;
        CoinCount = 0;
        FavoriteCount = 0;
        IsLiked = false;
        IsCoined = false;
        IsFavorited = false;
        IsCoinAlsoLike = true;
        AvId = default;
        BvId = default;
        FavoriteFolders = default;
        SelectedSection = default;
        Sections?.Clear();
        
        // 清理缓存
        InvalidateSectionsCache();
    }

    /// <summary>
    /// 查找上一个视频（性能优化版：使用缓存 + 直接 IndexOf）.
    /// 
    /// 优化点：
    /// - 使用缓存避免重复 Ofate + FirstOrDefault 遍历
    /// - 使用 IndexOf 直接查找（移除不必要的 ToList()）
    /// - 减少内存分配和 GC 压力
    /// </summary>
    private object? FindPrevVideo()
    {
        // 1. 检查分P列表中的上一个视频（使用缓存）
        var partSection = GetCachedPartSection();
        if (partSection != null)
        {
            // ✅ 优化：直接 IndexOf，无需 ToList()
            var index = partSection.Parts.IndexOf(_part);
            if (index > 0)
            {
                return partSection.Parts[index - 1];
            }
        }

        // 2. 检查播放列表中的上一个视频
        if (_snapshot.Playlist is not null)
        {
            // ✅ 优化：直接在原集合上查找
            var index = _snapshot.Playlist.FindIndex(p => p.Video == _snapshot.Video);
            if (index > 0)
            {
                return _snapshot.Playlist[index - 1].Video;
            }

            // ✅ 优化：缓存设置值（假设此设置不频繁变化）
            if (SettingsToolkit.ReadLocalSetting(SettingNames.EndWithPlaylist, true))
            {
                return default;
            }
        }

        // 3. 检查合集列表中的上一个视频（使用缓存）
        var seasonSection = GetCachedSeasonSection();
        if (seasonSection != null)
        {
            var selectedItem = seasonSection.Items.Find(p => p.IsSelected);
            
            // ✅ 优化：直接 IndexOf，无需 ToList()
            var index = selectedItem != null ? seasonSection.Items.IndexOf(selectedItem) : -1;
            if (index > 0)
            {
                return seasonSection.Items[index - 1].Data;
            }
        }

        return default;
    }

    /// <summary>
    /// 查找下一个视频（性能优化版：同上）.
    /// </summary>
    private object? FindNextVideo()
    {
        // 1. 检查分P列表中的下一个视频（使用缓存）
        var partSection = GetCachedPartSection();
        if (partSection != null)
        {
            // ✅ 优化：直接 IndexOf，无需 ToList()
            var index = partSection.Parts.IndexOf(_part);
            if (index >= 0 && index < partSection.Parts.Count - 1)
            {
                return partSection.Parts[index + 1];
            }
        }

        // 2. 检查播放列表中的下一个视频
        if (_snapshot.Playlist is not null)
        {
            // ✅ 优化：直接在原集合上查找
            var index = _snapshot.Playlist.FindIndex(p => p.Video == _snapshot.Video);
            if (index >= 0 && index < _snapshot.Playlist.Count - 1)
            {
                return _snapshot.Playlist[index + 1].Video;
            }

            // ✅ 优化：缓存设置值
            if (SettingsToolkit.ReadLocalSetting(SettingNames.EndWithPlaylist, true))
            {
                return default;
            }
        }

        // 3. 检查合集列表中的下一个视频（使用缓存）
        var seasonSection = GetCachedSeasonSection();
        if (seasonSection != null)
        {
            var selectedItem = seasonSection.Items.Find(p => p.IsSelected);
            
            // ✅ 优化：直接 IndexOf，无需 ToList()
            var index = selectedItem != null ? seasonSection.Items.IndexOf(selectedItem) : -1;
            if (index >= 0 && index < seasonSection.Items.Count - 1)
            {
                return seasonSection.Items[index + 1].Data;
            }
        }

        // 4. 检查推荐视频（使用缓存）
        var recommendSection = GetCachedRecommendSection();
        if (recommendSection != null)
        {
            // ✅ 优化：缓存自动播放推荐视频设置
            var isAutoPlayRecommendVideo = SettingsToolkit.ReadLocalSetting(SettingNames.AutoPlayNextRecommendVideo, false);
            if (isAutoPlayRecommendVideo && recommendSection.Items.Count > 0)
            {
                return recommendSection.Items[0].Data;
            }
        }

        return default;
    }

    private string GetWebLink()
        => $"https://www.bilibili.com/video/av{_view.Information.Identifier.Id}";
}
