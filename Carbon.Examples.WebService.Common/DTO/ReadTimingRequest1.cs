namespace Carbon.Examples.WebService.Common
{
    public sealed class ReadTimingRequest1
	{
        public ReadTimingRequest1(string azconnect, string container, string names)
        {
			AzConnect = azconnect;
			Container = container;
			Names = names;
        }

        public string AzConnect { get; set; }

        public string Container { get; set; }

        public string Names { get; set; }
    }
}
