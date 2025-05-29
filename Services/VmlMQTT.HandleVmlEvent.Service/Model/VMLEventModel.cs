using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.HandleVmlEvent.Service.Model
{
    public class VMLEventModel
    {
        public string Type { get; set; }

        public string Event { get; set; }

        public JArray Datas { get; set; }
    }
}
