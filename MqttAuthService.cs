using Microsoft.Extensions.Logging;
using VmlMQTT.Application.DTOs;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Core.Models;

namespace VmlMQTT.Application.Services
{
    public class MqttAuthService : IMqttAuthService
    {
        private readonly ILogger<MqttAuthService> _logger;
        private readonly IUserSessionRepository _userSessionRepository;
        private readonly IEmqxBrokerHostRepository _brokerRepository;
        private readonly IEmqxBrokerService _emqxBrokerService;
        private readonly IUserRepository _userRepository;

        // Default session expiration in days
        private const int DEFAULT_SESSION_EXPIRATION_DAYS = 7;

        public MqttAuthService(
            ILogger<MqttAuthService> logger,
            IUserSessionRepository userSessionRepository,
            IEmqxBrokerHostRepository brokerRepository,
            IEmqxBrokerService emqxBrokerService,
            IUserRepository userRepository)
        {
            _logger = logger;
            _userSessionRepository = userSessionRepository;
            _brokerRepository = brokerRepository;
            _emqxBrokerService = emqxBrokerService;

            _userRepository = userRepository;
        }

        public async Task<SessionInfo> StartSessionAsync(MqttStartSessionRequest request)
        {
            try
            {
                if (request == null)
                {
                    throw new IOTHubException(400, "Request cannot be null");
                }

                int userId = request.UserId;
                string deviceId = request.DeviceInfo;

                _logger.LogInformation("Starting MQTT session for user {UserId} with device {DeviceId}", userId, deviceId);

                // Step 1: Check if user exists, create if not
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogInformation("User {UserId} not found. Creating new user.", userId);
                    user = await _userRepository.AddAsync(new User
                    {
                        VMLUserId = userId,
                        Phone = "Unknown" // Could be extracted from request details if needed
                    });
                }

                // Register the device if not already registered
                //todo: if deviceId already added please don't add
                await _userRepository.AddDeviceIdAsync(userId, deviceId);

                // Step 2: Get the least loaded broker
                var broker = await _brokerRepository.GetLeastLoadedBrokerAsync();
                if (broker == null)
                {
                    _logger.LogError("No available MQTT brokers found");
                    throw new IOTHubException(400, "No available MQTT brokers found");
                }

                // Step 3-4: Generate unique credentials and create session
                var sessionId = Guid.NewGuid();
                var brokerUsername = $"user_{userId}_{sessionId.ToString("N").Substring(0, 8)}";
                var brokerPassword = GenerateRandomPassword();

                // Create pub/sub topic patterns for this user
                var subTopics = GenerateSubscribeTopics(sessionId, broker);
                var pubTopics = GeneratePublishTopics(sessionId, broker);

                // Step 4: Create user in EMQX broker
                var userCreated = await _emqxBrokerService.CreateUserAsync(broker, brokerUsername, brokerPassword);
                if (!userCreated)
                {
                    _logger.LogError("Failed to create broker user for {UserId}", userId);
                    throw new IOTHubException(500, "Failed to create MQTT broker user");
                }

                // Create session sub/pub topics
                var sessionSubTopics = new List<SessionSubTopic>();
                var sessionPubTopics = new List<SessionPubTopic>();

                foreach (var topic in subTopics)
                {
                    sessionSubTopics.Add(new SessionSubTopic
                    {
                        UniqueId = Guid.NewGuid(),
                        Name = $"Subscribe_{topic}",
                        TopicPattern = topic,
                        IsActive = true,
                        UserSessionId = sessionId
                    });
                }

                foreach (var topic in pubTopics)
                {
                    sessionPubTopics.Add(new SessionPubTopic
                    {
                        UniqueId = Guid.NewGuid(),
                        Name = $"Publish_{topic}",
                        TopicPattern = topic,
                        IsActive = true,
                        UserSessionId = sessionId
                    });
                }

                // Step 4: Set user permissions in EMQX broker
                var permissionsSet = await _emqxBrokerService.SetUserPermissionsAsync(
                    broker,
                    brokerUsername,
                    pubTopics.ToArray(),
                    subTopics.ToArray());

               
                // Step 5-6: Create and save the session
                var userSession = new UserSession
                {
                    UniqueId = sessionId,
                    UserId = userId,
                    Host = broker.Ip,
                    Date = DateTime.UtcNow,
                    Type = "MQTT",
                    SubTopics = subTopics,
                    PubTopics = pubTopics,
                    Password = brokerPassword,
                    TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    SessionSubTopics = sessionSubTopics,
                    SessionPubTopics = sessionPubTopics
                };

                await _userSessionRepository.AddAsync(userSession);

                // Step 7: Return session info for client to connect to broker
                return new SessionInfo
                {
                    SessionId = sessionId,
                    BrokerHost = broker.Ip,
                    AccessKey = brokerPassword,
                    PublishTopics = pubTopics,
                    SubscribeTopics = subTopics
                };
            }
            catch (IOTHubException)
            {
                // Just re-throw IOTHubExceptions as they already have appropriate error details
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting MQTT session for user {UserId}", request?.UserId);
                throw new IOTHubException(500, $"Error starting MQTT session: {ex.Message}");
            }
        }

        public async Task<bool> EndSessionAsync(Guid sessionId)
        {
            try
            {
                // Step 8: Get session details
                var session = await _userSessionRepository.GetByIdAsync(sessionId);
                if (session == null)
                {
                    _logger.LogWarning("Session {SessionId} not found for termination", sessionId);
                    return false;
                }

                // Step 9: Remove user from EMQX broker
                // We need to reconstruct the broker username used
                var broker = await _brokerRepository.GetByIdAsync(session.BrokerHost.Id);
                var userDeleted = await _emqxBrokerService.DeleteUserAsync(broker, session.RefreshToken);

                if (!userDeleted)
                {
                    _logger.LogWarning("Failed to delete broker user for session {SessionId}", sessionId);
                    // Continue anyway to clean up database
                }

                // Mark session as expired
                //todo: set userSession IsActive = false
                _logger.LogInformation("Successfully terminated session {SessionId}", sessionId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending MQTT session {SessionId}", sessionId);
                return false;
            }
        }

        private List<string> GenerateSubscribeTopics(Guid seesionId, EmqxBrokerHost broker)
        {
            // Generate subscribe topic patterns based on broker configuration
            var topics = new List<string>
            {
                $"vml_notify/{broker.Id}/{seesionId}",
                $"vml_command_client_request/{broker.Id}/{seesionId}"
            };

            return topics;
        }

        private List<string> GeneratePublishTopics(Guid seesionId, EmqxBrokerHost broker)
        {
            // Generate publish topic patterns based on broker configuration
            var topics = new List<string>
            {
               $"vml_command_client_response/{broker.Id}/{seesionId}"
            };

            return topics;
        }

        private string GenerateRandomPassword(int length = 16)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}

