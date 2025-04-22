using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Application.Interfaces
{
    public interface IEmqxBrokerService
    {
        Task<bool> CreateUserAsync(EmqxBrokerHost host,string username, string password);
        Task<bool> DeleteUserAsync(EmqxBrokerHost host, string username);
        Task<bool> SetUserPermissionsAsync(EmqxBrokerHost host, string username, string[] pubTopics, string[] subTopics, string[] denyPubTopics, string[] denySubTopics);
    }
}
