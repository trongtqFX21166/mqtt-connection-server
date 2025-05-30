using System.Collections.Concurrent;

namespace VmlMQTT.Application.Models
{
    public class SendCommandRequest
    {
        public Guid? SeqId { get; set; }

        public string DeviceId { get; set; }
        public string Phone { get; set; }
        public string PayLoad { get; set; }
        public bool IsAsync { get; set; } = false;
    }
}
