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
        private static string _cpuId = Helpers.GetCpuId();
        private static string _uniqueMachineName = _cpuId + "_" + Environment.MachineName;

        // https://www.home-assistant.io/docs/mqtt/discovery/
        // <discovery_prefix>/<component>/[<node_id>/]<object_id>/config
        private static readonly string UserActiveConfigTopic =
            $"homeassistant/sensor/{_uniqueMachineName}/user_active/config";

        private static readonly string UserActiveTopic =
            $"win2mqtt/{_uniqueMachineName}/user_active/state";

        private static readonly string WorkstationLockedConfigTopic =
            $"homeassistant/sensor/{_uniqueMachineName}/workstation_locked/config";

        private static readonly string WorkstationLockedTopic =
            $"win2mqtt/{_uniqueMachineName}/workstation_locked/state";

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
            await ReconnectWhenDisconnected();
            ConfigureHomeAssistantAutoDiscovery(Environment.MachineName);
            InitTopicsWithDefaultValues();
            try
            {
                _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ReconnectWhenDisconnected();
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
                var haActiveState = new HomeAssistantUserActiveState()
                {
                    State = (!currentIdleState).ToString()
                };

                await _mqttService.Publish(_mqttClient, JsonConvert.SerializeObject(haActiveState), UserActiveTopic);
                _lastIdleState = currentIdleState;
            }
        }

        private static async Task ReconnectWhenDisconnected()
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
        private static void PublishWsLockState(bool workstationLocked)
        {
            var haWorkstationLockState = new HomeAssistantWorkstationLockState()
            {
                Locked = workstationLocked.ToString()
            };
            _mqttService.Publish(_mqttClient, JsonConvert.SerializeObject(haWorkstationLockState),
                WorkstationLockedTopic);
        }

        /**
         * Should be called after starting, when the values are now known yet.
         * We want to announce all topics, so you can see them in Home Assistant.
         */
        private static void InitTopicsWithDefaultValues()
        {
            //_mqttService.Publish(_mqttClient, "unknown", UserActiveTopic);
            //_mqttService.Publish(_mqttClient, "unknown", WorkstationLockedTopic);
        }

        private static void ConfigureHomeAssistantAutoDiscovery(string computerName)
        {
            var deviceConfig = new HomeAssistantDevice
            {
                Name = _cpuId + "." + Environment.MachineName + ".device",
                Identifiers = new List<string> {_cpuId + "_" + Environment.MachineName},
                SwVersion = Environment.OSVersion.ToString()
            };
            var haLockConfig = new HomeAssistantLockConfig
            {
                Name = _cpuId + "." + computerName + ".workstation_locked",
                StateTopic = WorkstationLockedTopic,
                UniqueId = _cpuId + "_" + computerName + "_locked",
                Device = deviceConfig,
                Icon = "hass:lock",
                ValueTemplate = "{{ value_json.locked }}"
            };
            var lockConfigPayload = JsonConvert.SerializeObject(haLockConfig);
            if (lockConfigPayload == null)
            {
                throw new Exception("Unable to build auto discovery config for _unlocked");
            }

            _mqttService.Publish(_mqttClient, lockConfigPayload, WorkstationLockedConfigTopic);


            var haUserActiveConfig = new HomeAssistantLockConfig
            {
                Name = _cpuId + "." + computerName + ".user_active",
                StateTopic = UserActiveTopic,
                UniqueId = _cpuId + "_" + computerName + "_active",
                Device = deviceConfig,
                Icon = "hass:motion",
                ValueTemplate = "{{ value_json.user_active }}"
            };
            var presenceConfigPayload = JsonConvert.SerializeObject(haUserActiveConfig);
            if (presenceConfigPayload == null)
            {
                throw new Exception("Unable to build auto discovery config for _presence");
            }

            _mqttService.Publish(_mqttClient, presenceConfigPayload, UserActiveConfigTopic);
        }
    }
}