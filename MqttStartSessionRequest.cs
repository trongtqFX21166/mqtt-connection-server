using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.DTOs
{
    public class MqttStartSessionRequest
    {
        public int UserId { get; set; }
        public string DeviceInfo { get; set; }
        public string RefreshToken { get; set; }
    }
}
