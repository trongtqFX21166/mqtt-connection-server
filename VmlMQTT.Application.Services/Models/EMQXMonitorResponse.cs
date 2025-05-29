using Newtonsoft.Json;

namespace VmlMQTT.Application.Models
{
    public class EMQXMonitorResponse
    {
        public int Dropped { get; set; }
        public int Sent { get; set; }
        public int Connections { get; set; }
        public int Subscriptions { get; set; }
        public int Topics { get; set; }
        public int Persisted { get; set; }
        public int Received { get; set; }
        [JsonProperty("live_connections")]
        public int LiveConnections { get; set; }
        [JsonProperty("time_stamp")]
        public long TimeStamp { get; set; }
        [JsonProperty("disconnected_durable_sessions")]
        public int DisconnectedDurableSessions { get; set; }
        [JsonProperty("subscriptions_durable")]
        public int SubscriptionsDurable { get; set; }
        [JsonProperty("transformation_failed")]
        public int TransformationFailed { get; set; }
        [JsonProperty("transformation_succeeded")]
        public int TransformationSucceeded { get; set; }
        [JsonProperty("validation_failed")]
        public int ValidationFailed { get; set; }
        [JsonProperty("validation_succeeded")]
        public int ValidationSucceeded { get; set; }
        [JsonProperty("subscriptions_ram")]
        public int SubscriptionsRam { get; set; }

        public IDictionary<string, int> GetDictionaryByListKey(string[] keys)
        {
            var dictionary = new Dictionary<string, int>();
            foreach (var key in keys)
            {
                var propertyInfo = GetType().GetProperty(key);
                if (propertyInfo != null)
                {
                    var value = propertyInfo.GetValue(this);
                    if (value is int intValue)
                    {
                        dictionary[key] = intValue;
                    }
                }
            }
            return dictionary;
        }
    }
}
