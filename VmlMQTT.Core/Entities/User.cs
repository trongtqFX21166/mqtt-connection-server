using VmlMQTT.Core.Abstraction;

namespace VmlMQTT.Core.Entities
{
    public class User : BaseEntity
    {
        public int VMLUserId { get; set; }
        public string Phone { get; set; }

        // Navigation properties
        public List<UserDeviceId> UserDeviceIds { get; set; } = new List<UserDeviceId>();
        public List<UserSession> UserSessions { get; set; } = new List<UserSession>();
    }
}
