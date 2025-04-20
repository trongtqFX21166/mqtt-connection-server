using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Application.DTOs;

namespace VmlMQTT.Application.Interfaces
{
    public interface IMqttAuthService
    {
        Task<SessionInfo> StartSessionAsync(string userId, string deviceId);
        Task<bool> EndSessionAsync(Guid sessionId);
        Task<bool> ValidateCredentialsAsync(string username, string password);
        Task<bool> ValidateTopicPermissionAsync(string username, string topic, bool isPublish);
    }
}
