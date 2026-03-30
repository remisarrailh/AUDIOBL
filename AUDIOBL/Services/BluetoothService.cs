using System.Runtime.InteropServices;
using System.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace AUDIOBL.Services;

public class BluetoothService : IDisposable
{
    private readonly string _filter; // empty = any connected device
    private DeviceWatcher? _watcher;
    private Timer? _pollTimer;
    private string? _deviceId;
    private string  _connectedName = "";
    private ulong   _bleAddress;
    private bool    _disposed;

    public event Action<int?>? BatteryLevelChanged;
    public event Action<bool>? DeviceConnectionChanged;

    private const string PropIsConnected = "System.Devices.Aep.IsConnected";
    private const string PropIsPaired    = "System.Devices.Aep.IsPaired";

    private static readonly string[] RequestedProperties =
        [PropIsConnected, PropIsPaired, "System.ItemNameDisplay"];

    public BluetoothService(string deviceNameFilter = "")
    {
        _filter = deviceNameFilter;
    }

    public async Task StartAsync()
    {
        string selector = BluetoothDevice.GetDeviceSelector();
        try
        {
            _watcher = DeviceInformation.CreateWatcher(
                selector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            _watcher.Added   += OnDeviceAdded;
            _watcher.Updated += OnDeviceUpdated;
            _watcher.Removed += OnDeviceRemoved;
            _watcher.Start();
        }
        catch { _watcher = null; }

        _pollTimer = new Timer(_ => _ = PollAsync(), null,
            TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(30));

        await PollAsync();
    }

    private bool IsTargetDevice(string name)
        => string.IsNullOrEmpty(_filter)
           || name.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation info)
    {
        if (!IsTargetDevice(info.Name)) return;
        _deviceId = info.Id;
        _connectedName = info.Name;
        bool connected = ReadConnected(info.Properties);
        BatteryLevelChanged?.Invoke(null);
        DeviceConnectionChanged?.Invoke(connected || true);
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_deviceId != null && update.Id != _deviceId) return;
        if (update.Properties.TryGetValue(PropIsConnected, out var conn) && conn is bool c)
        {
            DeviceConnectionChanged?.Invoke(c);
            if (c)
                _ = ReadGattBatteryFromDeviceId(_deviceId!).ContinueWith(t =>
                    { if (t.Result.HasValue) BatteryLevelChanged?.Invoke(t.Result); },
                    TaskContinuationOptions.OnlyOnRanToCompletion);
            else
                BatteryLevelChanged?.Invoke(null);
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (update.Id != _deviceId) return;
        _deviceId = null;
        _connectedName = "";
        DeviceConnectionChanged?.Invoke(false);
        BatteryLevelChanged?.Invoke(null);
    }

    private async Task PollAsync()
    {
        try
        {
            string selector = BluetoothDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(
                selector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);

            foreach (var d in devices)
            {
                if (!IsTargetDevice(d.Name)) continue;
                _deviceId      = d.Id;
                _connectedName = d.Name;
                bool connected = ReadConnected(d.Properties);
                DeviceConnectionChanged?.Invoke(connected);

                if (connected)
                {
                    var battery = await ReadGattBatteryFromDeviceId(d.Id);
                    if (battery.HasValue) BatteryLevelChanged?.Invoke(battery);
                }
                else BatteryLevelChanged?.Invoke(null);
                return;
            }

            // No matching device found
            DeviceConnectionChanged?.Invoke(false);
            BatteryLevelChanged?.Invoke(null);
        }
        catch { FallbackWin32Poll(); }
    }

    private static bool ReadConnected(IReadOnlyDictionary<string, object> props)
        => props.TryGetValue(PropIsConnected, out var v) && v is true;

    // ── GATT Battery Service ──────────────────────────────────────────────

    /// <summary>
    /// Gets BT address from Classic BT device id, then tries GATT battery.
    /// Fallback: BLE advertisement scan using the device name.
    /// Works with any headphone that supports GATT Battery Service (UUID 0x180F).
    /// </summary>
    private async Task<int?> ReadGattBatteryFromDeviceId(string deviceId)
    {
        // Strategy 1: Classic BT address → BLE (most headphones share MAC address)
        try
        {
            using var btDev = await BluetoothDevice.FromIdAsync(deviceId);
            if (btDev != null)
            {
                var result = await TryGattFromAddress(btDev.BluetoothAddress);
                if (result.HasValue)
                {
                    _bleAddress = btDev.BluetoothAddress;
                    return result;
                }
            }
        }
        catch { }

        // Strategy 2: cached BLE address from previous scan
        if (_bleAddress != 0)
        {
            var result = await TryGattFromAddress(_bleAddress);
            if (result.HasValue) return result;
            _bleAddress = 0; // stale, reset
        }

        // Strategy 3: passive BLE advertisement scan (fallback for split-address devices)
        if (!string.IsNullOrEmpty(_connectedName))
        {
            ulong addr = await ScanBleAddressAsync(_connectedName);
            if (addr != 0)
            {
                var result = await TryGattFromAddress(addr);
                if (result.HasValue) { _bleAddress = addr; return result; }
            }
        }

        return null;
    }

    private static async Task<int?> TryGattFromAddress(ulong address)
    {
        try
        {
            using var le = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
            if (le == null) return null;

            var svcResult = await le.GetGattServicesForUuidAsync(
                GattServiceUuids.Battery, BluetoothCacheMode.Uncached);
            if (svcResult.Status != GattCommunicationStatus.Success || svcResult.Services.Count == 0)
            {
                foreach (var s in svcResult.Services) s.Dispose();
                return null;
            }

            using var svc = svcResult.Services[0];
            var charResult = await svc.GetCharacteristicsForUuidAsync(
                GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached);
            if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
                return null;

            var read = await charResult.Characteristics[0].ReadValueAsync(BluetoothCacheMode.Uncached);
            if (read.Status != GattCommunicationStatus.Success) return null;

            using var reader = DataReader.FromBuffer(read.Value);
            return reader.ReadByte();
        }
        catch { return null; }
    }

    private static async Task<ulong> ScanBleAddressAsync(string nameHint, int timeoutMs = 8000)
    {
        var tcs = new TaskCompletionSource<ulong>();
        var watcher = new BluetoothLEAdvertisementWatcher
            { ScanningMode = BluetoothLEScanningMode.Passive };

        // Match on any fragment of the device name
        string[] fragments = nameHint.Split(' ', '-', '_')
            .Where(f => f.Length >= 3).ToArray();

        watcher.Received += (w, args) =>
        {
            string local = args.Advertisement.LocalName ?? "";
            if (fragments.Any(f => local.Contains(f, StringComparison.OrdinalIgnoreCase))
                || local.Contains(nameHint, StringComparison.OrdinalIgnoreCase))
            {
                tcs.TrySetResult(args.BluetoothAddress);
                w.Stop();
            }
        };

        watcher.Start();
        await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        watcher.Stop();
        return tcs.Task.IsCompletedSuccessfully ? tcs.Task.Result : 0;
    }

    // ── Win32 fallback ────────────────────────────────────────────────────

    private void FallbackWin32Poll()
    {
        var info = Win32FindDevice(_filter);
        if (info == null) { DeviceConnectionChanged?.Invoke(false); return; }
        DeviceConnectionChanged?.Invoke(info.Value.fConnected);
        if (!info.Value.fConnected) BatteryLevelChanged?.Invoke(null);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
        public long _s1, _s2, _u1, _u2;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnAuthenticated;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnRemembered;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnUnknown;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnConnected;
        [MarshalAs(UnmanagedType.Bool)] public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public nint hRadio;
    }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern nint BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS p, ref BLUETOOTH_DEVICE_INFO i);
    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextDevice(nint h, ref BLUETOOTH_DEVICE_INFO i);
    [DllImport("bthprops.cpl")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindDeviceClose(nint h);

    private static BLUETOOTH_DEVICE_INFO? Win32FindDevice(string fragment)
    {
        var info = new BLUETOOTH_DEVICE_INFO { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>() };
        var search = new BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
            fReturnAuthenticated = true, fReturnRemembered = true, fReturnConnected = true
        };
        var hFind = BluetoothFindFirstDevice(ref search, ref info);
        if (hFind == nint.Zero) return null;
        try
        {
            do
            {
                bool match = string.IsNullOrEmpty(fragment)
                    || info.szName.Contains(fragment, StringComparison.OrdinalIgnoreCase);
                if (match) return info;
            }
            while (BluetoothFindNextDevice(hFind, ref info));
        }
        finally { BluetoothFindDeviceClose(hFind); }
        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer?.Dispose();
        if (_watcher != null)
        {
            _watcher.Added   -= OnDeviceAdded;
            _watcher.Updated -= OnDeviceUpdated;
            _watcher.Removed -= OnDeviceRemoved;
            if (_watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                _watcher.Stop();
        }
    }
}
