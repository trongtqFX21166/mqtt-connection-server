using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Models
{
    public class IOTHubResponse<T> where T : class
    {
        public int Code { get; set; }

        public string Msg { get; set; }

        public T Data { get; set; }

    }
}