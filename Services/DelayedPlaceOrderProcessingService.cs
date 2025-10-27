using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Events;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Services.Vendors;

namespace Unzer.Plugin.Payments.Unzer.Services
{
    public class DelayedPlaceOrderProcessingService : OrderProcessingService
    {
        #region Fields

        private readonly IOrderService _orderService;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly ILogger _logger;
        private readonly ICustomerService _customerService;
        private readonly IDiscountService _discountService;
        private readonly IWorkContext _workContext;
        private readonly IWorkflowMessageService _workflowMessageService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly IEventPublisher _eventPublisher;
        private readonly IPdfService _pdfService;
        private readonly ISettingService _settingService;

        private readonly OrderSettings _orderSettings;
        private readonly LocalizationSettings _localizationSettings;

        #endregion

        public DelayedPlaceOrderProcessingService(CurrencySettings currencySettings, IAddressService addressService, IAffiliateService affiliateService, ICheckoutAttributeFormatter checkoutAttributeFormatter, ICountryService countryService, ICurrencyService currencyService, ICustomerActivityService customerActivityService, ICustomerService customerService, ICustomNumberFormatter customNumberFormatter, IDiscountService discountService, IEncryptionService encryptionService, IEventPublisher eventPublisher, IGenericAttributeService genericAttributeService, IGiftCardService giftCardService, ILanguageService languageService, ILocalizationService localizationService, ILogger logger, IOrderService orderService, IOrderTotalCalculationService orderTotalCalculationService, IPaymentPluginManager paymentPluginManager, IPaymentService paymentService, IPdfService pdfService, IPriceCalculationService priceCalculationService, IPriceFormatter priceFormatter, IProductAttributeFormatter productAttributeFormatter, IProductAttributeParser productAttributeParser, IProductService productService, IReturnRequestService returnRequestService, IRewardPointService rewardPointService, IShipmentService shipmentService, IShippingService shippingService, IShoppingCartService shoppingCartService, IStateProvinceService stateProvinceService, IStaticCacheManager staticCacheManager, IStoreContext storeContext, IStoreMappingService storeMappingService, IStoreService storeService, ITaxService taxService, IVendorService vendorService, IWebHelper webHelper, IWorkContext workContext, IWorkflowMessageService workflowMessageService, LocalizationSettings localizationSettings, OrderSettings orderSettings, PaymentSettings paymentSettings, RewardPointsSettings rewardPointsSettings, ShippingSettings shippingSettings, TaxSettings taxSettings, ISettingService settingService) : base(currencySettings, addressService, affiliateService, checkoutAttributeFormatter, countryService, currencyService, customerActivityService, customerService, customNumberFormatter, discountService, encryptionService, eventPublisher, genericAttributeService, giftCardService, languageService, localizationService, logger, orderService, orderTotalCalculationService, paymentPluginManager, paymentService, pdfService, priceCalculationService, priceFormatter, productAttributeFormatter, productAttributeParser, productService, returnRequestService, rewardPointService, shipmentService, shippingService, shoppingCartService, stateProvinceService, staticCacheManager, storeContext, storeMappingService, storeService, taxService, vendorService, webHelper, workContext, workflowMessageService, localizationSettings, orderSettings, paymentSettings, rewardPointsSettings, shippingSettings, taxSettings)
        {
            _orderService = orderService;
            _localizationService = localizationService;
            _paymentPluginManager = paymentPluginManager;
            _logger = logger;
            _workContext = workContext;
            _workflowMessageService = workflowMessageService;
            _customerService = customerService;
            _discountService = discountService;
            _customerActivityService = customerActivityService;
            _eventPublisher = eventPublisher;
            _pdfService = pdfService;
            _settingService = settingService;

            _orderSettings = orderSettings;
            _localizationSettings = localizationSettings;
        }


        /// <summary>
        /// Places an order
        /// </summary>
        /// <param name="processPaymentRequest">Process payment request</param>
        /// <returns>Place order result</returns>
        public override async Task<PlaceOrderResult> PlaceOrderAsync(ProcessPaymentRequest processPaymentRequest)
        {
            ArgumentNullException.ThrowIfNull(processPaymentRequest);

            if (string.IsNullOrEmpty(processPaymentRequest.PaymentMethodSystemName))
                return await base.PlaceOrderAsync(processPaymentRequest);

            var paymentSystemName = string.IsNullOrEmpty(processPaymentRequest.PaymentMethodSystemName) ? string.Empty : string.Join(".", processPaymentRequest.PaymentMethodSystemName.Split('.').Take(2));
            var orderDelaySettingName = $"{paymentSystemName}.PlaceOrderDelay";            
            var placeOrderDelayed = await _settingService.GetSettingByKeyAsync(orderDelaySettingName, false);
            await _logger.InformationAsync($"DelayedPlaceOrderProcessingService.Placeorder looking for parameter {orderDelaySettingName} and found value {placeOrderDelayed}");
            if (!placeOrderDelayed)
            {
                await _logger.InformationAsync("DelayedPlaceOrderProcessingService.Placeorder normal PlaceOrder flow selected");
                return await base.PlaceOrderAsync(processPaymentRequest);
            }

            var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(processPaymentRequest.PaymentMethodSystemName);
            if (!_paymentPluginManager.IsPluginActive(paymentMethod))
                throw new NopException("Payment method is not active");

            var waitForPaymentAuth = paymentMethod.PaymentMethodType == PaymentMethodType.Redirection;
            if (!waitForPaymentAuth)
            {
                return await base.PlaceOrderAsync(processPaymentRequest);
            }

            if (processPaymentRequest.OrderGuid == Guid.Empty)
                throw new Exception("Order GUID is not generated");

            //prepare order details
            var details = await PreparePlaceOrderDetailsAsync(processPaymentRequest);

            async Task<PlaceOrderResult> placeOrder(PlaceOrderContainer placeOrderContainer)
            {
                var result = new PlaceOrderResult();

                try
                {
                    var processPaymentResult =
                        await GetProcessPaymentResultAsync(processPaymentRequest, placeOrderContainer)
                        ?? throw new NopException("processPaymentResult is not available");

                    waitForPaymentAuth = processPaymentResult.NewPaymentStatus == PaymentStatus.Pending;

                    if (processPaymentResult.Success)
                    {
                        var order = await SaveOrderDetailsAsync(processPaymentRequest, processPaymentResult,
                            placeOrderContainer);
                        result.PlacedOrder = order;

                        //move shopping cart items to order items
                        await MoveShoppingCartItemsToOrderItemsAsync(placeOrderContainer, order);

                        //discount usage history
                        await SaveDiscountUsageHistoryAsync(placeOrderContainer, order);

                        //gift card usage history
                        await SaveGiftCardUsageHistoryAsync(placeOrderContainer, order);

                        //recurring orders
                        if (placeOrderContainer.IsRecurringShoppingCart)
                            await CreateFirstRecurringPaymentAsync(processPaymentRequest, order);

                        //notifications
                        await SendNotificationsAndSaveNotesAsync(order, waitForPaymentAuth);

                        //reset checkout data
                        await _customerService.ResetCheckoutDataAsync(placeOrderContainer.Customer,
                            processPaymentRequest.StoreId, clearCouponCodes: true, clearCheckoutAttributes: true);
                        await _customerActivityService.InsertActivityAsync("PublicStore.PlaceOrder",
                            string.Format(await _localizationService.GetResourceAsync("ActivityLog.PublicStore.PlaceOrder"),
                                order.Id), order);

                        //raise event       
                        await _eventPublisher.PublishAsync(new OrderPlacedEvent(order));

                        //check order status
                        await CheckOrderStatusAsync(order);

                        if (order.PaymentStatus == PaymentStatus.Paid)
                            await ProcessOrderPaidAsync(order);
                    }
                    else
                        foreach (var paymentError in processPaymentResult.Errors)
                            result.AddError(string.Format(
                                await _localizationService.GetResourceAsync("Checkout.PaymentError"), paymentError));
                }
                catch (Exception exc)
                {
                    await _logger.ErrorAsync(exc.Message, exc);
                    result.AddError(exc.Message);
                }

                if (result.Success)
                    return result;

                //log errors
                var logError = result.Errors.Aggregate("Error while placing order. ",
                    (current, next) => $"{current}Error {result.Errors.IndexOf(next) + 1}: {next}. ");
                var customer = await _customerService.GetCustomerByIdAsync(processPaymentRequest.CustomerId);
                await _logger.ErrorAsync(logError, customer: customer);

                return result;
            }

            if (!_orderSettings.PlaceOrderWithLock)
                return await placeOrder(details);

            PlaceOrderResult result;
            var resource = details.Customer.Id.ToString();

            //the named mutex helps to avoid creating the same order in different threads,
            //and does not decrease performance significantly, because the code is blocked only for the specific cart.
            //you should be very careful, mutexes cannot be used in with the await operation
            //we can't use semaphore here, because it produces PlatformNotSupportedException exception on UNIX based systems
            using var mutex = new Mutex(false, resource);

            mutex.WaitOne();

            try
            {
                var cacheKey = _staticCacheManager.PrepareKey(NopOrderDefaults.OrderWithLockCacheKey, resource);
                cacheKey.CacheTime = _orderSettings.MinimumOrderPlacementInterval;

                var exist = _staticCacheManager.Get(cacheKey, () => false);

                if (exist)
                {
                    result = new PlaceOrderResult();
                    result.Errors.Add(_localizationService.GetResourceAsync("Checkout.MinOrderPlacementInterval").Result);
                }
                else
                {
                    result = placeOrder(details).Result;

                    if (result.Success)
                        _staticCacheManager.SetAsync(cacheKey, true).Wait();
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }

            return result;
        }

        /// <summary>
        /// Marks order as authorized
        /// </summary>
        /// <param name="order">Order</param>
        public override async Task MarkAsAuthorizedAsync(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (string.IsNullOrEmpty(order.PaymentMethodSystemName))
            {
                await base.MarkAsAuthorizedAsync(order);
                return;
            }

            var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(order.PaymentMethodSystemName);

            if (paymentMethod == null)
                throw new NopException("Payment method couldn't be loaded");

            if (!_paymentPluginManager.IsPluginActive(paymentMethod))
                throw new NopException("Payment method is not active");

            var paymentSystemName = string.Join(".", order.PaymentMethodSystemName.Split('.').Take(2));
            var orderDelaySettingName = $"{paymentSystemName}.PlaceOrderDelay";
            var placeOrderDelayed = await _settingService.GetSettingByKeyAsync(orderDelaySettingName, false);            
            if (!placeOrderDelayed)
            {
                await base.MarkAsAuthorizedAsync(order);
                return;
            }

            if (paymentMethod.PaymentMethodType == PaymentMethodType.Redirection && order.PaymentStatus == PaymentStatus.Pending)
            {
                var orderPlacedStoreOwnerNotificationQueuedEmailIds = await _workflowMessageService.SendOrderPlacedStoreOwnerNotificationAsync(order, _localizationSettings.DefaultAdminLanguageId);
                await SendEmailNotificationsAsync(orderPlacedStoreOwnerNotificationQueuedEmailIds, order);
            }

            order.PaymentStatusId = (int)PaymentStatus.Authorized;
            await _orderService.UpdateOrderAsync(order);

            //add a note
            await AddOrderNoteAsync(order, "*Order has been marked as authorized");

            await _eventPublisher.PublishAsync(new OrderAuthorizedEvent(order));

            //check order status
            await CheckOrderStatusAsync(order);
        }

        public override async Task<bool> CanCaptureAsync(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(order.PaymentMethodSystemName);

            if (paymentMethod == null)
                throw new NopException("Payment method couldn't be loaded");

            if (!_paymentPluginManager.IsPluginActive(paymentMethod))
                throw new NopException("Payment method is not active");

            var paymentType = UnzerPaymentDefaults.ReadUnzerPaymentType(order.PaymentMethodSystemName);
            if (paymentType.Prepayment)
            {
                if (order.OrderStatus == OrderStatus.Pending && order.PaymentStatus == PaymentStatus.Authorized &&
                    await _paymentService.SupportCaptureAsync(order.PaymentMethodSystemName))
                    return true;

                return false;
            }

            if (order.OrderStatus == OrderStatus.Cancelled ||
                order.OrderStatus == OrderStatus.Pending)
                return false;

            if (order.PaymentStatus == PaymentStatus.Authorized &&
                await _paymentService.SupportCaptureAsync(order.PaymentMethodSystemName))
                return true;

            return false;
        }

        public override async Task MarkOrderAsPaidAsync(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(order.PaymentMethodSystemName);
            if (paymentMethod == null)
                throw new NopException("Payment method couldn't be loaded");

            if (!_paymentPluginManager.IsPluginActive(paymentMethod))
                throw new NopException("Payment method is not active");

            if (!CanMarkOrderAsPaid(order))
                throw new NopException("You can't mark this order as paid");

            if (paymentMethod.PaymentMethodType == PaymentMethodType.Redirection && order.PaymentStatus == PaymentStatus.Pending)
            {
                var orderPlacedStoreOwnerNotificationQueuedEmailIds = await _workflowMessageService.SendOrderPlacedStoreOwnerNotificationAsync(order, _localizationSettings.DefaultAdminLanguageId);
                await SendEmailNotificationsAsync(orderPlacedStoreOwnerNotificationQueuedEmailIds, order);
            }

            order.PaymentStatusId = (int)PaymentStatus.Paid;
            order.PaidDateUtc = DateTime.UtcNow;
            await _orderService.UpdateOrderAsync(order);

            //add a note
            await AddOrderNoteAsync(order, "*Order has been marked as paid");

            await CheckOrderStatusAsync(order);

            if (order.PaymentStatus == PaymentStatus.Paid)
                await ProcessOrderPaidAsync(order);
        }

        /// <summary>
        /// Send "order placed" notifications and save order notes
        /// </summary>
        /// <param name="order">Order</param>
        /// <param name="waitForPaymentAuth"></param>
        private async Task SendNotificationsAndSaveNotesAsync(Order order, bool waitForPaymentAuth)
        {
            //notes, messages
            await AddOrderNoteAsync(order, _workContext.OriginalCustomerIfImpersonated != null
                ? $"Order placed by a store owner ('{_workContext.OriginalCustomerIfImpersonated.Email}'. ID = {_workContext.OriginalCustomerIfImpersonated.Id}) impersonating the customer."
                : "Order placed");

            if (waitForPaymentAuth)
                await AddOrderNoteAsync(order, "*Waiting for payment authorization");

            //send email notifications
            if (!waitForPaymentAuth)
            {
                var orderPlacedStoreOwnerNotificationQueuedEmailIds = await _workflowMessageService.SendOrderPlacedStoreOwnerNotificationAsync(order, _localizationSettings.DefaultAdminLanguageId);
                await SendEmailNotificationsAsync(orderPlacedStoreOwnerNotificationQueuedEmailIds, order);
            }
        }

        private async Task SendEmailNotificationsAsync(IList<int> orderPlacedStoreOwnerNotificationQueuedEmailIds, Order order)
        {
            if (orderPlacedStoreOwnerNotificationQueuedEmailIds.Any())
                await AddOrderNoteAsync(order, $"\"Order placed\" email (to store owner) has been queued. Queued email identifiers: {string.Join(", ", orderPlacedStoreOwnerNotificationQueuedEmailIds)}.");

            var orderPlacedAttachmentFilePath = _orderSettings.AttachPdfInvoiceToOrderPlacedEmail ?
                await _pdfService.SaveOrderPdfToDiskAsync(order) : null;
            var orderPlacedAttachmentFileName = _orderSettings.AttachPdfInvoiceToOrderPlacedEmail ?
                string.Format(await _localizationService.GetResourceAsync("PDFInvoice.FileName"), order.CustomOrderNumber) + ".pdf" : null;
            var orderPlacedCustomerNotificationQueuedEmailIds = await _workflowMessageService
                .SendOrderPlacedCustomerNotificationAsync(order, order.CustomerLanguageId, orderPlacedAttachmentFilePath, orderPlacedAttachmentFileName);
            if (orderPlacedCustomerNotificationQueuedEmailIds.Any())
                await AddOrderNoteAsync(order, $"\"Order placed\" email (to customer) has been queued. Queued email identifiers: {string.Join(", ", orderPlacedCustomerNotificationQueuedEmailIds)}.");

            var vendors = await GetVendorsInOrderAsync(order);
            foreach (var vendor in vendors)
            {
                var orderPlacedVendorNotificationQueuedEmailIds = await _workflowMessageService.SendOrderPlacedVendorNotificationAsync(order, vendor, _localizationSettings.DefaultAdminLanguageId);
                if (orderPlacedVendorNotificationQueuedEmailIds.Any())
                    await AddOrderNoteAsync(order, $"\"Order placed\" email (to vendor) has been queued. Queued email identifiers: {string.Join(", ", orderPlacedVendorNotificationQueuedEmailIds)}.");
            }

            if (order.AffiliateId == 0)
                return;

            var orderPlacedAffiliateNotificationQueuedEmailIds = await _workflowMessageService.SendOrderPlacedAffiliateNotificationAsync(order, _localizationSettings.DefaultAdminLanguageId);
            if (orderPlacedAffiliateNotificationQueuedEmailIds.Any())
                await AddOrderNoteAsync(order, $"\"Order placed\" email (to affiliate) has been queued. Queued email identifiers: {string.Join(", ", orderPlacedAffiliateNotificationQueuedEmailIds)}.");
        }
    }
}
