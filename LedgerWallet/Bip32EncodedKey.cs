using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet
{
    public class Bip32EncodedKey
    {
        byte[] _Key;
        public Bip32EncodedKey(byte[] bytes)
        {
            if(bytes == null)
                throw new ArgumentNullException("bytes");
            _Key = bytes.ToArray();
        }

        public byte[] ToBytes()
        {
            return _Key.ToArray();
        }

        public string ToHex()
        {
            return Encoders.Hex.EncodeData(_Key);
        }
    }
}
