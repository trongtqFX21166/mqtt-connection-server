using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.HandleVmlEvent.Service.Model
{
    public class EventDto
    {
        public UserEventDto User { get; set; }

        public string Package { get; set; }
    }

    public class UserEventDto
    {
        public string Phone { get; set; }

        public int CreatedDate { get; set; }
    }
}
