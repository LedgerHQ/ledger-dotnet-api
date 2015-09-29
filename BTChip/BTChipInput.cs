using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{
    public class BTChipInput
    {
        private byte[] response;
        private bool p;

        public BTChipInput(byte[] response, bool p)
        {
            // TODO: Complete member initialization
            this.response = response;
            this.p = p;
        }
    }
}
