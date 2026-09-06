// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Components;
using Richasy.WinUIKernel.Share.Base;

using BiliCopilot.UI.Extensions;

namespace BiliCopilot.UI.Controls.Search;

/// <summary>
/// PGC搜索分区详情控件.
/// </summary>
public sealed partial class PgcSectionDetailControl : PgcSectionDetailControlBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgcSectionDetailControl"/> class.
    /// </summary>
    public PgcSectionDetailControl() => InitializeComponent();

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
/// PGC搜索分区详情控件基类.
/// </summary>
public abstract class PgcSectionDetailControlBase : LayoutUserControlBase<PgcSearchSectionDetailViewModel>
{
}
