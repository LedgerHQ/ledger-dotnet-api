using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Metaco.Ledger
{
    public class LedgerClient
    {
        public static IEnumerable<LedgerClient> GetLedgers()
        {
            using(var context = EstablishContext())
            {
                int size = 0;
                if(ModWinsCard.SCardListReaders(context.Handle, null, null, ref size) != 0)
                    throw new LedgerException("Error while requesting readers");
                byte[] result = new byte[size];
                if(ModWinsCard.SCardListReaders(context.Handle, null, result, ref size) != 0)
                    throw new LedgerException("Error while requesting readers");
                var readers = ToStrings(result, size);
                foreach(var reader in readers)
                {
                    int cardContext = 0;
                    int protocol = 0;
                    var err = ModWinsCard.SCardConnect(context.Handle, reader, ModWinsCard.SCARD_SHARE_DIRECT, 0, ref cardContext, ref protocol);
                    if(err != 0)
                        throw new LedgerException(err);
                    using(var cardCtx = new AnonymousHandle(cardContext, (i) => ModWinsCard.SCardReleaseContext(i.Handle)))
                    {
                        //PID : 0x1b7c
                     
                    }
                }
            }
            return null;
        }

        private static string[] ToStrings(byte[] mszReaders, int pcchReaders)
        {
            char nullchar = (char)0;
            int nullindex = -1;
            List<string> readersList = new List<string>();
            ASCIIEncoding ascii = new ASCIIEncoding();
            string currbuff = ascii.GetString(mszReaders);
            int len = pcchReaders;

            while(currbuff[0] != nullchar)
            {
                nullindex = currbuff.IndexOf(nullchar);   //get null end character
                string reader = currbuff.Substring(0, nullindex);
                readersList.Add(reader);
                len = len - (reader.Length + 1);
                currbuff = currbuff.Substring(nullindex + 1, len);
            }
            return readersList.ToArray();
        }

        private static AnonymousHandle EstablishContext()
        {
            int ctx = 0;
            int err = ModWinsCard.SCardEstablishContext(ModWinsCard.SCARD_SCOPE_USER, 0, 0, ref ctx);
            if(err != ModWinsCard.SCARD_S_SUCCESS)
                throw new LedgerException(err);
            return new AnonymousHandle(ctx, i => ModWinsCard.SCardReleaseContext(i.Handle));
        }

    }
}
