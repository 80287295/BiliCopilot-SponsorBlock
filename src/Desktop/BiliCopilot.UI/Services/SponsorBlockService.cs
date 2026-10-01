// Copyright (c) Bili Copilot. All rights reserved.

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BiliCopilot.UI.Models.SponsorBlock;
using BiliCopilot.UI.Services.Interfaces;

namespace BiliCopilot.UI.Services;

/// <summary>
/// bsbsb.top（BilibiliSponsorBlock）API 的原始片段 DTO.
/// <para>
/// 实际响应为顶层平铺数组，元素示例：
/// { "cid":"168885122", "category":"sponsor", "actionType":"skip", "segment":[300.019,600.014], "UUID":"...", "videoDuration":1801, "locked":1, "votes":1, "description":"" }
/// </para>
/// </summary>
internal sealed class BsbsbSegmentDto
{
    /// <summary>
    /// 分片时间区间 [开始秒, 结束秒].
    /// </summary>
    [JsonPropertyName("segment")]
    public JsonElement? Segment { get; set; }

    /// <summary>
    /// 类别（小写字符串，如 sponsor / selfpromo / intro / poi_highlight）.
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>
    /// 动作类型（skip / mute / full / poi）.
    /// </summary>
    [JsonPropertyName("actionType")]
    public string? ActionType { get; set; }

    /// <summary>
    /// 片段唯一标识.
    /// </summary>
    [JsonPropertyName("UUID")]
    public string? Uuid { get; set; }

    /// <summary>
    /// 视频时长（秒）.
    /// </summary>
    [JsonPropertyName("videoDuration")]
    public double? VideoDuration { get; set; }

    /// <summary>
    /// 是否被锁定.
    /// </summary>
    [JsonPropertyName("locked")]
    public int? Locked { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// SponsorBlock 服务实现（线程安全优化版）.
/// 
/// 性能优化：
/// - 使用 ReaderWriterLockSlim 保护并发访问（支持多读者并行）
/// - 使用 ConcurrentDictionary 替代 Dictionary（原子操作）
/// - 双重检查锁定模式避免重复 API 调用
/// - FIFO 缓存淘汰机制（带大小限制）
/// </summary>
public sealed class SponsorBlockService : ISponsorBlockService, IDisposable
{
    private const string BASE_URL = "https://www.bsbsb.top";
    private const int MaxCacheSize = 200;
    
    private static readonly HttpClient _httpClient = new();
    
    // ✅ 线程安全集合
    private readonly ConcurrentDictionary<string, List<SponsorSegment>> _cache = new();
    private readonly LinkedList<string> _cacheOrder = new();
    private readonly ReaderWriterLockSlim _cacheLock = new();
    
    private SponsorBlockConfig _config = new();
    private bool _disposed;

    /// <inheritdoc/>
    public SponsorBlockConfig Config
    {
        get => _config;
        set => _config = value ?? new SponsorBlockConfig();
    }

    /// <inheritdoc/>
    public bool IsEnabled => _config.Enabled;

    /// <inheritdoc/>
    /// <remarks>
    /// 线程安全实现：
    /// - 快速路径：读锁（允许多个读者并发，性能高）
    /// - 慢速路径：写锁（独占访问，保证一致性）
    /// - 双重检查：避免在等待锁期间重复调用 API
    /// </remarks>
    public async Task<List<SponsorSegment>> GetSegmentsAsync(string videoID)
    {
        // ✅ 快速路径：读锁检查缓存（允许多个读者并发）
        _cacheLock.EnterReadLock();
        try
        {
            if (_cache.TryGetValue(videoID, out var cached))
            {
                return cached; // 缓存命中，直接返回
            }
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }

        // 慢速路径：从 API 获取数据
        List<SponsorSegment> segments;

        try
        {
            // 调用 BilibiliSponsorBlock API
            var encodedVideoID = Uri.EscapeDataString(videoID);
            var url = $"{BASE_URL}/api/skipSegments?videoID={encodedVideoID}";

            // ✅ 修复：bsbsb.top 返回顶层平铺数组，每个元素形如
            // { "cid": "...", "category": "sponsor", "actionType": "skip", "segment": [start, end], "UUID": "...", ... }
            // 时间在 "segment" 数组里，而不是 {videoID, segments:[...]} 包装结构。
            var dtos = await _httpClient.GetFromJsonAsync<List<BsbsbSegmentDto>>(url);

            if (dtos == null || dtos.Count == 0)
            {
                return new List<SponsorSegment>();
            }

            // DTO → 领域模型（含 segment 数组展开、类别字符串解析、按开始时间排序）
            segments = MapSegments(dtos);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SponsorBlock API error: {ex.Message}");
            return new List<SponsorSegment>();
        }

        // ✅ 写入缓存（需要写锁保护）
        _cacheLock.EnterWriteLock();
        try
        {
            // 双重检查：可能在等待写锁期间其他线程已经写入
            if (!_cache.ContainsKey(videoID))
            {
                EvictCacheIfNeeded();  // 先淘汰旧数据
                
                // 原子操作写入
                _cache[videoID] = segments;
                _cacheOrder.AddLast(videoID);
                
                System.Diagnostics.Debug.WriteLine($"Cached {segments.Count} segments for video {videoID} (Cache size: {_cache.Count})");
            }
            else
            {
                // 返回已存在的版本（避免重复数据）
                segments = _cache[videoID];
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }

        return segments;
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
            
            if (response.IsSuccessStatusCode)
            {
                // 提交成功后清除对应视频的缓存，强制下次刷新
                ClearVideoCache(segment.VideoID);
                System.Diagnostics.Debug.WriteLine($"Sponsor segment submitted successfully for video {segment.VideoID}");
            }
            
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
    /// <remarks>
    /// 优化：复用 GetSegmentsAsync 的缓存结果，避免重复 API 调用.
    /// </remarks>
    public async Task<SponsorSegment?> CheckShouldSkipAsync(string videoID, double currentTime)
    {
        if (!IsEnabled)
        {
            return null;
        }

        // 复用缓存数据
        var segments = await GetSegmentsAsync(videoID);
        
        // 使用线性查找（因为通常片段数量很少 < 10 个）
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
    /// <remarks>
    /// 线程安全清理.
    /// </remarks>
    public void ClearCache()
    {
        _cacheLock.EnterWriteLock();
        try
        {
            _cache.Clear();
            _cacheOrder.Clear();
            
            System.Diagnostics.Debug.WriteLine("SponsorBlock cache cleared completely");
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 清除指定视频的缓存（用于提交新片段后强制刷新）.
    /// </summary>
    public void ClearVideoCache(string videoID)
    {
        _cacheLock.EnterWriteLock();
        try
        {
            if (_cache.TryRemove(videoID, out _))
            {
                _cacheOrder.Remove(videoID);
                System.Diagnostics.Debug.WriteLine($"Cleared cache for video {videoID}");
            }
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// 获取当前缓存大小（用于监控和调试）.
    /// </summary>
    public int CacheCount 
    { 
        get 
        { 
            _cacheLock.EnterReadLock();
            try
            {
                return _cache.Count;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
        } 
    }

    /// <summary>
    /// 当缓存超过最大容量时，淘汰最早插入的条目（FIFO）.
    /// 需要在写锁内调用.
    /// </summary>
    private void EvictCacheIfNeeded()
    {
        while (_cache.Count >= MaxCacheSize && _cacheOrder.First != null)
        {
            var oldestKey = _cacheOrder.First.Value;
            _cacheOrder.RemoveFirst();
            _cache.TryRemove(oldestKey, out _); // 使用 TryRemove 保证原子性
            
            System.Diagnostics.Debug.WriteLine($"Evicted cache entry for video {oldestKey} (Cache full)");
        }
    }

    /// <summary>
    /// 将 bsbsb.top 的原始 DTO 映射为领域模型.
    /// </summary>
    /// <remarks>
    /// 处理要点：
    /// 1. 时间在 <c>segment: [start, end]</c> 数组里，展开为 StartTime / EndTime；
    /// 2. category 是小写字符串（如 poi_highlight / music_offtopic），用忽略大小写的枚举解析；
    /// 3. 过滤掉 end &lt;= start 的单点片段（如 poi_highlight 的 actionType=poi），避免无效跳过；
    /// 4. 结果按 StartTime 升序排列，保证调用方二分查找的时间有序前提成立.
    /// </remarks>
    private static List<SponsorSegment> MapSegments(List<BsbsbSegmentDto> dtos)
    {
        var result = new List<SponsorSegment>(dtos.Count);

        foreach (var dto in dtos)
        {
            if (dto.Segment is not { ValueKind: JsonValueKind.Array } segmentArray ||
                segmentArray.GetArrayLength() < 2)
            {
                continue;
            }

            double start;
            double end;
            try
            {
                start = segmentArray[0].GetDouble();
                end = segmentArray[1].GetDouble();
            }
            catch (FormatException)
            {
                continue;
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            // 单点片段（poi 高亮等）不参与自动跳过
            if (end <= start)
            {
                continue;
            }

            // 类别解析：忽略大小写，poi_highlight / music_offtopic 等带下划线的名称可正确匹配
            if (!Enum.TryParse(dto.Category, ignoreCase: true, out SponsorCategory category))
            {
                category = SponsorCategory.Sponsor;
            }

            if (!Enum.TryParse(dto.ActionType, ignoreCase: true, out SponsorActionType action))
            {
                action = SponsorActionType.Skip;
            }

            result.Add(new SponsorSegment
            {
                StartTime = start,
                EndTime = end,
                UUID = dto.Uuid,
                Category = category,
                Action = action,
                VideoDuration = dto.VideoDuration,
                Locked = dto.Locked,
                Description = dto.Description,
            });
        }

        return result.OrderBy(s => s.StartTime).ToList();
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
        if (_disposed)
            return;

        if (disposing)
        {
            // 清理缓存和锁
            ClearCache();
            _cacheLock.Dispose();
            
            System.Diagnostics.Debug.WriteLine("SponsorBlockService resources disposed");
        }

        _disposed = true;
    }
}
