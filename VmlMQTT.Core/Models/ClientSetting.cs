using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Core.Models
{
    public class ClientSetting
    {
        public List<string> DenyPublishTopics { get; set; }
        public List<string> DenySubTopics { get; set; }
        public List<string> AllowPublishTopics { get; set; }
        public List<string> AllowSubTopics { get; set; }
    }
}
