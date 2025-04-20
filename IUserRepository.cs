using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Core.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(string userId);
        Task<User> AddAsync(User user);
        Task<bool> AddDeviceIdAsync(string userId, string deviceId);
    }
}
