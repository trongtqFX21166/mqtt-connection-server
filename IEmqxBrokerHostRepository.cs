using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;

namespace VmlMQTT.Core.Interfaces.Repositories
{
    public interface IEmqxBrokerHostRepository
    {
        Task<List<EmqxBrokerHost>> GetAllAsync();
        Task<EmqxBrokerHost> GetByIdAsync(int id);
        Task<EmqxBrokerHost> GetLeastLoadedBrokerAsync();
    }
}
