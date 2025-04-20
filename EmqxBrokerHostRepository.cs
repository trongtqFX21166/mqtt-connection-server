using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VmlMQTT.Core.Entities;
using VmlMQTT.Core.Interfaces.Repositories;
using VmlMQTT.Infratructure.Data;

namespace VmlMQTT.Infratructure.Repositories
{
    public class EmqxBrokerHostRepository : IEmqxBrokerHostRepository
    {
        private readonly VmlMQTTDbContext _dbContext;

        public EmqxBrokerHostRepository(VmlMQTTDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<EmqxBrokerHost>> GetAllAsync()
        {
            return await _dbContext.EmqxBrokerHosts.ToListAsync();
        }

        public async Task<EmqxBrokerHost> GetByIdAsync(int id)
        {
            return await _dbContext.EmqxBrokerHosts.FindAsync(id);
        }

        public async Task<EmqxBrokerHost> GetLeastLoadedBrokerAsync()
        {
            // Get broker with the least number of active sessions
            var brokerSessionCounts = await _dbContext.UserSessions
                .Where(s => !s.IsRefreshTokenExpired)
                .GroupBy(s => s.Host)
                .Select(g => new { Host = g.Key, Count = g.Count() })
                .ToListAsync();

            var allBrokers = await _dbContext.EmqxBrokerHosts.ToListAsync();

            // If there are brokers with no sessions, pick the first one
            var brokersWithNoSessions = allBrokers
                .Where(b => !brokerSessionCounts.Any(c => c.Host == b.Ip))
                .ToList();

            if (brokersWithNoSessions.Any())
            {
                return brokersWithNoSessions.First();
            }

            // Otherwise, pick the broker with the least sessions
            var leastLoadedBrokerIp = brokerSessionCounts
                .OrderBy(c => c.Count)
                .First()
                .Host;

            return allBrokers.First(b => b.Ip == leastLoadedBrokerIp);
        }
    }
}
