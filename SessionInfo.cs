using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.DTOs
{
    public class SessionInfo
    {
        public Guid SessionId { get; set; }
        public string BrokerHost { get; set; }
        public string BrokerUsername { get; set; }
        public string BrokerPassword { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public List<string> PermittedPublishTopics { get; set; } = new List<string>();
        public List<string> PermittedSubscribeTopics { get; set; } = new List<string>();
    }
}
