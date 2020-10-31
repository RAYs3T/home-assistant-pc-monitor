using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MQTTnet.Client;

namespace MqttPcHeartbeatMonitor
{
    public class Worker : BackgroundService
    {
        private static IMqttService _mqttService;

        private static readonly string IdleTopic = $"{Environment.MachineName}/idleStatus";
        private static readonly string WsLockedTopic = $"{Environment.MachineName}/workstationLocked";

        private static bool _lastIdleState;

        private static IMqttClient _mqttClient;
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger, IMqttService mqttService)
        {
            _logger = logger;
            _mqttService = mqttService;

            SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;

            InitTopicsWithDefaultValues();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);
                while (!stoppingToken.IsCancellationRequested)
                {
                    await reconnectWhenDisconnected();
                    await CheckAndPublishIdleState();
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

        private static async Task CheckAndPublishIdleState()
        {
            var currentIdleState = GetLastUserInput.IsIdle();
            if (currentIdleState != _lastIdleState)
            {
                // Update idle topic only when it has changed
                await _mqttService.Publish(_mqttClient, currentIdleState.ToString(), IdleTopic);
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
            _mqttService.Publish(_mqttClient, wsLocked.ToString(), WsLockedTopic);
        }

        /**
         * Should be called after starting, when the values are now known yet.
         * We want to announce all topics, so you can see them in Home Assistant.
         */
        private static void InitTopicsWithDefaultValues()
        {
            _mqttService.Publish(_mqttClient, "unknown", IdleTopic);
            _mqttService.Publish(_mqttClient, "unknown", WsLockedTopic);
        }
    }
}