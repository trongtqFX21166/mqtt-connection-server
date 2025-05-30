using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Models
{
    public class MqttMessageResponse
    {
        public MqttMessageResponseHeader Header { get; set; }

        public string Payload { get; set; }
    }

    public class MqttMessageResponseHeader
    {
        public string Namespace { get; set; }

        public string Name { get; set; }

        public int Status { get; set; }

        public string StatusDesc { get; set; }

        public long TimeStamp { get; set; }

        public Guid SeqId { get; set; }
    }


}
