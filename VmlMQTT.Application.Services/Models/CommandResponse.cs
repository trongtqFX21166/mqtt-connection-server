namespace VmlMQTT.Application.Models
{
    public class CommandResponse
    {
        public string ProNo { get; set; }
        public string DeviceImei { get; set; }
        public bool OfflineFlag { get; set; }
        public string RequestId { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public object Data { get; set; }
    }
}
