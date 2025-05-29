using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Infratructure.Data;

namespace VmlMQTT.Infratructure.Repositories
{
    public class UserSessionRepository : IUserSessionRepository
    {
        private readonly VmlMQTTDbContext _dbContext;
        private readonly ILogger<UserSessionRepository> _logger;

        public UserSessionRepository(VmlMQTTDbContext dbContext, ILogger<UserSessionRepository> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<UserSession> GetByIdAsync(Guid id)
        {
            return await _dbContext.UserSessions
                .Include(s => s.User)
                .Include(s => s.BrokerHost)
                .FirstOrDefaultAsync(s => s.UniqueId == id);
        }

        public async Task<UserSession> GetByRefreshTokenAsync(string refreshToken)
        {
            return await _dbContext.UserSessions
                .Include(s => s.User)
                .Include(s => s.BrokerHost)
                .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken && s.IsActive);
        }

        public async Task<UserSession> GetByUserIdAndDeviceIdAsync(int userId, string deviceId)
        {
            // Find the device
            var device = await _dbContext.UserDeviceIds
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

            if (device == null)
                return null;

            // Find the associated session (latest)
            return await _dbContext.UserSessions
                .Include(s => s.User)
                .Include(s => s.BrokerHost)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.Date)
                .FirstOrDefaultAsync();
        }

        public async Task<List<UserSession>> GetAllByUserIdAsync(int userId)
        {
            return await _dbContext.UserSessions
                .Where(s => s.UserId == userId && s.IsActive)
                .Include(s => s.BrokerHost)
                .ToListAsync();
        }

        public async Task<UserSession> AddAsync(UserSession userSession)
        {
            try
            {
                userSession.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                _dbContext.UserSessions.Add(userSession);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
            return userSession;
        }

        public async Task UpdateAsync(UserSession userSession)
        {
            _dbContext.Update(userSession);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var userSession = await _dbContext.UserSessions.FindAsync(id);
            if (userSession != null)
            {
                _dbContext.UserSessions.Remove(userSession);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<bool> IsSessionActiveAsync(Guid id)
        {
            var session = await _dbContext.UserSessions.FindAsync(id);
            if (session == null)
                return false;

            // Check if the session is still valid (not expired)
            return !session.IsActive;
        }

    }
}
