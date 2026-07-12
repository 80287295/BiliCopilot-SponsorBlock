// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Models;
using BiliCopilot.UI.Models.Constants;
using BiliCopilot.UI.Toolkits;
using BiliCopilot.UI.ViewModels.Components;
using BiliCopilot.UI.ViewModels.Items;
using CommunityToolkit.Mvvm.Input;
using Richasy.BiliKernel.Models;
using Richasy.BiliKernel.Models.Media;

namespace BiliCopilot.UI.ViewModels.Core;

/// <summary>
/// PGC（番剧/影视）连接器视图模型（性能优化版）.
/// 
/// 优化点：
/// - 缓存 OfType 类型转换结果
/// - 移除不必要的 ConvertAll/ToList 调用
/// - 使用直接查找替代集合拷贝
/// </summary>
public sealed partial class PgcConnectorViewModel
{
    // ========== 性能优化：缓存 ==========
    private PgcPlayerEpisodeSectionDetailViewModel? _cachedEpisodeSection;
    private bool _sectionsCacheInvalidated = true;

    /// <summary>
    /// 标记缓存失效.
    /// </summary>
    private void InvalidateSectionsCache()
    {
        _sectionsCacheInvalidated = true;
        _cachedEpisodeSection = null;
    }

    /// <summary>
    /// 获取缓存的剧集列表 Section（避免重复遍历）.
    /// </summary>
    private PgcPlayerEpisodeSectionDetailViewModel? GetCachedEpisodeSection()
    {
        if (_sectionsCacheInvalidated || _cachedEpisodeSection == null)
        {
            _cachedEpisodeSection = Sections?.OfType<PgcPlayerEpisodeSectionDetailViewModel>().FirstOrDefault();
            _sectionsCacheInvalidated = false;
        }
        return _cachedEpisodeSection;
    }

    private void InitializeView(PgcPlayerView view)
    {
        if (view is null)
        {
            return;
        }

        _view = view;
        _type = view.Information.GetExtensionIfNotNull<EntertainmentType>(SeasonExtensionDataId.PgcType);
        Cover = view.Information.Identifier.Cover.SourceUri;
        SeasonTitle = view.Information.Identifier.Title;
        
        // ✅ 优化：提取 Alias 逻辑为独立方法（更清晰）
        Description = view.Information.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Description);
        ResolveAlias(view.Information);
        
        SeasonId = view.Information.Identifier.Id;
        IsFollow = view.Information.IsTracking ?? false;
        
        // ✅ 优化：批量初始化社区信息
        InitializeCommunityInformation(view.Information.CommunityInformation);

        // 标记缓存失效
        InvalidateSectionsCache();
    }

    /// <summary>
    /// 解析别名/副标题（优化版：逻辑清晰化）.
    /// </summary>
    private void ResolveAlias(SeasonInformation info)
    {
        Alias = info.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Alias);
        
        if (string.IsNullOrEmpty(Alias))
        {
            Alias = info.GetExtensionIfNotNull<string>(SeasonExtensionDataId.Subtitle);
        }
    }

    /// <summary>
    /// 批量初始化社区信息（优化版：减少重复代码）.
    /// </summary>
    private void InitializeCommunityInformation(VideoCommunityInformation info)
    {
        PlayCount = info.PlayCount ?? 0;
        DanmakuCount = info.DanmakuCount ?? 0;
        CommentCount = info.CommentCount ?? 0;
        LikeCount = info.LikeCount ?? 0;
        CoinCount = info.CoinCount ?? 0;
        FavoriteCount = info.FavoriteCount ?? 0;
        FollowCount = info.TrackCount ?? 0;
    }

    private void ClearView()
    {
        _view = default;
        Cover = default;
        SeasonTitle = default;
        IsFollow = false;
        PlayCount = 0;
        DanmakuCount = 0;
        CommentCount = 0;
        LikeCount = 0;
        CoinCount = 0;
        FavoriteCount = 0;
        FollowCount = 0;
        IsLiked = false;
        IsCoined = false;
        IsFavorited = false;
        IsCoinAlsoLike = true;
        FavoriteFolders = default;
        Sections?.Clear();
        SelectedSection = default;

        // 清理缓存
        InvalidateSectionsCache();
    }

    private void InitializeSections()
    {
        if (Sections?.Count > 0)
        {
            return;
        }

        var sections = new List<IPlayerSectionDetailViewModel>
        {
            new PgcPlayerInfoSectionDetailViewModel(this),
        };

        if (_view.Episodes?.Count > 1)
        {
            sections.Add(new PgcPlayerEpisodeSectionDetailViewModel(_view.Episodes, _episode.Identifier.Id, ChangeEpisode));
        }

        if (_view.Seasons?.Count > 1)
        {
            sections.Add(new PgcPlayerSeasonSectionDetailViewModel(_view.Seasons, _view.Information.Identifier.Id, ChangeSeason));
        }

        sections.Add(_comments);

        Sections = sections;
        SelectSection(Sections[0]);
        SectionInitialized?.Invoke(this, EventArgs.Empty);

        // 标记缓存失效
        InvalidateSectionsCache();
    }

    private EpisodeInformation? FindInitialEpisode(string? initialEpisodeId)
    {
        EpisodeInformation? playEpisode = default;
        
        if (!string.IsNullOrEmpty(initialEpisodeId))
        {
            // ✅ 优化：使用 FirstOrDefault 替代 Find（IList 不支持 Find）
            playEpisode = _view.Episodes.FirstOrDefault(p => p.Identifier.Id == initialEpisodeId);
        }

        if (playEpisode == null)
        {
            var historyEpisodeId = _view.Progress?.Cid;
            
            // ✅ 优化：短路求值，避免不必要的设置读取
            if (!string.IsNullOrEmpty(historyEpisodeId) && 
                SettingsToolkit.ReadLocalSetting(SettingNames.AutoLoadHistory, true))
            {
                playEpisode = _view.Episodes.FirstOrDefault(p => p.Identifier.Id == historyEpisodeId);
            }
        }

        return playEpisode ?? _view.Episodes.FirstOrDefault();
    }

    /// <summary>
    /// 查找下一个剧集（性能优化版：使用缓存 + 直接遍历）.
    /// 
    /// 优化点：
    /// - 使用缓存避免重复 Ofate + FirstOrDefault
    /// - 直接在 Episodes 集合上查找（移除 ConvertAll + ToList）
    /// </summary>
    private EpisodeInformation? FindNextEpisode()
    {
        if (Sections is null)
        {
            return default;
        }

        // ✅ 使用缓存的 Episode Section
        var episodeSection = GetCachedEpisodeSection();
        if (episodeSection == null)
        {
            return default;
        }

        // ✅ 优化：直接遍历 Episodes 找到当前剧集的索引
        int currentIndex = -1;
        for (int i = 0; i < episodeSection.Episodes.Count; i++)
        {
            if (episodeSection.Episodes[i].Data == _episode)
            {
                currentIndex = i;
                break;  // 找到立即停止
            }
        }

        if (currentIndex >= 0 && currentIndex < episodeSection.Episodes.Count - 1)
        {
            return episodeSection.Episodes[currentIndex + 1].Data;
        }

        return default;
    }

    /// <summary>
    /// 查找上一个剧集（性能优化版：同上）.
    /// </summary>
    private EpisodeInformation? FindPreviousEpisode()
    {
        if (Sections is null)
        {
            return default;
        }

        // ✅ 使用缓存的 Episode Section
        var episodeSection = GetCachedEpisodeSection();
        if (episodeSection == null)
        {
            return default;
        }

        // ✅ 优化：直接遍历 Episodes 找到当前剧集的索引
        int currentIndex = -1;
        for (int i = 0; i < episodeSection.Episodes.Count; i++)
        {
            if (episodeSection.Episodes[i].Data == _episode)
            {
                currentIndex = i;
                break;  // 找到立即停止
            }
        }

        if (currentIndex > 0)
        {
            return episodeSection.Episodes[currentIndex - 1].Data;
        }

        return default;
    }

    private void ChangeEpisode(EpisodeInformation episode)
    {
        if (episode.Identifier.Id == _episode.Identifier.Id)
        {
            return;
        }

        NewMediaRequest?.Invoke(this, new MediaSnapshot(default, episode));
    }

    private void ChangeSeason(SeasonItemViewModel season)
    {
        if (season.Data.Identifier.Id == _view.Information.Identifier.Id)
        {
            return;
        }

        NewMediaRequest?.Invoke(this, new MediaSnapshot(season.Data, default));
    }

    [RelayCommand]
    private void SelectSection(IPlayerSectionDetailViewModel section)
    {
        if (section is null || section == SelectedSection)
        {
            return;
        }

        SelectedSection = section;
        SelectedSection.TryFirstLoadCommand.Execute(default);
    }
}
