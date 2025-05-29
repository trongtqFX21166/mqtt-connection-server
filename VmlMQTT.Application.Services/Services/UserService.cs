using VmlMQTT.Application.Interfaces;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Core.Models;

namespace VmlMQTT.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserSessionRepository _userSessionRepository;
        private readonly IUserRepository _userRepository;

        public UserService(IUserSessionRepository userSessionRepository, IUserRepository userRepository)
        {
            _userSessionRepository = userSessionRepository;
            _userRepository = userRepository;
        }

        public async Task<List<UserSessionDto>> QueryOnlineSessionsAsync(string phone)
        {
            var response = new List<UserSessionDto>();

            var user = await _userRepository.GetByPhoneAsync(phone);

            if(user == null)
            {
                return response;
            }

            var userSessions = await _userSessionRepository.GetAllByUserIdAsync(user.VMLUserId);

            if(userSessions?.Count > 0)
            {
                response.AddRange(userSessions.Select(s => new UserSessionDto
                {
                    UniqueId = s.UniqueId,
                    UserId = s.UserId,
                    Date = s.Date,
                    TimestampUnix = s.TimestampUnix
                }).ToList());
            }

            return response;
        }
    }
}
