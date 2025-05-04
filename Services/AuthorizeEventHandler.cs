using System.Runtime.CompilerServices;
using System.Text;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Unzer.Plugin.Payments.Unzer.Infrastructure;
using Unzer.Plugin.Payments.Unzer.Models.Api;

namespace Unzer.Plugin.Payments.Unzer.Services;
public class AuthorizeEventHandler : ICallEventHandler<AuthorizeEventHandler>
{
    private readonly IUnzerApiService _unzerApiService;
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly UnzerPaymentSettings _unzerPaymentSettings;
    private readonly ILogger _logger;

    public AuthorizeEventHandler(
        IUnzerApiService unzerApiService,
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        UnzerPaymentSettings unzerPaymentSettings,
        ILogger logger)
    {           
        _unzerApiService = unzerApiService;
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _unzerPaymentSettings = unzerPaymentSettings;
        _logger = logger;
    }

    public async Task HandleEvent(UnzerCallbackPayload eventPayload)
    {
        if( UnzerPaymentDefaults.IgnoreCallbackEvents.Contains(eventPayload.Event))
            return;

        var paymentAuth = await _unzerApiService.PaymentAuthorizedResponse(eventPayload.paymentId);
        if(paymentAuth == null)
            throw new NopException(paymentAuth.ErrorResponse.Errors.First().merchantMessage);

        var nopOrder = await _orderService.GetOrderByIdAsync(Convert.ToInt32(paymentAuth.orderId));
        if(nopOrder == null)
            throw new NopException($"Order {paymentAuth.orderId} for payment {eventPayload.paymentId} could not be found");

        if (paymentAuth.IsError && eventPayload.Event == "authorize.failed")
        {
            await HandleAuthorizeFailed(nopOrder, paymentAuth);
            return;
        }

        if (eventPayload.Event == "authorize.succeeded" && nopOrder.PaymentStatus == PaymentStatus.Authorized)
            return;
        
        await UpdatePayment(nopOrder, paymentAuth);
    }

    public async Task HandleEvent(PaymentCaptureResponse paymentResponse)
    {
        var nopOrder = await _orderService.GetOrderByIdAsync(Convert.ToInt32(paymentResponse.orderId));
        if (nopOrder == null)
            throw new NopException($"Order {paymentResponse.orderId} could not be found");

        await UpdatePayment(nopOrder, paymentResponse);
    }

    public async Task UpdatePayment(Order order, PaymentCaptureResponse paymentAuth)
    {
        var authIsOk = paymentAuth.IsSuccess;
        if (authIsOk)
        {
            var authAndPayID = $"{paymentAuth.resources.paymentId}/{paymentAuth.Id}";
            order.AuthorizationTransactionId = authAndPayID;
            order.CardType = paymentAuth.resources.typeId;
        }

        await _orderService.UpdateOrderAsync(order);

        var sb = new StringBuilder();
        sb.AppendFormat("Unzer Payment id: {0}", paymentAuth.resources.paymentId).AppendLine();
        sb.AppendFormat("Short ID: {0}", paymentAuth.processing?.shortId).AppendLine();
        sb.AppendFormat("Auth. success: {0}", paymentAuth.IsSuccess).AppendLine();
        sb.AppendFormat("Pending: {0}", paymentAuth.IsPending).AppendLine();        
        sb.AppendFormat("Payment type: {0}", UnzerPaymentDefaults.MapPaymentType(paymentAuth.resources.typeId)).AppendLine();

        // order note update
        await AddOrderNote(order, sb.ToString());

        // mark order as Authorized if payment authurize is succesfull!
        if (authIsOk && !paymentAuth.IsPending && this._orderProcessingService.CanMarkOrderAsAuthorized(order))
        {
            await _orderProcessingService.MarkAsAuthorizedAsync(order);
        }
        else if (authIsOk && paymentAuth.IsPending)
        {
            order.PaymentStatus = PaymentStatus.Pending;
        }

        if (authIsOk && !paymentAuth.IsPending)
        {
            if (_unzerPaymentSettings.AutoCapture ==  Infrastructure.AutoCapture.OnAuthForDownloadableProduct ||
                _unzerPaymentSettings.AutoCapture ==  Infrastructure.AutoCapture.OnAuthForNoneDeliverProduct)
            {
                //await HandleAutoCaptureOrderProducts(order);
            }
        }
    }

    private async Task HandleAuthorizeFailed(Order order, PaymentCaptureResponse paymentAuth)
    {
        var sb = new StringBuilder();
        sb.AppendFormat("Authorize failed: {0}", paymentAuth.message.customer).AppendLine();
        // order note update to merchant
        await AddOrderNote(order, sb.ToString(), true);

        sb = new StringBuilder();
        sb.AppendFormat("Authorize failed: {0}", paymentAuth.message.merchant).AppendLine();
        sb.AppendFormat("Unzer Payment id: {0}", paymentAuth.resources.paymentId).AppendLine();
        sb.AppendFormat("Short ID: {0}", paymentAuth.processing?.shortId).AppendLine();        
        sb.AppendFormat("Pending: {0}", paymentAuth.IsPending).AppendLine();
        sb.AppendFormat("Payment type: {0}", UnzerPaymentDefaults.MapPaymentType(paymentAuth.resources.typeId)).AppendLine();
        // order note update to merchant
        await AddOrderNote(order, sb.ToString());
    }

    private async Task AddOrderNote(Order order, string note, bool displayToCustomer = false)
    {
        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = note,
            DisplayToCustomer = displayToCustomer,
            CreatedOnUtc = DateTime.UtcNow
        });
    }
}
