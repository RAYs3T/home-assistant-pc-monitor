using System.Collections.Generic;
using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class HomeAssistantDevice
    {
        [JsonProperty("identifiers")] public List<string> Identifiers;
        [JsonProperty("name")] public string Name;
        [JsonProperty("sw_version")] public string SwVersion;
    }
}