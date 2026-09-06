// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;

namespace BiliCopilot.UI.Controls.LivePartition;

/// <summary>
/// 直播推荐主体.
/// </summary>
public sealed partial class LiveRecommendMainBody : LivePartitionPageControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveRecommendMainBody"/> class.
    /// </summary>
    public LiveRecommendMainBody() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlLoaded()
    {
        ViewModel.RecommendUpdated += OnLiveListUpdatedAsync;
    }

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        ViewModel.RecommendUpdated -= OnLiveListUpdatedAsync;
    }

    private void OnLiveListUpdatedAsync(object? sender, EventArgs e)
        => View.DelayCheckItemsAsync().SafeFireAndForget();
}
