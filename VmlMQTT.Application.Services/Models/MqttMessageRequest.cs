using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Services
{
    public class MqttMessageRequest
    {
        public MqttMessageRequestHeader Header { get; set; }

        public string Payload { get; set; }
    }

    public class MqttMessageRequestHeader
    {
        public string Namespace { get; set; }

        public string Name { get; set; }

        public long TimeStamp { get; set; }

        public int SeqId { get; set; }
    }
}
