// Copyright (c) Bili Copilot. All rights reserved.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Authorization;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 启动页视图模型.
/// </summary>
public sealed partial class StartupPageViewModel
{
    private readonly ILogger<StartupPageViewModel> _logger;
    private readonly IAuthenticationService _authenticationService;
    private readonly DispatcherQueue _dispatcherQueue;

    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _version;

    // Note: ErrorTip is manually implemented in StartupPageViewModel.cs with custom SetProperty logic + logging
    // Do NOT add [ObservableProperty] here - it would cause CS0102 duplicate definition

    [ObservableProperty]
    private bool _isQRCodeLoading;

    /// <summary>
    /// 二维码图片数据就绪，由视图层订阅并渲染（MVVM 分层：ViewModel 不持有 XAML 控件）.
    /// </summary>
    public event EventHandler<byte[]>? QRCodeImageReady;
}
