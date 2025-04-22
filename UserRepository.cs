using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Infratructure.Data;

namespace VmlMQTT.Infratructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly VmlMQTTDbContext _dbContext;

        public UserRepository(VmlMQTTDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User> GetByIdAsync(int userId)
        {
            return await _dbContext.Users
                .Include(u => u.UserDeviceIds)
                .FirstOrDefaultAsync(u => u.VMLUserId == userId);
        }

        public async Task<User> AddAsync(User user)
        {
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();
            return user;
        }

        public async Task<bool> AddDeviceIdAsync(int userId, string deviceId)
        {
            try
            {
                // Check if device ID already exists directly in the database
                var existingDevice = await _dbContext.UserDeviceIds
                    .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

                if (existingDevice != null)
                {
                    return true; // Already exists, no need to add
                }

                // Add new device ID
                _dbContext.UserDeviceIds.Add(new UserDeviceId
                {
                    UserId = userId,
                    DeviceId = deviceId,
                    UniqueId = Guid.NewGuid()
                });

                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // If we get here, it likely means another process added the device concurrently
                // Check again to see if it exists now
                var existingDevice = await _dbContext.UserDeviceIds
                    .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

                return existingDevice != null;
            }
            catch (DbUpdateException)
            {
                // Handle unique constraint violation
                var existingDevice = await _dbContext.UserDeviceIds
                    .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceId == deviceId);

                return existingDevice != null;
            }
        }

        public async Task<UserSession> GetSessionByRefreshTokenAsync(string refreshToken)
        {
            return await _dbContext.UserSessions
                .Include(s => s.User)
                .Include(s => s.BrokerHost)
                .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken);
        }
    }
}