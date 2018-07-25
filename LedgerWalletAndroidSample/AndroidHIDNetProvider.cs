using Hid.Net.Android;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public class AndroidHIDNetProvider : IHIDProvider
    {
        AndroidHidDevice AndroidHidDevice;

        public AndroidHIDNetProvider(AndroidHidDevice androidHidDevice)
        {
            AndroidHidDevice = androidHidDevice;
        }

        public IHIDDevice CreateFromDescription(HIDDeviceInformation hidDevice)
        {
            return new AndroidHIDNetDevice(AndroidHidDevice);
        }

        public async Task<IEnumerable<HIDDeviceInformation>> EnumerateDeviceDescriptions(IEnumerable<VendorProductIds> vendorProductIds, UsageSpecification [] acceptedUsages)
        {
            var retVal = new List<HIDDeviceInformation>();

            if (AndroidHidDevice != null)
            {
                retVal.Add(new HIDDeviceInformation { ProductId = (ushort)AndroidHidDevice.ProductId, VendorId = (ushort)AndroidHidDevice.VendorId });
            }

            return retVal;
        }
    }
}
