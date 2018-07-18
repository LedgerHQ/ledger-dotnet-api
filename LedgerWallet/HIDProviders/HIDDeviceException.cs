using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerWallet.HIDProviders
{
    public class HIDDeviceException : Exception
    {
        public HIDDeviceException(string message, Exception inner) : base(message, inner)
        {

        }
    }
}
