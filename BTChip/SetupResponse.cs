using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BTChip
{
    public class SetupResponse
    {
        public SetupResponse(byte[] bytes)
        {
            SeedTyped = bytes[0] == 1;
            if(bytes.Length == 33)
            {
                TrustedInputKey = bytes.Skip(1).Take(16).ToArray();
                KeyWrappingKey = bytes.Skip(1 + 16).Take(16).ToArray();
            }
        }

        public bool SeedTyped
        {
            get;
            set;
        }

        public byte[] KeyWrappingKey
        {
            get;
            set;
        }

        public byte[] TrustedInputKey
        {
            get;
            set;
        }
    }
}
