using VmlMQTT.Core.Entities;

namespace VmlMQTT.Core.Interfaces.Repositories
{
    public interface IUserSessionRepository
    {
        Task<UserSession> GetByIdAsync(Guid id);
        Task<UserSession> GetByRefreshTokenAsync(string refreshToken);
        Task<UserSession> GetByUserIdAndDeviceIdAsync(int userId, string deviceId);
        Task<List<UserSession>> GetAllByUserIdAsync(int userId);
        Task<UserSession> AddAsync(UserSession userSession);
        Task UpdateAsync(UserSession userSession);
        Task DeleteAsync(Guid id);
        Task<bool> IsSessionActiveAsync(Guid id);
    }
}
