using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VmlMQTT.Application.Models
{
    public static class Topic
    {
        public const string TOPIC_M2_REQUEST = "vm-command/{0}";
        public const string TOPIC_M2_RESPONSE = "vm-command-response/+";

        public const string TOPIC_REQUEST = "devices/{0}/request";
        public const string TOPIC_DEVICE_RESPONSE = "devices/{0}/response";

        public const string TOPIC_WAKEUP_REQUEST = "wakeup-req/{0}";
        public const string TOPIC_WAKEUP_RESPONSE = "wakeup-resp/+";

        public const string TOPIC_ACC_STATE_RESPONSE = "acc-state-notification/+";

        public const string TOPIC_EVENT = "devices/{0}/event"; // {groupdcode}/{server_index}
        public const string TOPIC_TELEMETRY = "devices/{0}/telemetry"; // {groupdcode}/{server_index}
        public const string TOPIC_ALARM = "devices/{0}/alarm"; // {groupdcode}/{server_index}

        public const string TOPIC_RESPONSE = "devices/+/response";

        public const string TOPIC_TYPE_REQUEST = "request";
        public const string TOPIC_TYPE_RESPONE = "response";
        public const string TOPIC_TYPE_TELEMETRY = "telemetry";
        public const string TOPIC_TYPE_ALARM = "alarm";
        public const string TOPIC_TYPE_EVENT = "event";
    }
}
