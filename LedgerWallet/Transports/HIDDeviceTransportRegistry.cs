﻿using Hid.Net;
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
		public unsafe IEnumerable<T> GetHIDTransports(IEnumerable<VendorProductIds> ids, params UsageSpecification[] acceptedUsages)
		{
			return HIDTransportBase.EnumerateIHidDevices(ids, acceptedUsages)
							.Select(d => GetTransport(d))
							.ToList();
		}

		Dictionary<string, T> _TransportsByDevicePath = new Dictionary<string, T>();
		private T GetTransport(IHidDevice device)
		{
			lock(_TransportsByDevicePath)
			{
				T transport = null;
				var uniqueId = string.Format("[{0},{1}]", device.VendorId, device.ProductId);
				if(_TransportsByDevicePath.TryGetValue(uniqueId, out transport))
					return transport;
				transport = create(device);
				_TransportsByDevicePath.Add(uniqueId, transport);
				return transport;
			}
		}
	}
}
