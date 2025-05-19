using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Web.Framework.Infrastructure.Extensions;
using Unzer.Plugin.Payments.Unzer.Services;

namespace Unzer.Plugin.Payments.Unzer.Infrastructure
{
    public class NopStartUp : INopStartup
    {
        public int Order => 20000;

        public void Configure(IApplicationBuilder application)
        {
        }

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient<ApiBearerTokenHandler>();

            services.AddHttpClient<UnzerApiHttpClient>()
                .AddHttpMessageHandler<ApiBearerTokenHandler>()
                .WithProxy();

            services.AddScoped<UnzerPaymentRequestBuilder>();
            services.AddScoped<IUnzerApiService, UnzerApiService>();
            services.AddScoped<ICallEventHandler<AuthorizeEventHandler>, AuthorizeEventHandler>();
            services.AddScoped<ICallEventHandler<CaptureEventHandler>, CaptureEventHandler>();            
            services.AddScoped<IOrderProcessingService, DelayedPlaceOrderProcessingService>();
            services.AddScoped<IPaymentPluginManager, UnzerPaymentPluginManager>();
            services.AddScoped<IMessageTokenProvider, UnzerMessageTokenProvider>();
            services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
            services.AddHostedService<QueuedProcessorBackgroundService>();
        }
    }
}
