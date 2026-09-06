// Copyright (c) Bili Copilot. All rights reserved.

namespace BiliCopilot.UI.Extensions;

/// <summary>
/// Task 扩展方法.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// 安全地触发并遗忘一个后台任务：捕获所有异常，避免 async void 场景下未处理异常导致进程崩溃.
    /// </summary>
    /// <param name="task">后台任务.</param>
    /// <param name="onError">可选的异常回调（在异常发生时调用，回调自身的异常也会被捕获）.</param>
    public static async void SafeFireAndForget(this Task task, Action<Exception>? onError = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SafeFireAndForget 捕获未处理异常: {ex.GetType().Name}: {ex.Message}");
            try
            {
                onError?.Invoke(ex);
            }
            catch (Exception callbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"SafeFireAndForget onError 回调自身抛出异常: {callbackEx.GetType().Name}: {callbackEx.Message}");
            }
        }
    }
}
