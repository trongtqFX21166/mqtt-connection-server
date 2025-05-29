namespace VmlMQTT.Application.Models
{
    public class VmlBrokerLog
    {
        public long SystemTimstamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public string Category { get; set; }

        public long TimeStamp { get; set; }

        public string BrokerIp { get; set; }

        public int BrokerId { get; set; }

        public string ClientIp { get; set; }

        public string ClientId { get; set; }

        public string Username { get; set; }

        public string RefreshToken { get; set; }

        public string Phone { get; set; }

        public string Data { get; set; }
    }
}
