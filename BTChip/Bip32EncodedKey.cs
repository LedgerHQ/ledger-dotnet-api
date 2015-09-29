using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{
    public class Bip32EncodedKey
    {
        public Bip32EncodedKey(byte[] bytes)
        {
            var len = bytes[0];
            bytes.Skip(1).Take(len)
        }
    }
}
