using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Core.Interfaces.Repositories
{
    public interface IUserSessionRepository
    {
        Task<UserSession> GetByIdAsync(Guid id);
        Task<UserSession> GetByUserIdAndDeviceIdAsync(string userId, string deviceId);
        Task<List<UserSession>> GetAllByUserIdAsync(string userId);
        Task<UserSession> AddAsync(UserSession userSession);
        Task UpdateAsync(UserSession userSession);
        Task DeleteAsync(Guid id);
        Task<bool> IsSessionActiveAsync(Guid id);
        Task ExpireRefreshTokenAsync(Guid id);
    }
}
