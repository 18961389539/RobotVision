using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Controls
{
    internal sealed class ImageViewerBackgroundOperationObserver : IDisposable
    {
        private readonly Action<string, Exception> _logError;
        // 修复：观察器持有取消令牌，控件 Dispose 后取消在途观察，避免继续等待/访问已释放状态。
        private readonly CancellationTokenSource _cancellation = new();
        private int _disposed;

        public ImageViewerBackgroundOperationObserver(Action<string, Exception> logError)
        {
            _logError = logError ?? throw new ArgumentNullException(nameof(logError));
        }

        public async Task ObserveAsync(Task operation, string operationName)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

            try
            {
                // 修复：观察等待与取消令牌关联，Dispose 后立即中断等待；
                // 原先 fire-and-forget 在控件销毁后仍会继续访问已释放的 UI 状态。
                await operation.WaitAsync(_cancellation.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logError(operationName, ex);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            // 修复：取消并释放令牌，使所有在途 ObserveAsync 等待立即结束。
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
