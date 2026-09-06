// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Richasy.WinUIKernel.Share.Base;

using BiliCopilot.UI.Extensions;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 视频动态详情控件.
/// </summary>
public sealed partial class VideoMomentSectionDetailControl : VideoMomentSectionDetailControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VideoMomentSectionDetailControl"/> class.
    /// </summary>
    public VideoMomentSectionDetailControl() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        ViewModel.ListUpdated += OnListUpdatedAsync;
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        ViewModel.ListUpdated -= OnListUpdatedAsync;
    }

    private void OnListUpdatedAsync(object? sender, EventArgs e)
        => View.DelayCheckItemsAsync().SafeFireAndForget();
}

/// <summary>
/// 视频动态详情控件基类.
/// </summary>
public abstract class VideoMomentSectionDetailControlBase : LayoutUserControlBase<VideoMomentSectionDetailViewModel>
{
}
