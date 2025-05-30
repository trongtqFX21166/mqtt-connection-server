using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Models
{
    public class MqttConnectionConfig
    {
        public string Host { get; set; }
        public int Port { get; set; } = 1883;
        public string Username { get; set; }
        public string Password { get; set; }
        public string ClientId { get; set; }
        public int KeepAliveSeconds { get; set; } = 60;
    }
}
