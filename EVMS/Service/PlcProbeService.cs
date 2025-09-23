using ActUtlTypeLib;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

internal class PlcProbeService : IDisposable
{
    private ActUtlType plc;
    private OrbitService orbitService;
    private bool isConnected = false;
    private CancellationTokenSource? _cts;

    public PlcProbeService()
    {
        plc = new ActUtlType();
        orbitService = new OrbitService();
    }

    public bool IsConnected => isConnected && orbitService.IsConnected;

    // Event to notify subscribers about live probe value updates
    public event EventHandler<ProbeReadingEventArgs>? ProbeValueUpdated;

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

    // Method to get connected module IDs
    public List<string> GetConnectedProbeModuleIds()
    {
        return orbitService.GetConnectedModuleIds();
    }

    // Read a probe value for a specific module and channel
    public double? ReadProbeValue(string moduleId, int channel)
    {
        if (!orbitService.IsModuleConnected(moduleId))
            return null;

        try
        {
            dynamic module = orbitService.GetModuleById(moduleId);
            double value = module.ReadValue(channel);
            return value;
        }
        catch
        {
            return null;
        }
    }


    public void StartLiveReading(int channel = 0, int intervalMs = 1000)
    {
        
    }

    public void StopLiveReading()
    {
       
    }

    public void Dispose()
    {
        StopLiveReading();
        Disconnect();
        plc = null;
        orbitService.Dispose();
        orbitService = null;
    }
}

// Supporting probe reading event args class
public class ProbeReadingEventArgs : EventArgs
{
    public string ModuleId { get; set; }
    public double Value { get; set; }

    public ProbeReadingEventArgs(string moduleId, double value)
    {
        ModuleId = moduleId;
        Value = value;
    }
}
