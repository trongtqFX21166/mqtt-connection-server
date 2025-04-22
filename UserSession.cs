namespace VmlMQTT.Core.Entities
{
    public class UserSession
    {
        public Guid UniqueId { get; set; }
        public int UserId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string Host { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public List<string> SubTopics { get; set; } = new List<string>();
        public List<string> PubTopics { get; set; } = new List<string>();
        public string Password { get; set; }
        public string RefreshToken { get; set; }
        public long TimestampUnix { get; set; }

        public bool IsActive { get; set; }

        // Navigation properties
        public User User { get; set; }
        public EmqxBrokerHost BrokerHost { get; set; }
    }
}
