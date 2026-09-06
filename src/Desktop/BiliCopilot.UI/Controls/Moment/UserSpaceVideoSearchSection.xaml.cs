// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;

namespace BiliCopilot.UI.Controls.Moment;

/// <summary>
/// 用户空间视频搜索区域.
/// </summary>
public sealed partial class UserSpaceVideoSearchSection : UserSpacePageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserSpaceVideoSearchSection"/> class.
    /// </summary>
    public UserSpaceVideoSearchSection() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        ViewModel.SearchUpdated += OnSearchUpdatedAsync;
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        ViewModel.SearchUpdated -= OnSearchUpdatedAsync;
    }

    private void OnSearchUpdatedAsync(object? sender, EventArgs e)
        => View.DelayCheckItemsAsync().SafeFireAndForget();
}
