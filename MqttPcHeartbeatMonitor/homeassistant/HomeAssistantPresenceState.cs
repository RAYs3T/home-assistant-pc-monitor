using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class HomeAssistantPresenceState
    {
        [JsonProperty("state")] public bool Presence { get; set; }
    }
}