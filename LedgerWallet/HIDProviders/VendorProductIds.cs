using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerWallet.HIDProviders
{
    public class VendorProductIds
    {
        public VendorProductIds(int vendorId)
        {
            VendorId = vendorId;
        }
        public VendorProductIds(int vendorId, int? productId)
        {
            VendorId = vendorId;
            ProductId = productId;
        }
        public int VendorId
        {
            get; set;
        }
        public int? ProductId
        {
            get; set;
        }
    }
}
