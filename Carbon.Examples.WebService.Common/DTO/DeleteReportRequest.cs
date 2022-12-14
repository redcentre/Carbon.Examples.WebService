namespace Carbon.Examples.WebService.Common
{
    public sealed class DeleteReportRequest
    {
        public DeleteReportRequest()
        {
        }

        public DeleteReportRequest(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
