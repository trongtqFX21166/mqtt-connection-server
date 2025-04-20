using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Infratructure.Data;

namespace VmlMQTT.Infratructure.Repositories
{
    public class UserSessionRepository : IUserSessionRepository
    {
        private readonly VmlMQTTDbContext _dbContext;

        public UserSessionRepository(VmlMQTTDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UserSession> GetByIdAsync(Guid id)
        {
            return await _dbContext.UserSessions
                .Include(s => s.User)
                .Include(s => s.BrokerHost)
                .Include(s => s.SessionSubTopics)
                .Include(s => s.SessionPubTopics)
                .FirstOrDefaultAsync(s => s.UniqueId == id);
        }

        public async Task<UserSession> GetByUserIdAndDeviceIdAsync(string userId, string deviceId)
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
                .Include(s => s.SessionSubTopics)
                .Include(s => s.SessionPubTopics)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.Date)
                .FirstOrDefaultAsync();
        }

        public async Task<List<UserSession>> GetAllByUserIdAsync(string userId)
        {
            return await _dbContext.UserSessions
                .Where(s => s.UserId == userId)
                .Include(s => s.BrokerHost)
                .Include(s => s.SessionSubTopics)
                .Include(s => s.SessionPubTopics)
                .ToListAsync();
        }

        public async Task<UserSession> AddAsync(UserSession userSession)
        {
            _dbContext.UserSessions.Add(userSession);
            await _dbContext.SaveChangesAsync();
            return userSession;
        }

        public async Task UpdateAsync(UserSession userSession)
        {
            _dbContext.Entry(userSession).State = EntityState.Modified;
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
            return !session.IsRefreshTokenExpired;
        }

        public async Task ExpireRefreshTokenAsync(Guid id)
        {
            var session = await _dbContext.UserSessions.FindAsync(id);
            if (session != null)
            {
                session.IsRefreshTokenExpired = true;
                await _dbContext.SaveChangesAsync();
            }
        }
    }
}
