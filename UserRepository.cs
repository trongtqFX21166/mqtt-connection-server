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
    public class UserRepository : IUserRepository
    {
        private readonly VmlMQTTDbContext _dbContext;

        public UserRepository(VmlMQTTDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User> GetByIdAsync(string userId)
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

        public async Task<bool> AddDeviceIdAsync(string userId, string deviceId)
        {
            var user = await GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            // Check if device ID already exists
            var existingDevice = user.UserDeviceIds
                .FirstOrDefault(d => d.DeviceId == deviceId);

            if (existingDevice != null)
            {
                return true; // Already exists, no need to add
            }

            // Add new device ID
            user.UserDeviceIds.Add(new UserDeviceId
            {
                UserId = userId,
                DeviceId = deviceId,
                UniqueId = Guid.NewGuid()
            });

            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
