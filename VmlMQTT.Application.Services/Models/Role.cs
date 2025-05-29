namespace VmlMQTT.Application.Models
{
    public class EMQXRole
    {
        public string username { get; set; }

        public IList<AccessRight> rules { get; set; }
    }

    public class AccessRight
    {
        public string action { get; set; }

        public string permission { get; set; }

        public string topic { get; set; }
    }
}
