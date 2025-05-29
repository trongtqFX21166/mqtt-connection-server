namespace VmlMQTT.Application.DTOs
{
    public class MqttStartSessionRequest
    {
        public string Phone { get; set; }
        public int UserId { get; set; }
        public string DeviceInfo { get; set; }
        public required string Imei { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}
