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

        public int BrokerPort { get; set; }
        public string AccessKey { get; set; }
        public List<string> PublishTopics { get; set; } = new List<string>();
        public List<string> SubscribeTopics { get; set; } = new List<string>();
    }
}
