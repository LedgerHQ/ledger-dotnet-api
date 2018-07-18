using Hid.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
    public class UsageSpecification
    {
        public UsageSpecification(ushort usagePage, ushort usage)
        {
            UsagePage = usagePage;
            Usage = usage;
        }

        public ushort Usage
        {
            get;
            private set;
        }
        public ushort UsagePage
        {
            get;
            private set;
        }
    }
    public abstract class HIDTransportBase : ILedgerTransport
    {
        internal IHidDevice _Device;
		readonly string _DevicePath;
        readonly VendorProductIds _VendorProductIds;

        protected HIDTransportBase(string devicePath, IHidDevice device, UsageSpecification[] acceptedUsageSpecifications)
        {
            _DevicePath = devicePath;
            _Device = device;

            _VendorProductIds = new VendorProductIds(device.VendorId, device.ProductId);
            _AcceptedUsageSpecifications = acceptedUsageSpecifications;
        }

        UsageSpecification[] _AcceptedUsageSpecifications;

        bool needInit = true;
		public string DevicePath
		{
			get
			{
				return _DevicePath;
			}
		}

        protected SemaphoreSlim _SemaphoreSlim = new SemaphoreSlim(1, 1);
        bool initializing = false;
        public async Task<byte[][]> Exchange(byte[][] apdus, CancellationToken cancellation)
        {
            if(needInit && !initializing)
            {
                initializing = true;
                await InitAsync(cancellation);
                needInit = false;
                initializing = false;
            }
            var response = await ExchangeCoreAsync(apdus, cancellation).ConfigureAwait(false);

            if(response == null)
            {
				if(!await RenewTransportAsync(cancellation))
				{
					throw new LedgerWalletException("Ledger disconnected");
				}
                response = await ExchangeCoreAsync(apdus, cancellation).ConfigureAwait(false);
                if(response == null)
                    throw new LedgerWalletException("Error while transmission");
            }

            return response;
        }

		async Task<bool> RenewTransportAsync(CancellationToken cancellation)
		{
			var newDevice = EnumerateHIDDevices(new[]
			{
				this._VendorProductIds
			}, _AcceptedUsageSpecifications)
			.FirstOrDefault(hid => hid.DevicePath == _DevicePath);
			if(newDevice == null)
				return false;
			_Device = new WindowsHidDevice(newDevice);
			if(!await _Device.GetIsConnectedAsync())
			{
				var windowsHidDevice = _Device as WindowsHidDevice;
				await windowsHidDevice.InitializeAsync().WithCancellation(cancellation);
			}
			await InitAsync(cancellation);
			return true;
		}

        protected virtual Task InitAsync(CancellationToken cancellation)
        {
#if(NETSTANDARD2_0)
            return Task.CompletedTask;
#else
            return Task.FromResult<bool>(true);
#endif
        }

		internal static IEnumerable<DeviceInformation> EnumerateHIDDevices(IEnumerable<VendorProductIds> vendorProductIds, params UsageSpecification[] acceptedUsages)
		{
			List<DeviceInformation> devices = new List<DeviceInformation>();

			var collection = WindowsHidDevice.GetConnectedDeviceInformations();

			foreach(var ids in vendorProductIds)
			{
				if(ids.ProductId == null)
					devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId));
				else
					devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId && c.ProductId == ids.ProductId));

			}
			var retVal = devices
				.Where(d =>
				acceptedUsages == null ||
				acceptedUsages.Length == 0 ||
				acceptedUsages.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage)).ToList();

			return retVal;
		}


        const uint MAX_BLOCK = 64;
        const int DEFAULT_TIMEOUT = 20000;

        internal async Task<byte[][]> ExchangeCoreAsync(byte[][] apdus, CancellationToken cancellation)
        {
            if(apdus == null || apdus.Length == 0)
                return null;
            List<byte[]> resultList = new List<byte[]>();
            var lastAPDU = apdus.Last();

            await _SemaphoreSlim.WaitAsync();

            try
            {
                foreach(var apdu in apdus)
                {
                    await WriteAsync(apdu);
                    var result = await ReadAsync(cancellation);
                    if(result == null)
                        return null;
                    resultList.Add(result);
                }
            }
            finally
            {
                _SemaphoreSlim.Release();
            }

            return resultList.ToArray();
        }

        protected async Task<byte[]> ReadAsync(CancellationToken cancellation)
        {
            byte[] packet = new byte[MAX_BLOCK];
            MemoryStream response = new MemoryStream();
            int remaining = 0;
            int sequenceIdx = 0;
            do
            {
                var result = await hid_read_timeout(packet, MAX_BLOCK, cancellation);
                if(result < 0)
                    return null;
                var commandPart = UnwrapReponseAPDU(packet, ref sequenceIdx, ref remaining);
                if(commandPart == null)
                    return null;
                response.Write(commandPart, 0, commandPart.Length);
            } while(remaining != 0);

            return response.ToArray();
        }

        protected async Task<byte[]> WriteAsync(byte[] apdu)
        {
            int sequenceIdx = 0;
            byte[] packet = null;
            var apduStream = new MemoryStream(apdu);
            do
            {
                packet = WrapCommandAPDU(apduStream, ref sequenceIdx);
                await hid_write(packet, packet.Length);
            } while(apduStream.Position != apduStream.Length);
            return packet;
        }

        protected abstract byte[] UnwrapReponseAPDU(byte[] packet, ref int sequenceIdx, ref int remaining);

        protected abstract byte[] WrapCommandAPDU(Stream apduStream, ref int sequenceIdx);

        private async Task<int> hid_read_timeout(byte[] buffer, uint offset, uint length, CancellationToken cancellation)
        {
            var bytes = new byte[length];
            Array.Copy(buffer, offset, bytes, 0, length);
            var result = await hid_read_timeout(bytes, length, cancellation);
            Array.Copy(bytes, 0, buffer, offset, length);
            return result;
        }

        internal async Task<int> hid_read_timeout(byte[] buffer, uint length, CancellationToken cancellation)
        {
            //Note: this method used to read 64 bytes and shift that right in to an array of 65 bytes
            //Android does this automatically, so, we can't do this here for compatibility with Android.
            //See the flag DataHasExtraByte on WindowsHidDevice and UWPHidDevice

            var result = await _Device.ReadAsync().WithCancellation(cancellation);
            Array.Copy(result, 0, buffer, 0, length);
            return result.Length;
        }



        internal async Task<int> hid_write(byte[] buffer, int length)
        {
            //Note: this method used to take 64 bytes and shift that right in to an array of 65 bytes and then write to the device
            //Android does this automatically, so, we can't do this here for compatibility with Android.
            //See the flag DataHasExtraByte on WindowsHidDevice and UWPHidDevice

            await _Device.WriteAsync(buffer);
            return length;
        }
    }
}
