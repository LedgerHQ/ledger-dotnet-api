using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Metaco.Ledger
{
    public class BTChipException : Exception
    {
        public BTChipException(string message)
            : base(message)
        {
        }
        public BTChipException(string message, int sw)
            : base(message)
        {

        }
    }
}
