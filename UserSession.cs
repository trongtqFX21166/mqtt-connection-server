namespace VmlMQTT.Core.Entities
{
    public class UserSession
    {
        public Guid UniqueId { get; set; }
        public int UserId { get; set; }
        public string Host { get; set; }
        public DateTime Date { get; set; }
        public string Type { get; set; }
        public List<string> SubTopics { get; set; } = new List<string>();
        public List<string> PubTopics { get; set; } = new List<string>();
        public string Password { get; set; }
        public string RefreshToken { get; set; }
        public bool IsRefreshTokenExpired { get; set; }
        public long TimestampUnix { get; set; }

        // Navigation properties
        public User User { get; set; }
        public EmqxBrokerHost BrokerHost { get; set; }
        public List<SessionSubTopic> SessionSubTopics { get; set; } = new List<SessionSubTopic>();
        public List<SessionPubTopic> SessionPubTopics { get; set; } = new List<SessionPubTopic>();
    }
}
