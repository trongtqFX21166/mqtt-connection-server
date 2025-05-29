using System.Collections.Concurrent;

namespace VmlMQTT.Application.Models
{
    public class SendCommandRequest
    {
        public string SessionId { get; set; }

        public string DeviceId { get; set; }

        public string Phone { get; set; }

        public string RequestId { get; set; }

        public ConcurrentDictionary<string, List<CommandResponse>> ResponseCommands { get; set; }
    }
}
