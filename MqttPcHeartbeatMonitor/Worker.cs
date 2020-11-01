using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MQTTnet.Client;
using Newtonsoft.Json;

namespace MqttPcHeartbeatMonitor
{
    public class Worker : BackgroundService
    {
        private static IMqttService _mqttService;

        // https://www.home-assistant.io/docs/mqtt/discovery/
        // <discovery_prefix>/<component>/[<node_id>/]<object_id>/config
        private static readonly string UserPresenceConfigTopic =
            $"homeassistant/binary_sensor/{Environment.MachineName}-user-presence/config";

        private static readonly string UserPresenceTopic =
            $"homeassistant/binary_sensor/{Environment.MachineName}-user-presence/state";

        private static readonly string WsLockConfigTopic =
            $"homeassistant/binary_sensor/{Environment.MachineName}-workstation-lock/config";

        private static readonly string WsLockedTopic =
            $"homeassistant/binary_sensor/{Environment.MachineName}-workstation-lock/state";

        private static bool _lastIdleState;

        private static IMqttClient _mqttClient;
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger, IMqttService mqttService)
        {
            _logger = logger;
            _mqttService = mqttService;

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await reconnectWhenDisconnected();
            ConfigureHomeAssistantAutoDiscovery(Environment.MachineName);
            InitTopicsWithDefaultValues();
            try
            {
                _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);
                while (!stoppingToken.IsCancellationRequested)
                {
                    reconnectWhenDisconnected();
                    await CheckAndPublishPresenceState();
                    await Task.Delay(1000, stoppingToken);
                }

                await _mqttClient.DisconnectAsync();
                _logger.LogInformation("Worker stopped at {time}", DateTimeOffset.Now);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                if (e.InnerException != null)
                {
                    _logger.LogError(e.InnerException.Message);
                }

                throw;
            }
        }

        private static async Task CheckAndPublishPresenceState()
        {
            var currentIdleState = GetLastUserInput.IsIdle();
            if (currentIdleState != _lastIdleState)
            {
                // Update idle topic only when it has changed
                var presenceState = new HomeAssistantPresenceState()
                {
                    Presence = (!currentIdleState)
                };
                var presencePayload = JsonConvert.SerializeObject(presenceState);
                await _mqttService.Publish(_mqttClient, presencePayload, UserPresenceTopic);
                _lastIdleState = currentIdleState;
            }
        }

        private async Task reconnectWhenDisconnected()
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
            {
                _mqttClient = await _mqttService.Connect();
            }
        }

        /**
         * Called when the win logon session was switched
         */
        private static void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs switchEvent)
        {
            switch (switchEvent.Reason)
            {
                case SessionSwitchReason.SessionLock:
                    //desktop locked
                    PublishWsLockState(true);
                    break;
                case SessionSwitchReason.SessionUnlock:
                    //desktop unlocked
                    PublishWsLockState(false);
                    break;
            }
        }

        /**
         * Publishes the workstation lock state given
         */
        private static void PublishWsLockState(Boolean wsLocked)
        {
            var lockState = new HomeAssistantLockState()
            {
                Lock = wsLocked
            };
            var lockStatePayload = JsonConvert.SerializeObject(lockState);
            _mqttService.Publish(_mqttClient, lockStatePayload, WsLockedTopic);
        }

        /**
         * Should be called after starting, when the values are now known yet.
         * We want to announce all topics, so you can see them in Home Assistant.
         */
        private static void InitTopicsWithDefaultValues()
        {
            //_mqttService.Publish(_mqttClient, "false", UserPresenceTopic);
            //_mqttService.Publish(_mqttClient, "true", WsLockedTopic);
        }

        private static void ConfigureHomeAssistantAutoDiscovery(string computerName)
        {
            var deviceConfig = new HomeAssistantDevice
            {
                Name = Environment.MachineName,
                Identifiers = new List<string> {Environment.MachineName},
                SwVersion = Environment.OSVersion.ToString()
            };
            var haLockConfig = new HomeAssistantLockConfig
            {
                DeviceClass = "lock", Name = computerName + "_unlocked",
                StateTopic = WsLockedTopic,
                UniqueId = computerName + "_unlocked",
                Device = deviceConfig
            };
            var lockConfigPayload = JsonConvert.SerializeObject(haLockConfig);
            if (lockConfigPayload == null)
            {
                throw new Exception("Unable to build auto discovery config for _unlocked");
            }

            _mqttService.Publish(_mqttClient, lockConfigPayload, WsLockConfigTopic);


            var haUserPresenceConfig = new HomeAssistantLockConfig
            {
                DeviceClass = "presence", Name = computerName + "_presence",
                StateTopic = UserPresenceTopic,
                UniqueId = computerName + "_presence",
                Device = deviceConfig
            };
            var presenceConfigPayload = JsonConvert.SerializeObject(haUserPresenceConfig);
            if (presenceConfigPayload == null)
            {
                throw new Exception("Unable to build auto discovery config for _presence");
            }

            _mqttService.Publish(_mqttClient, presenceConfigPayload, UserPresenceConfigTopic);
        }
    }
}