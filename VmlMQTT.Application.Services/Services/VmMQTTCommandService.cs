using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Application.Models;

namespace VmlMQTT.Application.Services
{
    public class CommandService : ICommandService
    {
        private readonly ICommandValidator _validator;
        private readonly IMqttConnectionPool _connectionPool;
        private readonly IResponseManager _responseManager;
        private readonly IUserSessionService _sessionService;
        private readonly ILogger<CommandService> _logger;
        private readonly IConfiguration _configuration;

        public CommandService(
            ICommandValidator validator,
            IMqttConnectionPool connectionPool,
            IResponseManager responseManager,
            IUserSessionService sessionService,
            ILogger<CommandService> logger,
            IConfiguration configuration)
        {
            _validator = validator;
            _connectionPool = connectionPool;
            _responseManager = responseManager;
            _sessionService = sessionService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<CommandResult> SendCommandAsync(SendCommandRequest request, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // 1. Validate request
                var validationResult = await _validator.ValidateAsync(request);
                if (!validationResult.IsValid)
                {
                    return new CommandResult
                    {
                        Success = false,
                        Code = 400,
                        Message = string.Join("; ", validationResult.Errors),
                        RequestId = request.RequestId,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                // 2. Check permissions
                if (!await _sessionService.HasCommandPermissionAsync(request.Phone, request.DeviceId, request.Command))
                {
                    return new CommandResult
                    {
                        Success = false,
                        Code = 403,
                        Message = "Insufficient permissions to execute this command",
                        RequestId = request.RequestId,
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                // 3. Get session info
                var sessionInfo = await _sessionService.GetSessionAsync(request.Phone, request.DeviceId);

                // 4. Get MQTT connection
                var connectionConfig = new MqttConnectionConfig
                {
                    Host = sessionInfo.Host,
                    Port = sessionInfo.Port,
                    Username = sessionInfo.Username,
                    Password = sessionInfo.Password
                };

                var client = await _connectionPool.GetConnectionAsync(sessionInfo.Host, connectionConfig);

                // 5. Build command message
                var commandMessage = BuildCommandMessage(request, sessionInfo);
                var topic = GetCommandTopic(sessionInfo, request);

                // 6. Send command
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(commandMessage)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .WithRetainFlag(false)
                    .Build();

                await client.PublishAsync(message, cancellationToken);

                _logger.LogInformation("Command sent to device {DeviceId} via topic {Topic}",
                    request.DeviceId, topic);

                // 7. Wait for response
                var timeout = TimeSpan.FromSeconds(request.TimeoutSeconds);
                var response = await _responseManager.WaitForResponseAsync(request.RequestId, timeout, cancellationToken);

                return new CommandResult
                {
                    Success = response.Code == 200,
                    Code = response.Code,
                    Message = response.Message,
                    Data = response.Data,
                    RequestId = request.RequestId,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (OperationCanceledException)
            {
                return new CommandResult
                {
                    Success = false,
                    Code = 408,
                    Message = "Command was cancelled",
                    RequestId = request.RequestId,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation while sending command");
                return new CommandResult
                {
                    Success = false,
                    Code = 404,
                    Message = ex.Message,
                    RequestId = request.RequestId,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending command to device {DeviceId}", request.DeviceId);
                return new CommandResult
                {
                    Success = false,
                    Code = 500,
                    Message = "Internal server error",
                    RequestId = request.RequestId,
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }

        private string BuildCommandMessage(SendCommandRequest request, UserSessionInfo sessionInfo)
        {
            var commandPayload = new
            {
                request_id = request.RequestId,
                session_id = sessionInfo.SessionId,
                command = request.Command,
                parameters = request.Parameters,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                timeout_seconds = request.TimeoutSeconds
            };

            return JsonConvert.SerializeObject(commandPayload);
        }

        private string GetCommandTopic(UserSessionInfo sessionInfo, SendCommandRequest request)
        {
            string cmdTopic = string.Empty;
            // Use the first publish topic or construct one
            if (sessionInfo.SubscribeTopics.Any())
            {
                cmdTopic = sessionInfo.SubscribeTopics?.FirstOrDefault(x => x.StartsWith("vml_command_client_request")) ?? string.Empty;
            }

            // Fallback topic construction
            return !string.IsNullOrWhiteSpace(cmdTopic) ? cmdTopic : $"vml_command_client_request/{sessionInfo.SessionId}";
        }
    }
}
