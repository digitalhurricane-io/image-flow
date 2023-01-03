using Amazon;
using System.IO;
using Imageflow.Fluent;
using Imageflow.Server.HybridCache;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Imageflow.Server.Storage.S3;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.Extensions.NETCore.Setup;
using System;

namespace Imageflow.Server.ExampleDockerDiskCache
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            Env = env;
        }

        private IConfiguration Configuration { get; }
        private IWebHostEnvironment Env { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddAWSService<IAmazonS3>(new AWSOptions 
            {
                Credentials = new AnonymousAWSCredentials(),
                Region = RegionEndpoint.USWest1
            });

            // Make S3 containers available at /ri/ and /imageflow-resources/
            // If you use credentials, do not check them into your repository
            // You can call AddImageflowS3Service multiple times for each unique access key
            services.AddImageflowS3Service(new S3ServiceOptions()
                .MapPrefix("/prod/", "chido-prod")
                // .MapPrefix("/imageflow-resources/", RegionEndpoint.USWest2, "imageflow-resources")
                // .MapPrefix("/custom-s3client/", () => new AmazonS3Client(), "custom-client", "", false, false)
            );

            services.AddImageflowHybridCache(new HybridCacheOptions(Path.Combine(Env.ContentRootPath, "imageflow_cache"))
            {
                // How long after a file is created before it can be deleted
                MinAgeToDelete = TimeSpan.FromSeconds(60*60*48),
                CacheSizeLimitInBytes = (long)10 * 1024 * 1024 * 1024 // 10 GiB
            });
        }
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }else
            {
                app.UseExceptionHandler("/error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            //app.UseHttpsRedirection();

            app.UseImageflow(new ImageflowMiddlewareOptions()
                .SetMyOpenSourceProjectUrl("https://please-support-imageflow-with-a-license.com")
                .SetMapWebRoot(true)
                .MapPath("/images", Path.Combine(Env.ContentRootPath, "images"))
                .SetAllowCaching(true)
                // Cache publicly (including on shared proxies and CDNs) for 30 days
                .SetDefaultCacheControlString("public, max-age=2592000")
                .SetJobSecurityOptions(new SecurityOptions()
                    .SetMaxDecodeSize(new FrameSizeLimit(8000, 8000, 40))
                    .SetMaxFrameSize(new FrameSizeLimit(8000, 8000, 40))
                    .SetMaxEncodeSize(new FrameSizeLimit(8000, 8000, 20)))
            );
            
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("<img src=\"fire-umbrella-small.jpg?width=450\" />");
                });
                
                endpoints.MapGet("/error", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync("<p>An error has occurred while processing the request.</p>");
                });
            });
        }
    }
}