// © Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SymCacheHttpServer;
using System;
using System.IO;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
            .AddNegotiate();

        services.AddSingleton<SymCacheRepository>();
        services.AddSingleton<SymCacheTranscoder>();
        services.AddSingleton<SymbolServerClient>();
        services.AddSingleton<BackgroundTranscodeService>();
        services.AddSingleton<IBackgroundTranscodeQueue>(sp => sp.GetRequiredService<BackgroundTranscodeService>());
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<BackgroundTranscodeService>());

        Uri symbolServer = new Uri(GetValueOrThrow("SymbolServer"));
        string symCacheDirectory = GetValueOrThrow("SymCacheDirectory");

        if (!Directory.Exists(symCacheDirectory))
        {
            throw new DirectoryNotFoundException(symCacheDirectory);
        }

        string transcoderPath = GetValueOrThrow("TranscoderPath");

        if (!File.Exists(transcoderPath))
        {
            throw new FileNotFoundException(transcoderPath);
        }

        SemanticVersion transcoderVersion = SemanticVersion.Parse(GetValueOrThrow("TranscoderVersion"));

        services.Configure<SymCacheOptions>(o =>
        {
            o.SymbolServer = symbolServer;
            o.SymCacheDirectory = symCacheDirectory;
            o.TranscoderPath = transcoderPath;
            o.TranscoderVersion = transcoderVersion;
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }

    string GetValueOrThrow(string name)
    {
        string value = Configuration[name];

        if (value == null)
        {
            throw new InvalidOperationException($"{name} is a required configuration parameter. " +
                $"Please set an environment variable with an appropriate value.");
        }

        return value;
    }
}
