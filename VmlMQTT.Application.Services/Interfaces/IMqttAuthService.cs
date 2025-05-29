using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.DTOs;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Application.Interfaces
{
    public interface IMqttAuthService
    {
        Task<SessionInfo> StartSessionAsync(MqttStartSessionRequest request);
        Task<bool> EndSessionAsync(string userName);
        Task<bool> UpdateSessionAsync(string accessToken, string refreshToken);
    }
}
