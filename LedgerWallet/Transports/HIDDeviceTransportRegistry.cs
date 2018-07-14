using Hid.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
	public class IHidDeviceTransportRegistry<T> where T : HIDTransportBase
	{
		Func<IHidDevice, T> create;
		public IHidDeviceTransportRegistry(Func<IHidDevice, T> create)
		{
			this.create = create;
		}

		public
#if(!NETSTANDARD2_0)
			unsafe 
#endif
			IEnumerable<T> GetHIDTransports(IEnumerable<VendorProductIds> ids, params UsageSpecification[] acceptedUsages)
		{
			return EnumerateIHidDevices(ids, acceptedUsages)
							.Select(d => GetTransport(d))
							.ToList();
		}

		internal static
#if(!NETSTANDARD2_0)
			unsafe 
#endif
			IEnumerable<DeviceInformation> EnumerateIHidDevices(IEnumerable<VendorProductIds> vendorProductIds, params UsageSpecification[] acceptedUsages)
		{
			List<DeviceInformation> devices = new List<DeviceInformation>();

			var collection = WindowsHidDevice.GetConnectedDeviceInformations();
			foreach(var vendorProductId in vendorProductIds)
			{
				devices.AddRange(collection.Where(d => d.VendorId == vendorProductId.VendorId && d.ProductId == vendorProductId.ProductId));
			}

			return devices
				.Where(d =>
				acceptedUsages == null ||
				acceptedUsages.Length == 0 ||
				acceptedUsages.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage));
		}

		Dictionary<string, T> _TransportsByDevicePath = new Dictionary<string, T>();
		private T GetTransport(DeviceInformation device)
		{
			lock(_TransportsByDevicePath)
			{
				T transport = null;
				var uniqueId = string.Format("[{0},{1}]", device.VendorId, device.ProductId);
				if(_TransportsByDevicePath.TryGetValue(uniqueId, out transport))
					return transport;
				var windowsHidDevice = new WindowsHidDevice(device);
				transport = create(windowsHidDevice);
				_TransportsByDevicePath.Add(uniqueId, transport);
				return transport;
			}
		}
	}
}
