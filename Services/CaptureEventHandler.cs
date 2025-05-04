using System.Text;
using System.Text.Json;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Unzer.Plugin.Payments.Unzer.Infrastructure;
using Unzer.Plugin.Payments.Unzer.Models;
using Unzer.Plugin.Payments.Unzer.Models.Api;

namespace Unzer.Plugin.Payments.Unzer.Services;
public class CaptureEventHandler : ICallEventHandler<CaptureEventHandler>
{
    private readonly IUnzerApiService _unzerApiService;
    private readonly IOrderService _orderService;
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly UnzerPaymentSettings _unzerPaymentSettings;
    private readonly IPriceFormatter _priceFormatter;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILocalizationService _localizationService;
    private readonly IStoreContext _storeContext;
    private readonly ILogger _logger;

    public CaptureEventHandler(
        IUnzerApiService unzerApiService,
        IOrderService orderService,
        IOrderProcessingService orderProcessingService,
        UnzerPaymentSettings unzerPaymentSettings,
        IPriceFormatter priceFormatter,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        IStoreContext storeContext,
        ILogger logger)
    {
        _unzerApiService = unzerApiService;
        _orderService = orderService;
        _orderProcessingService = orderProcessingService;
        _unzerPaymentSettings = unzerPaymentSettings;
        _priceFormatter = priceFormatter;
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _storeContext = storeContext;
        _logger = logger;
    }

    public async Task HandleEvent(UnzerCallbackPayload eventPayload)
    {
        if (UnzerPaymentDefaults.IgnoreCallbackEvents.Contains(eventPayload.Event))
            return;

        var chargeId = eventPayload.retrieveUrl.Substring(eventPayload.retrieveUrl.LastIndexOf('/') + 1);

        var paymentCapt = await _unzerApiService.PaymentCaptureResponse(eventPayload.paymentId, chargeId);
        if (paymentCapt == null)
        {            
            throw new NopException("CaptureEventHandler: No Payment Capture response could be fetched");
        }

        var orderId = Convert.ToInt32(paymentCapt.orderId);
        var nopOrder = await _orderService.GetOrderByIdAsync(orderId);
        if (nopOrder == null)
            throw new NopException($"Order {paymentCapt.orderId} for payment {eventPayload.paymentId} could not be found");

        if (paymentCapt.IsError)
        {
            await HandleCaptureFailed(nopOrder, paymentCapt);
            return;
        }

        if (eventPayload.Event == "charge.succeeded" && nopOrder.PaymentStatus == PaymentStatus.Paid)
            return;

        if (eventPayload.Event == "charge.pending" && !UnzerPaymentDefaults.ReadUnzerPaymentType(nopOrder.PaymentMethodSystemName).Prepayment)
            return;

        await UpdatePaymentAsync(nopOrder, paymentCapt);
    }

    public async Task HandleEvent(PaymentCaptureResponse paymentResponse)
    {
        var nopOrder = await _orderService.GetOrderByIdAsync(Convert.ToInt32(paymentResponse.orderId));
        if (nopOrder == null)
            throw new NopException($"Order {paymentResponse.orderId} could not be found");

        await UpdatePaymentAsync(nopOrder, paymentResponse);
    }

    private async Task UpdatePaymentAsync(Order order, PaymentCaptureResponse paymentCapt)
    {
        var paymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);

        var isRecurrence = paymentCapt.additionalTransactionData?.card?.recurrenceType == "scheduled";
        var isAutoCapture = _unzerPaymentSettings.AutoCapture == AutoCapture.AutoCapture ||
                            _unzerPaymentSettings.AutoCapture == AutoCapture.OnAuthForNoneDeliverProduct ||
                            _unzerPaymentSettings.AutoCapture == AutoCapture.OnAuthForDownloadableProduct;
        var isPrePayment = paymentType.Prepayment;
        var isOnlyCharge = !paymentType.SupportAuthurize && paymentType.SupportCharge;

        if (isPrePayment && paymentCapt.IsPending && order.PaymentStatus == PaymentStatus.Pending)
        {
            await HandlePrepaymentAsync(order, paymentCapt);
        }

        if ((isAutoCapture || isRecurrence || isPrePayment || isOnlyCharge) && order.PaymentStatus == PaymentStatus.Pending)
        {
            if (_orderProcessingService.CanMarkOrderAsAuthorized(order))
            {
                if (isPrePayment)
                {
                    var authAndPayID = $"{paymentCapt.resources.paymentId}/{paymentCapt.Id}";
                    order.AuthorizationTransactionId = authAndPayID;                    
                }

                await _orderProcessingService.MarkAsAuthorizedAsync(order);

                var sb = new StringBuilder();
                sb.AppendFormat("Unzer Payment id: {0}", paymentCapt.resources.paymentId).AppendLine();
                sb.AppendFormat("Short ID: {0}", paymentCapt.processing?.shortId).AppendLine();
                sb.AppendFormat("Charge success: {0}", paymentCapt.IsSuccess).AppendLine();
                sb.AppendFormat("Pending: {0}", paymentCapt.IsPending).AppendLine();
                sb.AppendFormat("Payment type: {0}", UnzerPaymentDefaults.MapPaymentType(paymentCapt.resources.typeId)).AppendLine();

                // order note update
                await AddOrderNote(order, sb.ToString());
            }
        }

        if (!await _orderProcessingService.CanCaptureAsync(order) || (isPrePayment && paymentCapt.IsPending))
        {
            if (isPrePayment && paymentCapt.IsPending)
            {
                order.OrderStatus = OrderStatus.Pending;
                await _orderService.UpdateOrderAsync(order);
                await AddOrderNote(order, "Unzer Prepayment is pending, waiting for payment");

                await _logger.WarningAsync($"CaptureEventHandler.UpdatePaymentAsync: Capture for order {order.Id} cannot be handled, order payment is pending");                
            }
            else
            {
                await _logger.WarningAsync($"CaptureEventHandler.UpdatePaymentAsync: Capture for order {order.Id} cannot be handled, order must be authorized");
                await AddOrderNote(order, "Order cannot be captured, must be authorized");
            }
        }
        else
        {
            var captAndPayID = $"{paymentCapt.resources.paymentId}/{paymentCapt.Id}";
            order.CaptureTransactionId = captAndPayID;
            order.CaptureTransactionResult = paymentCapt.message.merchant;
            order.SubscriptionTransactionId = isRecurrence ? paymentCapt.resources?.typeId : null;

            await _orderService.UpdateOrderAsync(order);

            if (_orderProcessingService.CanMarkOrderAsPaid(order))
            {
                await _orderProcessingService.MarkOrderAsPaidAsync(order);
            }
            else
            {
                await _logger.WarningAsync($"CaptureEventHandler.UpdatePaymentAsync: Captured order {order.Id} cannot be marked as paid");
                await AddOrderNote(order, "Order cannot be marked as paid");
            }
        }
    }

    private async Task HandlePrepaymentAsync(Order order, PaymentCaptureResponse paymentCapt)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var languageId = _storeContext.GetCurrentStore().DefaultLanguageId;
        var orderAmount = await _priceFormatter.FormatPriceAsync(order.OrderTotal, true, order.CustomerCurrencyCode, false, languageId);
        var paymentInstructions = await _localizationService.GetResourceAsync("Plugins.Payments.Unzer.PaymentMethod.Prepayment.Instructions");
        var paymentRef = await _localizationService.GetResourceAsync("Plugins.Payments.Unzer.PaymentMethod.Prepayment.Reference");

        var prePaymentComplete = new PrePaymentCompletedModel
        {
            OrderId = order.Id,
            HowToPay = string.Format(paymentInstructions, orderAmount),
            holder = $"Holder: {paymentCapt.processing.holder}",
            Bic = $"BIC: {paymentCapt.processing.bic}",
            Iban = $"IBAN {paymentCapt.processing.iban}",
            PaymentReference = $"{paymentRef} {paymentCapt.processing.shortId}"
        };

        await _genericAttributeService.SaveAttributeAsync(order, UnzerPaymentDefaults.PrePaymentInstructionAttribute, JsonSerializer.Serialize(prePaymentComplete), store.Id);

        var sb = new StringBuilder();
        sb.AppendLine(prePaymentComplete.HowToPay);
        sb.AppendLine(prePaymentComplete.holder);
        sb.AppendLine(prePaymentComplete.Bic);
        sb.AppendLine(prePaymentComplete.Iban);
        sb.AppendLine(await _localizationService.GetResourceAsync("Plugins.Payments.Unzer.PaymentMethod.Prepayment.Reference"));
        sb.AppendLine(paymentCapt.processing.shortId);

        await _orderService.InsertOrderNoteAsync(new OrderNote
        {
            OrderId = order.Id,
            Note = sb.ToString(),
            DisplayToCustomer = true,
            CreatedOnUtc = DateTime.UtcNow
        });
    }

    private async Task HandleCaptureFailed(Order order, PaymentCaptureResponse paymentCapt)
    {
        var sb = new StringBuilder();
        sb.AppendFormat("Capture failed: {0}", paymentCapt.message.customer).AppendLine();
        // order note update to merchant
        await AddOrderNote(order, sb.ToString(), true);

        sb = new StringBuilder();
        sb.AppendFormat("Capture failed: {0}", paymentCapt.message.merchant).AppendLine();
        sb.AppendFormat("Unzer Payment id: {0}", paymentCapt.resources.paymentId).AppendLine();
        sb.AppendFormat("Short ID: {0}", paymentCapt.processing?.shortId).AppendLine();
        sb.AppendFormat("Pending: {0}", paymentCapt.IsPending).AppendLine();
        sb.AppendFormat("Payment type: {0}", UnzerPaymentDefaults.MapPaymentType(paymentCapt.resources.typeId)).AppendLine();
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
