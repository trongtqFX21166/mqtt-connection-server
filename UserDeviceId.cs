using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Core.Entities
{
    public class UserDeviceId
    {
        public Guid UniqueId { get; set; }
        public int UserId { get; set; }
        public string DeviceId { get; set; }

        // Navigation properties
        public User User { get; set; }
    }
}
