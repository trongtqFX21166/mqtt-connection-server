namespace VmlMQTT.Application.DTOs
{
    public class DeviceTokenDto
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public string AccessGrant { get; set; }
        public string RefreshGrant { get; set; }
        public string Imei { get; set; }
    }
}
