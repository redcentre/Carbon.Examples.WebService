using System;
using RCS.Carbon.Shared;

namespace Carbon.Examples.WebService.Common
{
    public sealed class OpenCloudJobResponse
    {
#pragma warning disable CS8618     // Empty ctor required for JSON serialization                                                   
        public OpenCloudJobResponse()
        {
        }
#pragma warning restore CS8618

        public OpenCloudJobResponse(XDisplayProperties? dprops, string[]? vartreeNames, string[]? axisTreeNames, GenNode[]? toc, GenNode[]? jobIni)
        {
            DProps = dprops;
            VartreeNames = vartreeNames;
            AxisTreeNames = axisTreeNames;
            Toc = toc;
            JobIni = jobIni;
        }

        public XDisplayProperties? DProps { get; set; }
        public string[]? VartreeNames { get; set; }
        public string[]? AxisTreeNames { get; set; }
        public GenNode[]? Toc { get; set; }
        public GenNode[]? JobIni { get; set; }

        public override string ToString() => $"({DProps},{VartreeNames?.Length},{AxisTreeNames?.Length},{Toc?.Length},{JobIni?.Length})";
    }
}
