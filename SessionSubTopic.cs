using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Core.Entities
{
    public class SessionSubTopic
    {
        public Guid UniqueId { get; set; }
        public string Name { get; set; }
        public string TopicPattern { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties
        public UserSession UserSession { get; set; }
        public Guid UserSessionId { get; set; }
    }
}
