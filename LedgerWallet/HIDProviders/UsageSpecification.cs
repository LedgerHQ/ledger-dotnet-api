namespace LedgerWallet.HIDProviders
{
	public class UsageSpecification
    {
        public UsageSpecification(ushort usagePage, ushort usage)
        {
            UsagePage = usagePage;
            Usage = usage;
        }

        public ushort Usage
        {
            get;
            private set;
        }
        public ushort UsagePage
        {
            get;
            private set;
        }
    }
}
