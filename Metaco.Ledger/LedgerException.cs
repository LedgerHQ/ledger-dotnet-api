using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{
    public class LedgerException : Exception
    {
        public LedgerException(int err)
            : base(ModWinsCard.GetScardErrMsg(err))
        {

        }

        public LedgerException(string message):base(message)
        {
        }
    }
}
