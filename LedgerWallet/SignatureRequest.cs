using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet
{
	public class SignatureRequest
	{
		public ICoin InputCoin
		{
			get; set;
		}
		public Transaction InputTransaction
		{
			get; set;
		}
		public KeyPath KeyPath
		{
			get; set;
		}
		public PubKey PubKey
		{
			get; set;
		}
		public TransactionSignature Signature
		{
			get;
			set;
		}
	}
}
