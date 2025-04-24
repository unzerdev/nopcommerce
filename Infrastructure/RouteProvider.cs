using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Infrastructure;

namespace Unzer.Plugin.Payments.Unzer.Infrastructure
{
    /// <summary>
    /// Represents plugin route provider
    /// </summary>
    public class RouteProvider : BaseRouteProvider, IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="endpointRouteBuilder">Route builder</param>
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            var lang = GetLanguageRoutePattern();

            endpointRouteBuilder.MapControllerRoute(name: UnzerPaymentDefaults.ConfigurationRouteName,
                pattern: "Admin/UnzerPayment/Configure",
                defaults: new { controller = "UnzerPayment", action = "Configure", area = AreaNames.ADMIN });

            endpointRouteBuilder.MapControllerRoute(name: UnzerPaymentDefaults.CallBackUrlRouteName,
                pattern: "unzerpayment/callbackhandler",
                defaults: new { controller = "UnzerCallback", action = "CallbackHandler" });

            endpointRouteBuilder.MapControllerRoute(name: UnzerPaymentDefaults.UnzerPaymentStatusRouteName,
                pattern: "unzerpayment/unzerpaymentcompleted/{orderId:int}",
                defaults: new { controller = "UnzerCallback", action = "UnzerPaymentCompleted" });

            endpointRouteBuilder.MapControllerRoute(name: UnzerPaymentDefaults.UnzerPrePaymentComplteRouteName,
                pattern: "unzerpayment/unzerprepaymentcompleted/{model}",
                defaults: new { controller = "UnzerCallback", action = "UnzerPrePaymentCompleted" });

            endpointRouteBuilder.MapControllerRoute(name: UnzerPaymentDefaults.UnzerCancelOrderRouteName,
                pattern: "unzerpayment/cancelorder",
                defaults: new { controller = "UnzerCallback", action = "CancelOrder" });

        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}