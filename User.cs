using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Core.Entities
{
    public class User
    {
        public int VMLUserId { get; set; }
        public string Phone { get; set; }



        public DateTime LastModified { get; set; }

        public DateTime LastModifiedBy { get; set; }


        // Navigation properties
        public List<UserDeviceId> UserDeviceIds { get; set; } = new List<UserDeviceId>();
        public List<UserSession> UserSessions { get; set; } = new List<UserSession>();
    }
}
