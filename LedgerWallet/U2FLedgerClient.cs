using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LedgerWallet.Transports;

namespace LedgerWallet
{
	public class U2FLedgerClient : LedgerClientBase
	{
		public U2FLedgerClient(ILedgerTransport transport) : base(transport)
		{
		}
	}
}
