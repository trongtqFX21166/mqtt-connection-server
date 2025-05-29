using VmlMQTT.Core.Abstraction;

namespace VmlMQTT.Core.Entities
{
    public class UserDeviceId : BaseEntity
    {
        public Guid UniqueId { get; set; }
        public int UserId { get; set; }
        public string DeviceId { get; set; }

        public string? DeviceInfo { get; set; }

        // Navigation properties
        public User User { get; set; }
    }
}
