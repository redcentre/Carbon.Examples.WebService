﻿using RCS.Carbon.Shared;

namespace Carbon.Examples.WebService.Common
{
	public sealed class RunSpecRequest
	{
		public string Name { get; set; }
		public XDisplayProperties DProps { get; set; }
		public TableSpec Spec { get; set; }

	}
}
