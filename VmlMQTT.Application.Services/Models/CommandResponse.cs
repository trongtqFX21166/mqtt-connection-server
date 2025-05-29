namespace VmlMQTT.Application.Models
{
    public class CommandResponse
    {
        public string proNo { get; set; }
        public string deviceImei { get; set; }
        public bool offlineFlag { get; set; } = false;
        public string requestId { get; set; }
        public int code { get; set; }
        public string msg { get; set; }

        public DateTime ExpiredTime { get; set; }
    }
}
