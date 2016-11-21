using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
	public class HIDDeviceTransportRegistry<T> where T : HIDTransportBase
	{
		Func<HidDevice, T> create;
		public HIDDeviceTransportRegistry(Func<HidDevice, T> create)
		{
			this.create = create;
		}
		public unsafe IEnumerable<T> GetHIDTransports(IEnumerable<VendorProductIds> ids, params UsageSpecification[] acceptedUsages)
		{
			return HIDTransportBase.EnumerateHIDDevices(ids, acceptedUsages)
							.Select(d => GetTransport(d))
							.ToList();
		}

		Dictionary<string, T> _TransportsByDevicePath = new Dictionary<string, T>();
		private T GetTransport(HidDevice device)
		{
			lock(_TransportsByDevicePath)
			{
				T transport = null;
				var uniqueId = string.Format("[{0},{1}]{2}", device.Attributes.VendorId, device.Attributes.ProductId, device.DevicePath);
				if(_TransportsByDevicePath.TryGetValue(uniqueId, out transport))
					return transport;
				transport = create(device);
				_TransportsByDevicePath.Add(uniqueId, transport);
				return transport;
			}
		}
	}
}
