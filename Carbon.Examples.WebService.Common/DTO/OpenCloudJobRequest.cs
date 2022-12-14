namespace Carbon.Examples.WebService.Common
{
    public enum JobTocType
    {
        None,
        ExecUser,
        Simple
    }

    public sealed class OpenCloudJobRequest
    {
#pragma warning disable CS8618     // Empty ctor required for JSON serialization                                                   
        public OpenCloudJobRequest()
        {
        }
#pragma warning restore CS8618

        public OpenCloudJobRequest(string customerName, string jobName)
            : this(customerName, jobName, false, false, false, JobTocType.ExecUser, false, false)
        {
        }

        public OpenCloudJobRequest(string customerName, string jobName, bool getDisplayProps, bool getVarteeNames, bool getAxisTreeNames, JobTocType tocType, bool getTocOld, bool getIni)
        {
            CustomerName = customerName;
            JobName = jobName;
            GetDisplayProps = getDisplayProps;
            GetVartreeNames = getVarteeNames;
            GetAxisTreeNames = getAxisTreeNames;
            TocType = tocType;
            GetTocOld = getTocOld;
            GetIni = getIni;
        }

        public string CustomerName { get; set; }
        public string JobName { get; set; }
        public bool GetDisplayProps { get; set; }
        public bool GetVartreeNames { get; set; }
        public bool GetAxisTreeNames { get; set; }
        public JobTocType TocType { get; set; }
        public bool GetTocOld { get; set; }
        public bool GetIni { get; set; }
    }
}
