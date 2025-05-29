using VmlMQTT.Core.Entities;

namespace VmlMQTT.Core.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<User> GetByIdAsync(int userId, bool asNoTracking = false);
        Task<User> AddAsync(User user);
        Task<bool> AddDeviceIdAsync(int userId, string deviceId, string deviceInfo);
        Task<User> GetByPhoneAsync(string phone);
    }
}
