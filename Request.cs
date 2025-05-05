namespace VmlMQTT.Auth.Api.Models
{
    public class MqttStartSessionRequest
    {
        public string Phone { get; set; }
        public int UserId { get; set; }
        public string DeviceInfo { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class ReleaseSessionRequest
    {
        public string UserName { get; set; }
    }
    
    public class UpdateSessionRequest
    {
        public string UserName { get; set; }

        public string AccessToken { get; set; }
    }
    
    public class NotificationRequest
    {
        public string To { get; set; }

        public string MessageType { get; set; }

        public string Title { get; set; }
        
        public string Body { get; set; }

        public string ImageUrl { get; set; }
    }
}
