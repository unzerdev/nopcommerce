using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Unzer.Plugin.Payments.Unzer.Models;
using Unzer.Plugin.Payments.Unzer.Models.Api;
using Unzer.Plugin.Payments.Unzer.Services;

namespace Unzer.Plugin.Payments.Unzer.Controllers;
public class UnzerCallbackController : Controller
{
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly IOrderService _orderService;
    private readonly UnzerPaymentSettings _unzerSettings;
    private readonly ILogger _logger;
    private readonly IWorkContext _workContext;
    private readonly IStoreContext _storeContext;
    private readonly IUnzerApiService _unzerApiService;
    private readonly ICustomerService _customerService;
    private readonly OrderSettings _orderSettings;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ICallEventHandler<AuthorizeEventHandler> _authEventHandler;
    private readonly ICallEventHandler<CaptureEventHandler> _captEventHandler;

    public UnzerCallbackController(
        IPaymentPluginManager paymentPluginManager,
        IOrderService orderService,
        UnzerPaymentSettings unzerSettings,
        ILogger logger, IWorkContext workContext,
        IStoreContext storeContext,
        IUnzerApiService unzerApiService,
        ICustomerService customerService,
        OrderSettings orderSettings,
        IGenericAttributeService genericAttributeService,
        ICallEventHandler<AuthorizeEventHandler> authEventHandler,
        ICallEventHandler<CaptureEventHandler> captEventHandler)
    {
        _paymentPluginManager = paymentPluginManager;
        _orderService = orderService;
        _unzerSettings = unzerSettings;
        _logger = logger;
        _workContext = workContext;
        _storeContext = storeContext;
        _unzerApiService = unzerApiService;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _authEventHandler = authEventHandler;
        _captEventHandler = captEventHandler;
    }

    [HttpPost]
    public async Task<IActionResult> CallbackHandler()
    {
        Response.StatusCode = 200;

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        // Check if Unzer Pay module is alive
        if (!await _paymentPluginManager.IsPluginActiveAsync(UnzerPaymentDefaults.SystemName, customer, store.Id))
        {
            var errorMsg = "CallBackHandler Post - UnzerPayment method is not active or not installed!";
            await _logger.InsertLogAsync(LogLevel.Warning, errorMsg);
            throw new NopException(errorMsg);
        }

        var content = string.Empty;
        using (var streamReader = new StreamReader(Request.Body))
            content = await streamReader.ReadToEndAsync();
        
        var callBackReq = JsonSerializer.Deserialize<UnzerCallbackPayload>(content);

        if (_unzerSettings.LogCallbackPostData)
        {
            await _logger.InsertLogAsync(LogLevel.Information, $"UnzerPayment {callBackReq.Event} full callback content", content);
        }

        if (callBackReq.publicKey != _unzerSettings.UnzerPublicApiKey)
        {
            var errorMsg = "CallBackHandler Post - The recieved key does not match the configured key";
            await _logger.InsertLogAsync(LogLevel.Warning, errorMsg);
            throw new NopException(errorMsg);
        }

        if(!UnzerPaymentDefaults.AllowedUrls.Any(u => callBackReq.retrieveUrl.Contains(u)))
        {
            var errorMsg = "CallBackHandler Post - The retrieve Url is not allowed";
            await _logger.InsertLogAsync(LogLevel.Warning, errorMsg);
            throw new NopException(errorMsg);
        }

        if (callBackReq.Event.StartsWith("authorize."))
            await _authEventHandler.HandleEvent(callBackReq);

        if (callBackReq.Event.StartsWith("charge."))
            await _captEventHandler.HandleEvent(callBackReq);

        return Ok();
    }

    [HttpGet]
    public async Task<IActionResult> UnzerPaymentCompleted(int orderId)
    {
        Order order = await _orderService.GetOrderByIdAsync(orderId);
        if (order == null || order.Deleted)
        {
            return RedirectToRoute("Homepage");
        }
        var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
        if (unzerPaymentType.Prepayment)
        {
            var store = await _storeContext.GetCurrentStoreAsync();
            //var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);            
            var instructionJson = await _genericAttributeService.GetAttributeAsync<string>(order, UnzerPaymentDefaults.PrePaymentInstructionAttribute, store.Id);
            var prePaymentInstModel = JsonSerializer.Deserialize<PrePaymentCompletedModel>(instructionJson);
            //await _genericAttributeService.SaveAttributeAsync<PrePaymentCompletedModel>(order, UnzerPaymentDefaults.PrePaymentInstructionAttribute, null, store.Id);
            return View("~/Plugins/Payments.Unzer/Views/Completed.cshtml", prePaymentInstModel);
        }

        await _logger.InformationAsync("UnzerCallbackController.UnzerPaymentStatus: Return from Unzer Payment Page");
        
        var waitCnt = 3;
        while (order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Pending && waitCnt > 0)
        {
            var timespan = TimeSpan.FromSeconds(2);
            await Task.Delay(timespan);

            order = await _orderService.GetOrderByIdAsync(orderId);
            waitCnt--;
        }

        var waitedFor = (3 - waitCnt) * 2;
        await _logger.InformationAsync($"UnzerCallbackController.UnzerPaymentStatus: Return from Unzer Payment Page, waited {waitedFor} sec.");

        if (order.PaymentStatus == Nop.Core.Domain.Payments.PaymentStatus.Pending)
        {
            return RedirectToRoute("OrderDetails", new { orderId = order.Id });
        }

        //return View("~/Plugins/Payments.Unzer/Views/Completed.cshtml", model);

        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> UnzerPrePaymentCompleted(PrePaymentCompletedModel model)
    {
        Order order = await _orderService.GetOrderByIdAsync(model.OrderId);
        if (order == null || order.Deleted)
        {
            return RedirectToRoute("Homepage");
        }
        var unzerPaymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
        if (unzerPaymentType.Prepayment)
        {
            return View("~/Plugins/Payments.Unzer/Views/Completed.cshtml", model);
        }

        return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
    }
}
