using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{

    /// <summary>
    /// 3DES-2 private key
    /// </summary>
    public class LedgerKey
    {
        byte[] _Key;
        public LedgerKey(string hex):this(HexEncoder.Instance.DecodeData(hex))
        {
        }
        public LedgerKey(byte[] bytes)
        {
            if(bytes.Length != 16)
                throw new FormatException("Invalid byte count");
            _Key = bytes.ToArray();
        }

        public byte[] ToBytes()
        {
            return _Key.ToArray();
        }

        public string ToHex()
        {
            return HexEncoder.Instance.EncodeData(_Key, 0, 16);
        }
    }
}
