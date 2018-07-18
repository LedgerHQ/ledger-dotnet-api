using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hid.Net;
using LedgerWallet.Transports;

namespace LedgerWallet.HIDProviders.HIDNet
{
    public class WindowsHIDNetDevice : HIDNetDevice
    {
        Hid.Net.WindowsHidDevice _Windows;
        public WindowsHIDNetDevice(Hid.Net.DeviceInformation deviceInfo) : base(deviceInfo, new Hid.Net.WindowsHidDevice(deviceInfo)
        {
            DataHasExtraByte = true
        })
        {
            _Windows = (Hid.Net.WindowsHidDevice)base._Device;
        }
        public override IHIDDevice Clone()
        {
            return new WindowsHIDNetDevice(_DeviceInformation);
        }

        public override async Task EnsureInitializedAsync(CancellationToken cancellation)
        {
            if(!_Windows.IsInitialized)
            {
                try
                {
                    await _Windows.InitializeAsync();
                }
                catch(Exception ex)
                {
                    throw new HIDDeviceException(ex.Message, ex);
                }
            }
        }
    }
}
