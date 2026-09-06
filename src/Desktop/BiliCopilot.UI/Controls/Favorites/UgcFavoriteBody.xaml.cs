// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;

using BiliCopilot.UI.Extensions;

namespace BiliCopilot.UI.Controls.Favorites;

/// <summary>
/// UGC收藏主体.
/// </summary>
public sealed partial class UgcFavoriteBody : UgcFavoriteControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UgcFavoriteBody"/> class.
    /// </summary>
    public UgcFavoriteBody() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.ListUpdated -= OnVideoListUpdatedAsync;
        }
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(UgcSeasonFavoriteSectionDetailViewModel? oldValue, UgcSeasonFavoriteSectionDetailViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.ListUpdated -= OnVideoListUpdatedAsync;
        }

        if (newValue is null)
        {
            return;
        }

        newValue.ListUpdated += OnVideoListUpdatedAsync;
    }

    private void OnVideoListUpdatedAsync(object? sender, EventArgs e)
        => View.DelayCheckItemsAsync().SafeFireAndForget();
}
