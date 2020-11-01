using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class HomeAssistantLockConfig
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("state_topic")] public string StateTopic { get; set; }

        [JsonProperty("json_attributes_topic")]
        public string JsonAttributesTopic { get; set; }

        [JsonProperty("device")] public HomeAssistantDevice Device { get; set; }

        [JsonProperty("unique_id")] public string UniqueId { get; set; }

        [JsonProperty("icon")] public string Icon { get; set; }

        [JsonProperty("value_template")] public string ValueTemplate { get; set; }
    }
}