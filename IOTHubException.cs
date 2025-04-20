using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VmlMQTT.Core.Models
{
    public class IOTHubException : Exception
    {
        public int Code { get; set; }


        public IOTHubException(int code, string message)
            : base(message)
        {
            Code = code;

        }

        [JsonIgnore]
        public HttpStatusCode? StatusCode { get; set; }
        public string Imei { get; set; }
        public IOTHubException(HttpStatusCode StatusCode, string imei, int code, string message)
            : base(message)
        {
            Code = code;
            Imei = imei;
        }
    }
}
