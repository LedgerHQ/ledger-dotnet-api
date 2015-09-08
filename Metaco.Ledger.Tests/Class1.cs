using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Metaco.Ledger.Tests
{
	public class Class1
	{
		[Fact]
		public void Test()
		{
            LedgerClient.GetLedgers();
		}
	}
}
