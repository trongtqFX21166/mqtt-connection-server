using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.DTOs;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;

namespace VmlMQTT.Application.Services
{
    public class MqttAuthService : IMqttAuthService
    {
        private readonly ILogger<MqttAuthService> _logger;
        private readonly IUserSessionRepository _userSessionRepository;
        private readonly IEmqxBrokerHostRepository _brokerRepository;
        private readonly IEmqxBrokerService _emqxBrokerService;

        // Default session expiration in days
        private const int DEFAULT_SESSION_EXPIRATION_DAYS = 7;

        public MqttAuthService(
            ILogger<MqttAuthService> logger,
            IUserSessionRepository userSessionRepository,
            IEmqxBrokerHostRepository brokerRepository,
            IEmqxBrokerService emqxBrokerService)
        {
            _logger = logger;
            _userSessionRepository = userSessionRepository;
            _brokerRepository = brokerRepository;
            _emqxBrokerService = emqxBrokerService;
        }

        public async Task<SessionInfo> StartSessionAsync(string userId, string deviceId)
        {
            // todo:
            //1. Check userId not existed, create new user

            //2. Get randoom MQTT Broker Host

            //3. Generate UserSession with Host Info

            //4. Call MQTT Broker Host Api from step 2
            //4.1 Create Account
            //4.2 Assign Roles
            try
            {
                _logger.LogInformation("Starting MQTT session for user {UserId} with device {DeviceId}", userId, deviceId);

                // Step 3: Get the least loaded broker
                var broker = await _brokerRepository.GetLeastLoadedBrokerAsync();
                if (broker == null)
                {
                    _logger.LogError("No available MQTT brokers found");
                    return null;
                }

                // Generate unique credentials for this session
                var sessionId = Guid.NewGuid();
                var brokerUsername = $"user_{userId}_{sessionId.ToString("N").Substring(0, 8)}";
                var brokerPassword = GenerateRandomPassword();
                var refreshToken = GenerateRefreshToken();

                // Create pub/sub topic patterns for this user
                var subTopics = GenerateSubscribeTopics(userId, broker);
                var pubTopics = GeneratePublishTopics(userId, broker);

                // Step 4: Create user in EMQX broker
                var userCreated = await _emqxBrokerService.CreateUserAsync(brokerUsername, brokerPassword);
                if (!userCreated)
                {
                    _logger.LogError("Failed to create broker user for {UserId}", userId);
                    return null;
                }

                // Step 4: Set user permissions in EMQX broker
                var permissionsSet = await _emqxBrokerService.SetUserPermissionsAsync(
                    brokerUsername,
                    pubTopics.ToArray(),
                    subTopics.ToArray());

                if (!permissionsSet)
                {
                    _logger.LogError("Failed to set broker permissions for {UserId}", userId);
                    // Cleanup the created user
                    await _emqxBrokerService.DeleteUserAsync(brokerUsername);
                    return null;
                }

                // Create and save the session
                var userSession = new UserSession
                {
                    UniqueId = sessionId,
                    UserId = userId,
                    Host = broker.Ip,
                    Date = DateTime.UtcNow,
                    Type = "MQTT",
                    SubTopics = subTopics,
                    PubTopics = pubTopics,
                    Password = brokerPassword,  // Consider encrypting this
                    RefreshToken = refreshToken,
                    IsRefreshTokenExpired = false,
                    TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                await _userSessionRepository.AddAsync(userSession);

                // Step 5: Return session info
                return new SessionInfo
                {
                    SessionId = sessionId,
                    BrokerHost = broker.Ip,
                    BrokerUsername = brokerUsername,
                    BrokerPassword = brokerPassword,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(DEFAULT_SESSION_EXPIRATION_DAYS),
                    PermittedPublishTopics = pubTopics,
                    PermittedSubscribeTopics = subTopics
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting MQTT session for user {UserId}", userId);
                return null;
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
                var brokerUsername = $"user_{session.UserId}_{sessionId.ToString("N").Substring(0, 8)}";
                var userDeleted = await _emqxBrokerService.DeleteUserAsync(brokerUsername);

                if (!userDeleted)
                {
                    _logger.LogWarning("Failed to delete broker user for session {SessionId}", sessionId);
                    // Continue anyway to clean up database
                }

                // Mark session as expired
                await _userSessionRepository.ExpireRefreshTokenAsync(sessionId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending MQTT session {SessionId}", sessionId);
                return false;
            }
        }

        public async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            // Extract session ID from username pattern
            try
            {
                var parts = username.Split('_');
                if (parts.Length < 3 || parts[0] != "user")
                {
                    return false;
                }

                var userId = parts[1];
                var sessionIdPart = parts[2];

                // Find all sessions for this user
                var userSessions = await _userSessionRepository.GetAllByUserIdAsync(userId);

                // Find the specific session that matches both the ID part and password
                var session = userSessions.FirstOrDefault(s =>
                    s.UniqueId.ToString("N").StartsWith(sessionIdPart) &&
                    s.Password == password &&
                    !s.IsRefreshTokenExpired);

                return session != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating MQTT credentials for {Username}", username);
                return false;
            }
        }

        public async Task<bool> ValidateTopicPermissionAsync(string username, string topic, bool isPublish)
        {
            try
            {
                // Extract session ID from username pattern
                var parts = username.Split('_');
                if (parts.Length < 3 || parts[0] != "user")
                {
                    return false;
                }

                var userId = parts[1];
                var sessionIdPart = parts[2];

                // Find all sessions for this user
                var userSessions = await _userSessionRepository.GetAllByUserIdAsync(userId);

                // Find the specific session
                var session = userSessions.FirstOrDefault(s =>
                    s.UniqueId.ToString("N").StartsWith(sessionIdPart) &&
                    !s.IsRefreshTokenExpired);

                if (session == null)
                {
                    return false;
                }

                // Check if the topic is in the allowed list
                var allowedTopics = isPublish ? session.PubTopics : session.SubTopics;

                // Topic patterns may contain wildcards, so we need to match patterns
                foreach (var allowedTopic in allowedTopics)
                {
                    if (IsTopicMatch(allowedTopic, topic))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating topic permission for {Username} on {Topic}", username, topic);
                return false;
            }
        }

        private List<string> GenerateSubscribeTopics(string userId, EmqxBrokerHost broker)
        {
            // Generate subscribe topic patterns based on broker configuration
            var topics = new List<string>
            {
                broker.TopicClientResponsePattern.Replace("{userId}", userId),
                broker.TopicNotifyPattern.Replace("{userId}", userId),
                // Add more default topics as needed
                $"user/{userId}/response/+",
                $"system/broadcast"
            };

            return topics;
        }

        private List<string> GeneratePublishTopics(string userId, EmqxBrokerHost broker)
        {
            // Generate publish topic patterns based on broker configuration
            var topics = new List<string>
            {
                broker.TopicClientRequestPattern.Replace("{userId}", userId),
                // Add more default topics as needed
                $"user/{userId}/request/+",
                $"user/{userId}/status"
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

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private bool IsTopicMatch(string pattern, string topic)
        {
            // Simple MQTT topic matching with support for single-level (+) and multi-level (#) wildcards
            var patternParts = pattern.Split('/');
            var topicParts = topic.Split('/');

            // If the pattern ends with #, it matches any number of levels
            if (patternParts[patternParts.Length - 1] == "#")
            {
                // Remove the # and check if the topic starts with the pattern prefix
                var prefix = string.Join("/", patternParts.Take(patternParts.Length - 1));
                return topic.StartsWith(prefix + "/") || topic == prefix;
            }

            if (patternParts.Length != topicParts.Length)
            {
                return false;
            }

            // Check each level
            for (int i = 0; i < patternParts.Length; i++)
            {
                if (patternParts[i] != "+" && patternParts[i] != topicParts[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
