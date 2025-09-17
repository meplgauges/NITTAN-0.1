using Solartron.Orbit3;
using ActUtlTypeLib;
// For OrbitService
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVMS.Service
{
    internal class PlcProbeService : IDisposable
    {
        private ActUtlType plc;
        private OrbitService orbitService;
        private bool isConnected = false;

        public PlcProbeService()
        {
            plc = new ActUtlType();
            orbitService = new OrbitService();
        }

        public bool IsConnected => isConnected && orbitService.IsConnected;

        // Connect asynchronously to PLC and Orbit probes
        public async Task<bool> ConnectAsync()
        {
            bool plcConnected = await ConnectPlcAsync();
            bool orbitConnected = await orbitService.ConnectAsync();

            isConnected = plcConnected && orbitConnected;
            return isConnected;
        }

        private async Task<bool> ConnectPlcAsync()
        {
            plc.ActLogicalStationNumber = 1;

            var openTask = Task.Run(() =>
            {
                try
                {
                    return plc.Open(); // Blocking open call
                }
                catch
                {
                    return -1;
                }
            });

            var completedTask = await Task.WhenAny(openTask, Task.Delay(3000)); // 3 sec timeout

            if (completedTask == openTask)
            {
                int result = openTask.Result;
                return result == 0;
            }
            else
            {
                return false; // timeout
            }
        }

        public void Disconnect()
        {
            try
            {
                if (isConnected)
                {
                    plc.Close();
                    orbitService.Disconnect();
                }
            }
            catch
            {
                // Log or ignore exceptions
            }
            finally
            {
                isConnected = false;
            }
        }

        // Get list of connected probe module IDs from Orbit service
        public List<string> GetConnectedProbeModuleIds()
        {
            return orbitService.GetConnectedModuleIds();
        }

        // Read a probe value by module ID and channel (adapt to your API)
        public double? ReadProbeValue(string moduleId, int channel)
        {
            if (!orbitService.IsModuleConnected(moduleId))
                return null;

            try
            {
                dynamic module = orbitService.GetModuleById(moduleId);
                // Replace with actual API call per your probe hardware
                double value = module.ReadValue(channel);
                return value;
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            Disconnect();
            plc = null;
            orbitService.Dispose();
            orbitService = null;
        }
    }
}
