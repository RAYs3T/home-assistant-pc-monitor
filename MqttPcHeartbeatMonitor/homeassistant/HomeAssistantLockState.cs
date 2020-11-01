using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class HomeAssistantLockState
    {
        [JsonProperty("state")] public bool Lock { get; set; }
    }
}