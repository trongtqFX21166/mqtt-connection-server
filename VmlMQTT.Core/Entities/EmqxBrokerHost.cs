using VmlMQTT.Core.Abstraction;

namespace VmlMQTT.Core.Entities
{
    public class EmqxBrokerHost : BaseEntity
    {
        public int Id { get; set; }

        public string Ip { get; set; }
        public int Port { get; set; }
        public string PublicIp { get; set; }
        public int PublicPort { get; set; }

        public string UserName { get; set; }
        public string Password { get; set; }

        public int TotalAccounts { get; set; }
        public int TotalConnections { get; set; }
        public long LimitConnections { get; set; }

        public bool IsActive { get; set; }

        // Navigation properties
        public List<UserSession> UserSessions { get; set; } = new List<UserSession>();
    }
}
