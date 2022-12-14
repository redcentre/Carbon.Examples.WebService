using System;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Carbon.Examples.WebService.Common;
using Carbon.Examples.WebService.WebApi;

var builder = WebApplication.CreateBuilder(args);

var asm = typeof(Program).Assembly;
var infoattr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

builder.Services.AddControllers(opt =>
{
	opt.OutputFormatters.RemoveType<HttpNoContentOutputFormatter>();
}).AddXmlSerializerFormatters();

// Serilog was being used for a while, but it's currently commented out since this is just an example project.
//var logger = new LoggerConfiguration()
//  .ReadFrom.Configuration(builder.Configuration)
//  .Enrich.FromLogContext()
//  .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
//  .CreateLogger();
//builder.Logging.ClearProviders();
//builder.Logging.AddSerilog(logger);

SessionManager.CacheSlidingSeconds = builder.Configuration.GetValue<int>("Service:SessionCacheSlideSeconds");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Carbon Web API",
		Version = "v1",
		Description = $"REST style web service version {asm.GetName().Version} (build {infoattr!.InformationalVersion}). This web service is under development by Red Centre Software. Access to the service requires a registered authorization key to be present in the request headers.",
		Contact = new OpenApiContact()
		{
			Name = "Red Centre Software",
			Url = new Uri("https://www.redcentresoftware.com/"),
			Email = "support@redcentresoftware.com"
		}
	});
	var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
	c.IncludeXmlComments(xmlFile);
	// The docs for the common library could be added to the swagger here (not at the moment)
	//xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(HoarderConfiguration).Assembly.GetName().Name}.xml");
	//c.IncludeXmlComments(xmlFile);
	c.AddSecurityDefinition("basic", new OpenApiSecurityScheme
	{
		Name = CarbonServiceClient.SessionIdHeaderKey,
		Type = SecuritySchemeType.ApiKey,
		Scheme = "basic",
		In = ParameterLocation.Header,
		Description = "Simple authorisation using a request header."
	});
	c.AddSecurityRequirement(new OpenApiSecurityRequirement
	{
		{
			new OpenApiSecurityScheme
			{
				Reference = new OpenApiReference
				{
					Type = ReferenceType.SecurityScheme,
					Id = "basic"
				}
			},
			Array.Empty<string>()
}
	});
});
builder.Services.AddControllers()
	.AddXmlSerializerFormatters();
	// Testing when the GenNode would not serialize. Not needed.
	//.AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

var app = builder.Build();

SessionManager.Load();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
	// Use the standard error handling page in both environments
	//app.UseDeveloperExceptionPage();
	app.UseExceptionHandler("/error");
}
else
{
	app.UseExceptionHandler("/error");
}

//app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
