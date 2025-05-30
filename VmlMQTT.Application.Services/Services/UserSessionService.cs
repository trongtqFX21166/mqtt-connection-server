using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Interfaces.Repositories;

namespace VmlMQTT.Application.Services
{
    public class UserSessionInfo
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string SessionId { get; set; }
        public List<string> PublishTopics { get; set; } = new();
        public List<string> SubscribeTopics { get; set; } = new();
    }

    public class UserSessionService : IUserSessionService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserSessionRepository _sessionRepository;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UserSessionService> _logger;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);

        public UserSessionService(
            IUserRepository userRepository,
            IUserSessionRepository sessionRepository,
            IMemoryCache cache,
            ILogger<UserSessionService> logger)
        {
            _userRepository = userRepository;
            _sessionRepository = sessionRepository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<UserSessionInfo> GetSessionAsync(string phone, string deviceId)
        {
            var cacheKey = $"session:{phone}:{deviceId}";

            if (_cache.TryGetValue(cacheKey, out UserSessionInfo cachedSession))
            {
                return cachedSession;
            }

            var user = await _userRepository.GetByPhoneAsync(phone);
            if (user == null)
            {
                throw new InvalidOperationException($"User with phone {phone} not found");
            }

            var session = await _sessionRepository.GetByUserIdAndDeviceIdAsync(user.VMLUserId, deviceId);
            if (session == null || !session.IsActive)
            {
                throw new InvalidOperationException($"No active session found for device {deviceId}");
            }

            var sessionInfo = new UserSessionInfo
            {
                Host = session.Host,
                Port = session.BrokerHost?.Port ?? 1883,
                Username = session.RefreshToken,
                Password = session.Password,
                SessionId = session.UniqueId.ToString(),
                PublishTopics = session.PubTopics,
                SubscribeTopics = session.SubTopics
            };

            _cache.Set(cacheKey, sessionInfo, _cacheExpiration);
            return sessionInfo;
        }

        public async Task<bool> HasCommandPermissionAsync(string phone, string deviceId, string command)
        {
            try
            {
                var session = await GetSessionAsync(phone, deviceId);
                // Add your permission logic here
                // For now, just check if session exists
                return !string.IsNullOrEmpty(session.SessionId);
            }
            catch
            {
                return false;
            }
        }
    }
}
