namespace VmlMQTT.Auth.Api.Models
{
    public class MqttStartSessionResponse
    {
        public string AccessToken { get; set; }

        public string Host { get; set; }
        public int Port { get; set; }

        public List<string> PubTopics { get; set; }
        public List<string> SubTopics { get; set; }
    }
}
