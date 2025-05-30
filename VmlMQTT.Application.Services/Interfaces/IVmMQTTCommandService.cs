using MQTTnet.Client;
using VmlMQTT.Application.Models;
using VmlMQTT.Application.Services;

namespace VmlMQTT.Application.Interfaces
{
    public interface ICommandService
    {
        Task<CommandResult> SendCommandAsync(SendCommandRequest request, CancellationToken cancellationToken = default);
    }

    public interface ICommandValidator
    {
        Task<ValidationResult> ValidateAsync(SendCommandRequest request);
    }

    public interface IMqttConnectionPool : IDisposable
    {
        Task<IMqttClient> GetConnectionAsync(string host, MqttConnectionConfig config);
        Task ReleaseConnectionAsync(string host);
        Task<bool> IsConnectedAsync(string host);
    }

    public interface IResponseManager
    {
        Task<CommandResponse> WaitForResponseAsync(string requestId, TimeSpan timeout, CancellationToken cancellationToken = default);
        void RegisterResponse(CommandResponse response);
        Task CleanupExpiredResponsesAsync();
    }

    public interface IUserSessionService
    {
        Task<UserSessionInfo> GetSessionAsync(string phone, string deviceId);
        Task<bool> HasCommandPermissionAsync(string phone, string deviceId, string command);
    }
}
