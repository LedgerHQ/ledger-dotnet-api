using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace BTChip
{
    class VarintUtils
    {
        internal static void write(System.IO.MemoryStream data, int p)
        {
            var b = new VarInt((ulong)p).ToBytes();
            data.Write(b, 0, b.Length);
        }
    }
}
