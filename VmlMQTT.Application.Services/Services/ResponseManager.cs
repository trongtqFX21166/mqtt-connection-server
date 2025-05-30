using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;

namespace VmlMQTT.Application.Services
{
    public class ResponseManager : IResponseManager, IHostedService
    {
        private readonly ConcurrentDictionary<string, PendingCommand> _pendingCommands = new();
        private readonly Timer _cleanupTimer;
        private readonly ILogger<ResponseManager> _logger;

        public ResponseManager(ILogger<ResponseManager> logger)
        {
            _logger = logger;
            _cleanupTimer = new Timer(async _ => await CleanupExpiredResponsesAsync(),
                null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public async Task<CommandResponse> WaitForResponseAsync(string requestId, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<CommandResponse>();
            var pendingCommand = new PendingCommand
            {
                RequestId = requestId,
                TaskCompletionSource = tcs,
                ExpiresAt = DateTime.UtcNow.Add(timeout)
            };

            _pendingCommands[requestId] = pendingCommand;

            try
            {
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                combinedCts.CancelAfter(timeout);

                return await tcs.Task.WaitAsync(combinedCts.Token);
            }
            catch (OperationCanceledException)
            {
                return new CommandResponse
                {
                    RequestId = requestId,
                    Code = 408,
                    Message = "Command timeout",
                    OfflineFlag = true
                };
            }
            finally
            {
                _pendingCommands.TryRemove(requestId, out _);
            }
        }

        public void RegisterResponse(CommandResponse response)
        {
            if (_pendingCommands.TryRemove(response.RequestId, out var pendingCommand))
            {
                pendingCommand.TaskCompletionSource.SetResult(response);
                _logger.LogDebug("Response registered for request {RequestId}", response.RequestId);
            }
            else
            {
                _logger.LogWarning("Received response for unknown request {RequestId}", response.RequestId);
            }
        }

        public async Task CleanupExpiredResponsesAsync()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _pendingCommands
                .Where(kvp => kvp.Value.ExpiresAt < now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                if (_pendingCommands.TryRemove(key, out var pendingCommand))
                {
                    pendingCommand.TaskCompletionSource.TrySetResult(new CommandResponse
                    {
                        RequestId = key,
                        Code = 408,
                        Message = "Command expired",
                        OfflineFlag = true
                    });
                }
            }

            if (expiredKeys.Any())
            {
                _logger.LogDebug("Cleaned up {Count} expired pending commands", expiredKeys.Count);
            }

            await Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cleanupTimer?.Dispose();

            // Complete all pending commands
            foreach (var kvp in _pendingCommands)
            {
                kvp.Value.TaskCompletionSource.TrySetCanceled();
            }

            return Task.CompletedTask;
        }

        private class PendingCommand
        {
            public string RequestId { get; set; }
            public TaskCompletionSource<CommandResponse> TaskCompletionSource { get; set; }
            public DateTime ExpiresAt { get; set; }
        }
    }
}
