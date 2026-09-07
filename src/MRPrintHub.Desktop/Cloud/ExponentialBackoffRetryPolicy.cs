using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace MRPrintHub.Desktop.Cloud;

/// <summary>
/// Custom SignalR reconnect policy with exponential backoff and jitter.
/// Delays: 2s, 4s, 8s, 16s, 32s, max 60s.
/// </summary>
public class ExponentialBackoffRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
        TimeSpan.FromSeconds(32),
        TimeSpan.FromSeconds(60)
    ];

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // Exponential backoff capped at 60 seconds
        if (retryContext.PreviousRetryCount < BackoffDelays.Length)
        {
            return BackoffDelays[retryContext.PreviousRetryCount];
        }

        return TimeSpan.FromSeconds(60);
    }
}
