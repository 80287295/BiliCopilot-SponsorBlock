// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;

using BiliCopilot.UI.Extensions;

namespace BiliCopilot.UI.Controls.Pgc;

/// <summary>
/// 娱乐索引主体.
/// </summary>
public sealed partial class EntertainmentIndexMainControl : EntertainmentIndexControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntertainmentIndexMainControl"/> class.
    /// </summary>
    public EntertainmentIndexMainControl() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnControlUnloaded()
    {
        if (ViewModel is not null)
        {
            ViewModel.ItemsUpdated -= OnItemsUpdatedAsync;
        }
    }

    /// <inheritdoc/>
    protected override void OnViewModelChanged(EntertainmentIndexViewModel? oldValue, EntertainmentIndexViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.ItemsUpdated -= OnItemsUpdatedAsync;
        }

        if (newValue is null)
        {
            return;
        }

        View?.ResetScrollPosition();
        newValue.ItemsUpdated += OnItemsUpdatedAsync;
    }

    private void OnItemsUpdatedAsync(object? sender, EventArgs e)
        => View.DelayCheckItemsAsync().SafeFireAndForget();
}
