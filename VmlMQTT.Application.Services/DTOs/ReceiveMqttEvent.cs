namespace VmlMQTT.Application.DTOs
{
    public class ReceiveMqttEvent
    {
        public string BrokerIp { get; set; }

        public string Event { get; set; }

        public long TimeStamp { get; set; }

        public string Peername { get; set; }

        public string ClientId { get; set; }

        public string Username { get; set; }

        public string? Reason { get; set; }
    }
}
