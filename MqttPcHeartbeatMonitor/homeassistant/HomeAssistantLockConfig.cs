using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class HomeAssistantLockConfig
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("device_class")] public string DeviceClass { get; set; }
        [JsonProperty("state_topic")] public string StateTopic { get; set; }

        [JsonProperty("device")] public HomeAssistantDevice Device { get; set; }

        [JsonProperty("unique_id")] public string UniqueId { get; set; }
    }
}