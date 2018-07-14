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

#if(!NETSTANDARD2_0)
		public unsafe IEnumerable<T> GetHIDTransports(IEnumerable<VendorProductIds> ids, params UsageSpecification[] acceptedUsages)
		{
			return HIDTransportBase.EnumerateIHidDevices(ids, acceptedUsages)
							.Select(d => GetTransport(d))
							.ToList();
		}
#endif

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
