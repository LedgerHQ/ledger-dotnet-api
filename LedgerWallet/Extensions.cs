using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LedgerWallet
{
	public static class Extensions
	{
		public static Dictionary<TKey, TValue> ToDictionaryUnique<TKey, TValue>(this IEnumerable<TValue> v, Func<TValue, TKey> selectKey)
		{
			Dictionary<TKey, TValue> dico = new Dictionary<TKey, TValue>();
			foreach(var value in v)
			{
				var k = selectKey(value);
				if(!dico.ContainsKey(k))
					dico.Add(k, value);
			}
			return dico;
		}
	}
}
