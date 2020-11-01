using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class HomeAssistantWorkstationLockState
    {
        [JsonProperty("locked")] public string Locked { get; set; }
    }
}