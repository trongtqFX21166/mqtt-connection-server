using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Interfaces
{
    public interface IEmqxBrokerService
    {
        Task<bool> CreateUserAsync(string username, string password);
        Task<bool> DeleteUserAsync(string username);
        Task<bool> SetUserPermissionsAsync(string username, string[] pubTopics, string[] subTopics);
    }
}
