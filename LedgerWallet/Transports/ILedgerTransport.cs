using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet.Transports
{
	public interface ILedgerTransport
	{
		Task<byte[][]> Exchange(byte[][] apdus);
	}
}
