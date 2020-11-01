using System;
using System.IO;
using System.Management;
using System.Reflection;

namespace MqttPcHeartbeatMonitor
{
    public static class Helpers
    {
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public static string MqttConfigJson
        {
            get
            {
                string filePath = Path.Combine(AssemblyDirectory, "config.json");

                if (File.Exists(filePath))
                {
                    using (var streamReader = new StreamReader(filePath))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public static string GetCpuId()
        {
            ManagementObjectCollection managementInstance;
            using (ManagementClass managClass = new ManagementClass("win32_processor"))
            {
                managementInstance = managClass.GetInstances();
            }

            foreach (var o in managementInstance)
            {
                var managementObject = (ManagementObject) o;
                return managementObject.Properties["processorID"].Value.ToString();
            }

            return null;
        }
    }
}