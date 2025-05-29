namespace VmlMQTT.HandleVmlEvent.Service.Model
{
    public class IOTHubResponse<T> where T : class
    {
        public int Code { get; set; }

        public string Msg { get; set; }

        public T Data { get; set; }

    }
}
