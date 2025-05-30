using System.Collections.Concurrent;

namespace VmlMQTT.Application.Models
{
    public class SendCommandRequest
    {
        public string SessionId { get; set; }
        public string DeviceId { get; set; }
        public string Phone { get; set; }
        public string RequestId { get; set; }
        public string Command { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 30;
    }
}
