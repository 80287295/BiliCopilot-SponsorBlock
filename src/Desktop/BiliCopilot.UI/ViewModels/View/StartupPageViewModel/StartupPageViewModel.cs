// Copyright (c) Bili Copilot. All rights reserved.

using BiliCopilot.UI.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.BiliKernel.Bili.Authorization;
using Richasy.WinUIKernel.Share.Toolkits;
using Richasy.WinUIKernel.Share.ViewModels;

namespace BiliCopilot.UI.ViewModels.View;

/// <summary>
/// 启动页视图模型.
/// </summary>
public sealed partial class StartupPageViewModel : ViewModelBase
{
    private string? _errorTip;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupPageViewModel"/> class.
    /// </summary>
    public StartupPageViewModel(
        IAuthenticationService authenticationService,
        ILogger<StartupPageViewModel> logger,
        DispatcherQueue dispatcherQueue)
    {
        _authenticationService = authenticationService;
        _logger = logger;
        _dispatcherQueue = dispatcherQueue;
        _logger.LogInformation("StartupPageViewModel 已创建，调试模式已启用");
    }

    /// <summary>
    /// 是否有错误（用于调试绑定）.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(ErrorTip);

    /// <summary>
    /// 调试信息字符串.
    /// </summary>
    public string DebugInfo => $"[DEBUG] Loading={IsQRCodeLoading}, Error={ErrorTip ?? "null"}, HasError={HasError}";

    /// <summary>
    /// 错误提示信息.
    /// </summary>
    public string? ErrorTip
    {
        get => _errorTip;
        set
        {
            if (SetProperty(ref _errorTip, value))
            {
                _logger.LogInformation("ErrorTip 变更: {OldValue} → {NewValue}, HasError={HasError}",
                    _errorTip ?? "null", value ?? "null", HasError);
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(DebugInfo));
            }
        }
    }

    /// <summary>
    /// 初始化视图模型.
    /// </summary>
    /// <returns><see cref="Task"/>.</returns>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        Version = this.Get<IAppToolkit>().GetPackageVersion();
        await ReloadQRCodeAsync();
    }

    [RelayCommand]
    private async Task ReloadQRCodeAsync()
    {
        _logger.LogInformation("=== ReloadQRCodeAsync 开始执行 ===");

        if (_cancellationTokenSource is not null)
        {
            _logger.LogInformation("取消之前的二维码加载任务");
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        try
        {
            _logger.LogInformation("设置 IsQRCodeLoading = true（应该显示 Shimmer 骨架屏）");
            IsQRCodeLoading = true;

            OnPropertyChanged(nameof(DebugInfo));

            _logger.LogInformation("调用 SignInAsync (cancellationToken: {HasToken})",
                _cancellationTokenSource.Token != default ? "有" : "无");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await _authenticationService.SignInAsync(cancellationToken: _cancellationTokenSource.Token);
            stopwatch.Stop();

            _logger.LogInformation("SignInAsync 完成，耗时 {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

            _logger.LogInformation("检查授权状态...");
            var isSignedIn = await CheckAuthorizeStatusAsync();

            if (isSignedIn)
            {
                _logger.LogInformation("登录成功，执行退出命令");
                ExitCommand.Execute(true);
            }
            else
            {
                _logger.LogWarning("未能成功获取授权信息，扫码可能出现了异常. DebugInfo={DebugInfo}", DebugInfo);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "二维码加载被用户取消");
            IsQRCodeLoading = false;
            ErrorTip = "操作已取消";
        }
        catch (System.Net.Http.HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "网络请求失败！StatusCode={StatusCode}, Message={Message}",
                httpEx.StatusCode?.ToString() ?? "N/A", httpEx.Message);
            IsQRCodeLoading = false;
            ErrorTip = $"网络错误 ({httpEx.StatusCode}): {httpEx.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登录过程中出现异常！类型={TypeName}, Message={Message}",
                ex.GetType().Name, ex.Message);
            IsQRCodeLoading = false;
            ErrorTip = $"错误 ({ex.GetType().Name}): {ex.Message}";
        }

        _logger.LogInformation("=== ReloadQRCodeAsync 执行完毕，最终状态: {DebugInfo} ===", DebugInfo);
    }

    [RelayCommand]
    private async Task ExitAsync(bool shouldRestart)
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        if (shouldRestart)
        {
            this.Get<AppViewModel>().RestartCommand.Execute(default);
        }
    }

    [RelayCommand]
    private void RenderQRCode(byte[] imageData)
    {
        _logger.LogInformation("RenderQRCode 被调用，数据长度={Length} bytes", imageData?.Length ?? 0);

        if (imageData is null || imageData.Length == 0)
        {
            _logger.LogError("二维码图片数据为空，忽略渲染请求");
            return;
        }

        // MVVM 分层：ViewModel 不构造 XAML 图像对象，改为通知视图层自行解码渲染.
        var enqueued = _dispatcherQueue.TryEnqueue(() =>
        {
            _logger.LogInformation("通知视图层渲染二维码图片，数据长度={Length} bytes", imageData.Length);
            IsQRCodeLoading = false;
            QRCodeImageReady?.Invoke(this, imageData);
        });

        if (!enqueued)
        {
            _logger.LogError("❌ DispatcherQueue.TryEnqueue 返回 false！调度失败！");
        }
        else
        {
            _logger.LogInformation("✅ DispatcherQueue.TryEnqueue 成功");
        }
    }

    private async Task<bool> CheckAuthorizeStatusAsync()
    {
        var tokenResolver = this.Get<IAuthenticationService>();
        try
        {
            await tokenResolver.EnsureTokenAsync(_cancellationTokenSource?.Token ?? default).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "检查授权状态时出现异常.");
        }

        return false;
    }
}
