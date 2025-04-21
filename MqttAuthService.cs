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
                var refreshToken = GenerateRefreshToken();

                // Create pub/sub topic patterns for this user
                var subTopics = GenerateSubscribeTopics(userId, broker);
                var pubTopics = GeneratePublishTopics(userId, broker);

                // Step 4: Create user in EMQX broker
                var userCreated = await _emqxBrokerService.CreateUserAsync(brokerUsername, brokerPassword);
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
                    brokerUsername,
                    pubTopics.ToArray(),
                    subTopics.ToArray());

                if (!permissionsSet)
                {
                    _logger.LogError("Failed to set broker permissions for {UserId}", userId);
                    // Cleanup the created user
                    await _emqxBrokerService.DeleteUserAsync(brokerUsername);
                    throw new IOTHubException(500, "Failed to set MQTT broker permissions");
                }

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
                    RefreshToken = refreshToken,
                    IsRefreshTokenExpired = false,
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
                    BrokerUsername = brokerUsername,
                    BrokerPassword = brokerPassword,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddDays(DEFAULT_SESSION_EXPIRATION_DAYS),
                    PermittedPublishTopics = pubTopics,
                    PermittedSubscribeTopics = subTopics
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

                if (session.IsRefreshTokenExpired)
                {
                    _logger.LogInformation("Session {SessionId} already expired", sessionId);
                    return true; // Session already handled
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
                _logger.LogInformation("Successfully terminated session {SessionId}", sessionId);

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

        public async Task<bool> ValidateCredentialsAsync(string username, string password)
        {
            // Extract session ID from username pattern
            try
            {
                var parts = username.Split('_');
                if (parts.Length < 3 || parts[0] != "user")
                {
                    _logger.LogWarning("Invalid username format: {Username}", username);
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

                if (session == null)
                {
                    _logger.LogWarning("No valid session found for user {UserId} with session part {SessionIdPart}",
                        userId, sessionIdPart);
                    return false;
                }

                _logger.LogInformation("Successfully validated credentials for user {UserId}", userId);
                return true;
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
                    _logger.LogWarning("Invalid username format for topic validation: {Username}", username);
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
                    _logger.LogWarning("No valid session found for topic validation for user {UserId}", userId);
                    return false;
                }

                // Check if the topic is in the allowed list
                var allowedTopics = isPublish ? session.PubTopics : session.SubTopics;

                // Topic patterns may contain wildcards, so we need to match patterns
                foreach (var allowedTopic in allowedTopics)
                {
                    if (IsTopicMatch(allowedTopic, topic))
                    {
                        _logger.LogInformation("Topic {Topic} matched pattern {Pattern} for user {UserId}",
                            topic, allowedTopic, userId);
                        return true;
                    }
                }

                _logger.LogWarning("No matching topic permission found for {Topic} for user {UserId}", topic, userId);
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
            if (patternParts.Length > 0 && patternParts[patternParts.Length - 1] == "#")
            {
                // If pattern is just "#", it matches everything
                if (patternParts.Length == 1)
                {
                    return true;
                }

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
}
