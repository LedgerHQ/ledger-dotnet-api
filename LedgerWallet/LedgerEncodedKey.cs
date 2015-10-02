using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet
{
    public class LedgerEncodedKey
    {
        byte[] _Key;
        public LedgerEncodedKey(byte[] key)
        {
            if(key == null)
                throw new ArgumentNullException("key");
            _Key = key.ToArray();
        }

        public byte[] ToBytes()
        {
            return _Key.ToArray();
        }

        public string ToHex()
        {
            return Encoders.Hex.EncodeData(_Key, 0, _Key.Length);
        }
    }
}
