using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Models
{
    public class CommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int Code { get; set; }
        public object Data { get; set; }
        public TimeSpan Duration { get; set; }
        public Guid SeqId { get; set; }
    }
}
