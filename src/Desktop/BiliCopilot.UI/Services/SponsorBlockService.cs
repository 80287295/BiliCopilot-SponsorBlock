// Copyright (c) Bili Copilot. All rights reserved.

using System.Net.Http.Json;
using BiliCopilot.UI.Models.SponsorBlock;
using BiliCopilot.UI.Services.Interfaces;

namespace BiliCopilot.UI.Services;

/// <summary>
/// SponsorBlock 服务实现.
/// </summary>
public sealed class SponsorBlockService : ISponsorBlockService
{
    private const string BASE_URL = "https://www.bsbsb.top";
    private const int MaxCacheSize = 200;
    private static readonly HttpClient _httpClient = new();
    private readonly Dictionary<string, List<SponsorSegment>> _cache = new();
    private readonly LinkedList<string> _cacheOrder = new(); // 跟踪插入顺序，用于 FIFO 淘汰
    private SponsorBlockConfig _config = new();

    /// <inheritdoc/>
    public SponsorBlockConfig Config
    {
        get => _config;
        set => _config = value ?? new SponsorBlockConfig();
    }

    /// <inheritdoc/>
    public bool IsEnabled => _config.Enabled;

    /// <inheritdoc/>
    public async Task<List<SponsorSegment>> GetSegmentsAsync(string videoID)
    {
        try
        {
            // 检查缓存
            if (_cache.TryGetValue(videoID, out var cached))
            {
                return cached;
            }

            // 调用 BilibiliSponsorBlock API
            var encodedVideoID = Uri.EscapeDataString(videoID);
            var url = $"{BASE_URL}/api/skipSegments?videoID={encodedVideoID}";
            var responses = await _httpClient.GetFromJsonAsync<List<SponsorVideoResponse>>(url);

            if (responses == null || responses.Count == 0)
            {
                return new List<SponsorSegment>();
            }

            // 合并所有片段
            var segments = new List<SponsorSegment>();
            foreach (var response in responses)
            {
                if (response.Segments != null)
                {
                    segments.AddRange(response.Segments);
                }
            }

            // 按开始时间排序
            segments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            // 写入缓存（带大小限制）
            EvictCacheIfNeeded();
            _cache[videoID] = segments;
            _cacheOrder.AddLast(videoID);

            return segments;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SponsorBlock API error: {ex.Message}");
            return new List<SponsorSegment>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SubmitSegmentAsync(SponsorSegment segment, string userID)
    {
        try
        {
            var url = $"{BASE_URL}/api/skipSegments";
            var content = JsonContent.Create(new
            {
                videoID = segment.VideoID,
                startTime = segment.StartTime,
                endTime = segment.EndTime,
                category = segment.Category.ToString(),
                userID = userID,
                description = segment.Description ?? string.Empty,
            });

            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SponsorBlock submit error: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> VoteSegmentAsync(string uuid, int vote)
    {
        try
        {
            var url = $"{BASE_URL}/api/voteOnSponsorTime";
            var content = JsonContent.Create(new
            {
                UUID = uuid,
                vote = vote,
            });

            var response = await _httpClient.PostAsync(url, content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SponsorBlock vote error: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<SponsorSegment?> CheckShouldSkipAsync(string videoID, double currentTime)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var segments = await GetSegmentsAsync(videoID);
        foreach (var segment in segments)
        {
            // 检查当前时间是否在片段范围内（允许 0.5 秒误差）
            if (currentTime >= segment.StartTime - 0.5 && currentTime < segment.EndTime)
            {
                // 检查该类别的跳过选项
                if (_config.CategoryOptions.TryGetValue(segment.Category, out var option))
                {
                    if (option == CategorySkipOption.Disabled || option == CategorySkipOption.ShowOverlay)
                    {
                        continue; // 不自动跳过
                    }
                }

                return segment;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        _cache.Clear();
        _cacheOrder.Clear();
    }

    /// <summary>
    /// 当缓存超过最大容量时，淘汰最早插入的条目（FIFO）.
    /// </summary>
    private void EvictCacheIfNeeded()
    {
        while (_cache.Count >= MaxCacheSize && _cacheOrder.First != null)
        {
            var oldestKey = _cacheOrder.First.Value;
            _cacheOrder.RemoveFirst();
            _cache.Remove(oldestKey);
        }
    }
}
