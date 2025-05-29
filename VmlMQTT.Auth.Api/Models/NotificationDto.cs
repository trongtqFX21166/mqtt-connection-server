namespace VmlMQTT.Auth.Api.Models
{
    public class NotificationDto
    {
        public string To { get; set; }

        public string MessageType { get; set; }

        public string Title { get; set; }

        public string Body { get; set; }

        public string ImageUrl { get; set; }

        public string ActionUrl { get; set; }

        public string Icon { get; set; }
    }
}
