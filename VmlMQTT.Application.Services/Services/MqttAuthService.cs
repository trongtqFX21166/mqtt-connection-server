using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VietmapCloud.Shared.Redis;
using VmlMQTT.Application.DTOs;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Core.Models;

namespace VmlMQTT.Application.Services
{
    public class MqttAuthService : IMqttAuthService
    {
        private const string BROKER_CACHE_KEY = "EmqxBrokers";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        private readonly IMemoryCache _memoryCache;
        private readonly IRedisCache _redisCache;

        private readonly ILogger<MqttAuthService> _logger;
        private readonly IUserSessionRepository _userSessionRepository;
        private readonly IEmqxBrokerHostRepository _brokerRepository;
        private readonly IEmqxBrokerService _emqxBrokerService;
        private readonly IUserRepository _userRepository;

        private readonly ClientSetting _clientSetting;
        private readonly ServerSetting _serverSetting;

        public MqttAuthService(
            IOptions<ClientSetting> clientSettingOptions,
            IOptions<ServerSetting> serverSettingOptions,
            ILogger<MqttAuthService> logger,
            IUserSessionRepository userSessionRepository,
            IEmqxBrokerHostRepository brokerRepository,
            IEmqxBrokerService emqxBrokerService,
            IUserRepository userRepository,
            IMemoryCache memoryCache,
            IRedisCache redisCache)
        {
            _logger = logger;
            _userSessionRepository = userSessionRepository;
            _brokerRepository = brokerRepository;
            _emqxBrokerService = emqxBrokerService;
            _userRepository = userRepository;

            _clientSetting = clientSettingOptions.Value ?? new ClientSetting();
            _serverSetting = serverSettingOptions.Value ?? new ServerSetting();
            _memoryCache = memoryCache;
            _redisCache = redisCache;
        }

        public async Task<SessionInfo> StartSessionAsync(MqttStartSessionRequest request)
        {
            try
            {
                if (!await StartSessionValidation(request))
                {
                    _logger.LogError("Invalid request for starting MQTT session");
                    throw new IOTHubException(400, "Invalid request for starting MQTT session");

                }

                int userId = request.UserId;
                string deviceId = request.Imei;

                _logger.LogInformation("Starting MQTT session for user {UserId} with device {DeviceId}", userId, deviceId);

                // Ensure user exists
                await EnsureUserExistsAsync(userId, request.Phone);

                var existingSessionInfo = await TryReuseExistingSessionAsync(request.RefreshToken);
                if (existingSessionInfo != null)
                {
                    return existingSessionInfo;
                }

                // Register the device if not already registered
                await RegisterDeviceAsync(userId, deviceId, request.DeviceInfo);

                // Create a new session
                return await CreateNewSessionAsync(userId, deviceId, request);
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

        private async Task<bool> StartSessionValidation(MqttStartSessionRequest request)
        {
            if (request == null)
            {
                throw new IOTHubException(400, "Request cannot be null");
            }
            var userToken = await _redisCache.GetAsync<UserTokenDto>("UserToken", request.UserId.ToString());

            var checkExistRefreshToken = userToken.GetAll.FirstOrDefault(x => x.RefreshToken == request.RefreshToken);
            if (checkExistRefreshToken == null)
            {
                _logger.LogError("Refresh token {RefreshToken} not found for user {UserId}", request.RefreshToken, request.UserId);
                throw new IOTHubException(404, "Refresh token not found");
            }
            else if (!checkExistRefreshToken.Imei.Equals(request.Imei))
            {
                _logger.LogError("Imei {Imei} not found for user {UserId}", request.Imei, request.UserId);
                throw new IOTHubException(404, "Imei not found");
            }
            return true;
        }

        private async Task EnsureUserExistsAsync(int userId, string phone = "Unknown")
        {
            var user = await _userRepository.GetByIdAsync(userId, true);
            if (user == null)
            {
                _logger.LogInformation("User {UserId} not found. Creating new user.", userId);
                await _userRepository.AddAsync(new Core.Entities.User
                {
                    VMLUserId = userId,
                    Phone = phone, // Could be extracted from request details if needed
                    LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                });
            }
        }

        private async Task RegisterDeviceAsync(int userId, string deviceId, string deviceInfo)
        {
            bool deviceAdded = await _userRepository.AddDeviceIdAsync(userId, deviceId, deviceInfo);
            if (deviceAdded)
            {
                _logger.LogInformation("Device {DeviceId} registered for user {UserId}", deviceId, userId);
            }
        }

        private async Task<SessionInfo> TryReuseExistingSessionAsync(string refreshToken)
        {
            var existingSession = await _userSessionRepository.GetByRefreshTokenAsync(refreshToken);
            if (existingSession?.IsActive != true)
            {
                return null!;
            }

            // First, try to reuse the existing broker
            if (existingSession.BrokerHost?.IsActive == true)
            {
                var leastLoadedBrokerId = await GetLeastLoadedBrokerAsync();
                if (leastLoadedBrokerId == existingSession.BrokerHost.Id)
                {
                    _logger.LogInformation("Using existing session {SessionId} for user {UserId}",
                        existingSession.UniqueId, existingSession.UserId);

                    return CreateSessionInfoFrom(existingSession);
                }
                else
                {
                    _logger.LogWarning("Existing broker {BrokerId} is not the least loaded. Migrating session {SessionId} to new broker.",
                        existingSession.BrokerHost.Id, existingSession.UniqueId);
                    await RemoveEmqxSession(existingSession);
                }
            }
            // If original broker isn't available, try to migrate to a new broker
            return await TryMigrateSessionToDifferentBrokerAsync(existingSession);
        }

        private async Task<SessionInfo> TryMigrateSessionToDifferentBrokerAsync(UserSession existingSession)
        {
            List<EmqxBrokerHost> ignoreBrokers = [];
            if (existingSession.BrokerHost != null)
            {
                ignoreBrokers.Add(existingSession.BrokerHost);
            }

            while (true)
            {
                var leastBrokerId = await GetLeastLoadedBrokerAsync(ignoreBrokers);

                // No more available brokers
                if (leastBrokerId == 0)
                {
                    _logger.LogWarning("No available brokers for session {SessionId}. Deactivating.",
                        existingSession.UniqueId);

                    existingSession.IsActive = false;
                    await _userSessionRepository.UpdateAsync(existingSession);
                    return null!;
                }

                var leastBroker = await _brokerRepository.GetByIdAsync(leastBrokerId);

                try
                {


                    existingSession.SubTopics = GenerateSubscribeTopics(existingSession.UniqueId, leastBroker);
                    existingSession.PubTopics = GeneratePublishTopics(existingSession.UniqueId, leastBroker);

                    // Try to set up the session on the new broker
                    if (await SetupExistingSessionOnNewBrokerAsync(existingSession, leastBroker))
                    {
                        // Update session with new broker info
                        existingSession.Host = leastBroker.Ip;
                        existingSession.BrokerHost = leastBroker;
                        await _userSessionRepository.UpdateAsync(existingSession);
                        return CreateSessionInfoFrom(existingSession);
                    }
                }
                catch (Exception ex)
                {
                    // If this broker fails, ignore it and try the next one
                    _logger.LogError(ex, "Failed to set up session on broker {BrokerId}. Trying next broker.", leastBroker.Id);
                }

                ignoreBrokers.Add(leastBroker);
            }
        }

        private async Task RemoveEmqxSession(UserSession session)
        {
            if (session?.BrokerHost == null) return;

            try
            {
                // ✅ Parallel execution với proper error handling
                var deleteUserTask = _emqxBrokerService.DeleteUserAsync(session.BrokerHost, session.RefreshToken);
                var deleteRolesTask = _emqxBrokerService.DeleteUserRolesAsync(session.BrokerHost, session.RefreshToken);

                await Task.WhenAll(deleteUserTask, deleteRolesTask);

                _logger.LogInformation("Successfully cleaned up session {SessionId} from broker {BrokerId}",
                    session.UniqueId, session.BrokerHost.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup session {SessionId} from broker {BrokerId}",
                    session.UniqueId, session.BrokerHost?.Id);
                // ✅ Don't throw - cleanup failure shouldn't break main flow
            }
        }

        private async Task<bool> SetupExistingSessionOnNewBrokerAsync(UserSession session, EmqxBrokerHost leastBroker)
        {
            // Create the user on the new broker
            if (!await _emqxBrokerService.CreateUserAsync(leastBroker, session.RefreshToken, session.Password))
            {
                return false;
            }

            // Set user permissions
            return await _emqxBrokerService.SetUserPermissionsAsync(
                leastBroker,
                session.RefreshToken,
                session.PubTopics.ToArray(),
                session.SubTopics.ToArray(),
                _clientSetting.DenyPublishTopics?.ToArray() ?? Array.Empty<string>(),
                _clientSetting.DenySubTopics?.ToArray() ?? Array.Empty<string>());
        }

        private async Task<SessionInfo> CreateNewSessionAsync(int userId, string deviceId, MqttStartSessionRequest request)
        {
            // Get broker
            var brokerId = await GetLeastLoadedBrokerAsync([]);
            if (brokerId == 0)
            {
                _logger.LogError("No available MQTT brokers found");
                throw new IOTHubException(500, "No available MQTT brokers found");
            }

            var broker = await _brokerRepository.GetByIdAsync(brokerId);

            // Generate session details
            var sessionId = Guid.NewGuid();
            var brokerUsername = string.IsNullOrEmpty(request.RefreshToken)
                ? $"user_{userId}_{sessionId:N}"
                : request.RefreshToken;
            var brokerPassword = GenerateRandomPassword();

            // Create topic patterns
            var subTopics = GenerateSubscribeTopics(sessionId, broker);
            var pubTopics = GeneratePublishTopics(sessionId, broker);

            // Set up the broker user and permissions
            if (!await SetupBrokerUserAsync(broker, brokerUsername, brokerPassword, pubTopics, subTopics))
            {
                throw new IOTHubException(500, "Failed to set up MQTT broker user");
            }

            // Create and save session
            var userSession = CreateUserSession(
                sessionId, userId, deviceId, broker, brokerPassword,
                request.RefreshToken, request.AccessToken, subTopics, pubTopics);

            await _userSessionRepository.AddAsync(userSession);

            // Return session info
            return CreateSessionInfoFrom(userSession);
        }

        private async Task<bool> SetupBrokerUserAsync(
            EmqxBrokerHost broker, string username, string password,
            List<string> pubTopics, List<string> subTopics)
        {
            // Create user
            var userCreated = await _emqxBrokerService.CreateUserAsync(broker, username, password);
            if (!userCreated)
            {
                _logger.LogError("Failed to create broker user {Username}", username);
                return false;
            }

            // Set permissions
            var permissionsSet = await _emqxBrokerService.SetUserPermissionsAsync(
                broker,
                username,
                pubTopics.ToArray(),
                subTopics.ToArray(),
                _clientSetting.DenyPublishTopics?.ToArray() ?? Array.Empty<string>(),
                _clientSetting.DenySubTopics?.ToArray() ?? Array.Empty<string>());

            if (!permissionsSet)
            {
                _logger.LogWarning("Failed to set permissions for broker user {Username}. Using defaults.", username);
            }

            return true;
        }

        private static UserSession CreateUserSession(
            Guid sessionId, int userId, string deviceId, EmqxBrokerHost broker,
            string password, string refreshToken, string accessToken,
            List<string> subTopics, List<string> pubTopics)
        {
            return new UserSession
            {
                UniqueId = sessionId,
                UserId = userId,
                DeviceId = deviceId,
                Host = broker.Ip,
                Date = DateTime.UtcNow,
                Type = "MQTT",
                SubTopics = subTopics,
                PubTopics = pubTopics,
                Password = password,
                RefreshToken = string.IsNullOrEmpty(refreshToken) ?
                    Guid.NewGuid().ToString("N") : refreshToken,
                AccessToken = string.IsNullOrEmpty(accessToken) ?
                    Guid.NewGuid().ToString("N") : accessToken,
                TimestampUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsActive = true,
                BrokerHost = broker
            };
        }

        private static SessionInfo CreateSessionInfoFrom(UserSession session)
        {
            return new SessionInfo
            {
                SessionId = session.UniqueId,
                BrokerHost = session.BrokerHost?.PublicIp ?? session.Host,
                BrokerPort = session.BrokerHost?.PublicPort ?? 1883,
                AccessKey = session.Password,
                PublishTopics = session.PubTopics,
                SubscribeTopics = session.SubTopics
            };
        }

        public async Task<bool> UpdateSessionAsync(string accessToken, string refreshToken)
        {
            try
            {
                var userSession = await _userSessionRepository.GetByRefreshTokenAsync(refreshToken);

                if (userSession == null)
                {
                    _logger.LogWarning("Session {refreshToken} not found for termination", refreshToken);
                    return false;
                }

                userSession.AccessToken = accessToken;
                await _userSessionRepository.UpdateAsync(userSession);

                _logger.LogInformation("Successfully update session {refreshToken}", refreshToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending MQTT session {refreshToken}", refreshToken);
                return false;
            }
        }

        public async Task<bool> EndSessionAsync(string userName)
        {
            try
            {
                // Step 8: Get session details
                var session = await _userSessionRepository.GetByRefreshTokenAsync(userName);
                if (session == null)
                {
                    _logger.LogWarning("Session {userName} not found for termination", userName);
                    return false;
                }

                if (!session.IsActive)
                {
                    _logger.LogInformation("Session {userName} is already inactive", userName);
                    return true;
                }

                // Step 9: Remove user from EMQX broker
                var broker = session.BrokerHost;
                if (broker == null)
                {
                    _logger.LogWarning("Broker not found for session {userName}", userName);
                    // Continue anyway to clean up database
                }
                else
                {
                    var userDeleted = await _emqxBrokerService.DeleteUserAsync(broker, session.RefreshToken);
                    if (!userDeleted)
                    {
                        _logger.LogWarning("Failed to delete broker user for session {userName}", userName);
                        // Continue anyway to clean up database
                    }

                    var userRoleDeleted = await _emqxBrokerService.DeleteUserRolesAsync(broker, session.RefreshToken);
                    if (!userRoleDeleted)
                    {
                        _logger.LogWarning("Failed to delete broker user for session {userName}", userName);
                        // Continue anyway to clean up database
                    }
                }

                // Mark session as inactive
                session.IsActive = false;
                await _userSessionRepository.UpdateAsync(session);

                _logger.LogInformation("Successfully terminated session {userName}", userName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending MQTT session {userName}", userName);
                return false;
            }
        }

        private List<string> GenerateSubscribeTopics(Guid sessionId, EmqxBrokerHost broker)
        {

            // Generate subscribe topic patterns based on broker configuration
            var topics = new List<string>();

            foreach (var topic in _clientSetting.AllowSubTopics)
            {
                topics.Add(topic
                    .Replace("{hostid}", broker.Id.ToString())
                    .Replace("{sessionid}", sessionId.ToString()));
            }

            return topics;
        }

        private List<string> GeneratePublishTopics(Guid sessionId, EmqxBrokerHost broker)
        {
            if (_clientSetting.AllowPublishTopics == null || !_clientSetting.AllowPublishTopics.Any())
            {
                return new List<string> { $"client/{broker.Id}/{sessionId}/+" };
            }

            // Generate publish topic patterns based on broker configuration
            var topics = new List<string>();

            foreach (var topic in _clientSetting.AllowPublishTopics)
            {
                topics.Add(topic
                     .Replace("{hostid}", broker.Id.ToString())
                     .Replace("{sessionid}", sessionId.ToString()));
            }

            return topics;
        }

        private static string GenerateRandomPassword(int length = 16)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private async Task<int> GetLeastLoadedBrokerAsync(List<EmqxBrokerHost> ignoreBrokes = default!)
        {
            // Thử lấy từ cache trước
            if (_memoryCache.TryGetValue(BROKER_CACHE_KEY, out List<(int emqxId, long connections)>? cacheBrokers) && cacheBrokers?.Count > 0)
            {
                // Loại bỏ các broker cần bỏ qua
                cacheBrokers = [.. cacheBrokers.Where(x => ignoreBrokes?.Any(b => b.Id == x.emqxId) != true)];

                // Nếu có trong cache, trả về broker có tải thấp nhất
                if (cacheBrokers.Count > 0)
                {
                    return cacheBrokers.OrderBy(x => x.connections)
                        .ThenBy(x => x.emqxId)
                        .FirstOrDefault().emqxId;
                }
            }

            cacheBrokers = [];
            // Nếu không có trong cache thì lấy tất cả brokers từ repository
            var brokers = await _brokerRepository.GetAllAsync();
            foreach (var broker in brokers)
            {
                // Bỏ qua các broker cần loại trừ
                if (ignoreBrokes?.Any(b => b.Id == broker.Id) == true)
                {
                    continue;
                }

                var (success, currentLiveConnection) = await _emqxBrokerService.CurrentLiveConnectionAsync(broker);
                if (success)
                {
                    // Thêm vào cache
                    cacheBrokers.Add((broker.Id, currentLiveConnection));
                }
            }

            if (cacheBrokers.Count > 0)
            {
                _memoryCache.Set(BROKER_CACHE_KEY, cacheBrokers, _cacheExpiration);
                return cacheBrokers.OrderBy(x => x.connections)
                    .ThenBy(x => x.emqxId)
                    .FirstOrDefault().emqxId;
            }

            return default!;
        }
    }
}