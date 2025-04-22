using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Core.Entities
{
    public class EmqxBrokerHost
    {
        public int Id { get; set; }
        
        public string Ip { get; set; }

        public string UserName { get; set; }
        public string Password { get; set; }

        public int TotalAccounts { get; set; }
        public int TotalConnections { get; set; }

        public DateTime LastModified { get; set; }

        public DateTime LastModifiedBy { get; set; }

        public bool IsActive { get; set; }

        // Navigation properties
        public List<UserSession> UserSessions { get; set; } = new List<UserSession>();
    }
}
