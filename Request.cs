namespace VmlMQTT.Auth.Api.Models
{
    public class MqttStartSessionRequest
    {
        public int UserId { get; set; }

        public string DeviceInfo { get; set; }

        public string RefreshToken { get; set; }
    }

    public class ReleaseSessionRequest
    {
        public string RefreshToken
    }
}
