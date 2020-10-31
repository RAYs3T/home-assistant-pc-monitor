using System;
using System.Runtime.InteropServices;
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
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        private static IMqttService _mqttService;

        private static readonly string IdleTopic = $"{Environment.MachineName}/idleStatus";
        private static readonly string WsLockedTopic = $"{Environment.MachineName}/workstationLocked";

        private static bool lastIdleState;

        private static SessionSwitchEventHandler sseh;
        private static IMqttClient _mqttClient;
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger, IMqttService mqttService)
        {
            _logger = logger;
            _mqttService = mqttService;

            sseh = SystemEvents_SessionSwitch;
            SystemEvents.SessionSwitch += sseh;

            InitTopicsWithDefaultValues();
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

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
            if (currentIdleState != lastIdleState)
            {
                // Update idle topic only when it has changed
                await _mqttService.Publish(_mqttClient, currentIdleState.ToString(), IdleTopic);
                lastIdleState = currentIdleState;
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