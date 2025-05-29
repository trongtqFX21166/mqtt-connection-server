using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Models
{
    public class CommandRequest
    {
        public string SessionId { get; set; }

        public string RequestId { get; set; }
    }
}
