using Hid.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
	public class HIDDeviceTransportRegistry<T> where T : HIDTransportBase
	{
		Func<string, IHidDevice, T> create;
		public HIDDeviceTransportRegistry(Func<string, IHidDevice, T> create)
		{
			this.create = create;
		}


		public async Task<IEnumerable<T>> GetHIDTransportsAsync(IEnumerable<VendorProductIds> ids, params UsageSpecification[] acceptedUsages)
		{
			var devices = HIDTransportBase.EnumerateHIDDevices(ids, acceptedUsages)
							.Select(d => GetTransportAsync(d))
							.ToList();
            await Task.WhenAll(devices);
            return devices.Select(d => d.GetAwaiter().GetResult());
		}


		Dictionary<string, T> _TransportsByDevicePath = new Dictionary<string, T>();
        protected SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);

        private async Task<T> GetTransportAsync(DeviceInformation device)
		{
            await _Lock.WaitAsync();

            try
            {
                T transport = null;
                var uniqueId = string.Format("[{0},{1}]{2}", device.VendorId, device.ProductId, device.DevicePath);
                if(_TransportsByDevicePath.TryGetValue(uniqueId, out transport))
                    return transport;
                var windowsHidDevice = new WindowsHidDevice(device);
                if(!windowsHidDevice.IsInitialized)
                    await windowsHidDevice.InitializeAsync();
                transport = create(device.DevicePath, windowsHidDevice);
                _TransportsByDevicePath.Add(uniqueId, transport);
                return transport;
            }
            finally
            {
                _Lock.Release();
            }
        }
	}
}
