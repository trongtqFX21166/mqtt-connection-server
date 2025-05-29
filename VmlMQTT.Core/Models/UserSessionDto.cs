namespace VmlMQTT.Core.Models
{
    public class UserSessionDto
    {
        public Guid UniqueId { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public long TimestampUnix { get; set; }
    }
}
