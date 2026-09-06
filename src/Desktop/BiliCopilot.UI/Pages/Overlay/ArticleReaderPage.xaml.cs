// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.Extensions;
using BiliCopilot.UI.ViewModels.View;
using Richasy.BiliKernel.Models.Article;
using Richasy.WinUIKernel.Share.Base;

namespace BiliCopilot.UI.Pages.Overlay;

/// <summary>
/// 文章阅读页面.
/// </summary>
public sealed partial class ArticleReaderPage : ArticleReaderPageBase, IParameterPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleReaderPage"/> class.
    /// </summary>
    public ArticleReaderPage() => InitializeComponent();

    public void SetParameter(object? parameter)
    {
        if (parameter is ArticleIdentifier article)
        {
            ViewModel.InitializeCommand.Execute(article);
            Reader.Initialized += OnInitializedAsync;
            ViewModel.ArticleInitialized += OnInitializedAsync;
        }
    }

    protected override void OnPageUnloaded()
    {
        Reader.ClearContent();
        Reader.Initialized -= OnInitializedAsync;
        ViewModel.ArticleInitialized -= OnInitializedAsync;
    }

    private void OnInitializedAsync(object? sender, EventArgs e)
    {
        if (ViewModel.Content is not null && Reader.IsInitialized)
        {
            Reader.LoadContentAsync(ViewModel.Content).SafeFireAndForget();
        }
    }
}

/// <summary>
/// 文章阅读页面基类.
/// </summary>
public abstract class ArticleReaderPageBase : LayoutPageBase<ArticleReaderPageViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticleReaderPageBase"/> class.
    /// </summary>
    protected ArticleReaderPageBase() => ViewModel = this.Get<ArticleReaderPageViewModel>();
}
