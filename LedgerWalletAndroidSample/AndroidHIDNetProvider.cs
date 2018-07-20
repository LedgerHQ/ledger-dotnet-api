using Android.Content;
using Android.Hardware.Usb;
using Hid.Net.Android;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public class AndroidHIDNetProvider : IHIDProvider
    {
        public UsbManager UsbManager { get; private set; }
        public Context AndroidContext { get; private set; }
        public int TimeoutMilliseconds { get; private set; }
        public int ReadBufferLength { get; private set; }

        public AndroidHIDNetProvider(UsbManager usbManager, Context androidContext, int timeoutMilliseconds, int readBufferLength)
        {
            UsbManager = usbManager;
            AndroidContext = androidContext;
            TimeoutMilliseconds = timeoutMilliseconds;
            ReadBufferLength = readBufferLength;
        }

        public IHIDDevice CreateFromDescription(HIDDeviceInformation hidDevice)
        {
            var androidHidDevice = new AndroidHidDevice(UsbManager, AndroidContext, TimeoutMilliseconds, ReadBufferLength, hidDevice.VendorId, hidDevice.ProductId);

            return new AndroidHIDNetDevice(androidHidDevice);
        }

        public Task<IEnumerable<HIDDeviceInformation>> EnumerateDeviceDescriptions(IEnumerable<VendorProductIds> vendorProductIds, UsageSpecification [] acceptedUsages)
        {
            List<UsbDevice> devices = new List<UsbDevice>();

            var collection = UsbManager.DeviceList.Values;

            foreach (var ids in vendorProductIds)
            {
                if (ids.ProductId == null)
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId));
                else
                    devices.AddRange(collection.Where(c => c.VendorId == ids.VendorId && c.ProductId == ids.ProductId));

            }

            //TODO: Filter by usage page. It's not clear how to do this on Android
            //var retVal = devices
            //    .Where(d =>
            //    acceptedUsages == null ||
            //    acceptedUsages.Length == 0 ||
            //    acceptedUsages.Any(u => d.UsagePage == u.UsagePage && d.Usage == u.Usage)).ToList();

            return Task.FromResult<IEnumerable<HIDDeviceInformation>>(devices.Select(r => new HIDDeviceInformation()
            {
                ProductId = (ushort)r.ProductId,
                VendorId = (ushort)r.VendorId,
                DevicePath = r.DeviceId.ToString(),
                ProviderInformation = r
            }));
        }
    }
}
