using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{
    class BufferUtils
    {
        internal static void writeUint32BE(System.IO.MemoryStream data, long index)
        {
            var bytes = Utils.ToBytes((uint)index, false);
            data.Write(bytes, 0, bytes.Length);
        }

        internal static void writeBuffer(System.IO.MemoryStream data, byte[] p)
        {
            throw new NotImplementedException();
        }

        internal static void writeBuffer(System.IO.MemoryStream data, IBitcoinSerializable serializable)
        {
            writeBuffer(data, serializable.ToBytes());
        }

        internal static void writeBuffer(System.IO.MemoryStream data, uint v)
        {
            var bytes = Utils.ToBytes(v, true);
            data.Write(bytes, 0, bytes.Length);
        }
    }
}
