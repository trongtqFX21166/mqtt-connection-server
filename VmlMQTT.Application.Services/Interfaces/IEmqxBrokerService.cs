using VmlMQTT.Application.Models;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Application.Interfaces
{
    public interface IEmqxBrokerService
    {
        Task<bool> CreateUserAsync(EmqxBrokerHost host, string username, string password);
        Task<bool> UpdateUserAsync(EmqxBrokerHost host, string username, string password);
        Task<bool> DeleteUserAsync(EmqxBrokerHost host, string username);
        Task<bool> DeleteUserRolesAsync(EmqxBrokerHost host, string username);
        Task<bool> SetUserPermissionsAsync(EmqxBrokerHost host, string username, string[] pubTopics, string[] subTopics, string[] denyPubTopics, string[] denySubTopics);
        Task<EMQXMonitorResponse[]> GetMqttMonitorAsync(EmqxBrokerHost host, long lastest = 120);
        Task<(bool, long)> CurrentLiveConnectionAsync(EmqxBrokerHost host);
    }
}
