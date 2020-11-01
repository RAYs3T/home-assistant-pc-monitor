using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class HomeAssistantUserActiveState
    {
        [JsonProperty("user_active")] public string State { get; set; }
    }
}