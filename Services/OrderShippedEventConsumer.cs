﻿using System.Text;
using Nop.Core.Domain.Orders;
using Nop.Services.Events;
using Nop.Services.Orders;
using Nop.Core.Domain.Shipping;
using Unzer.Plugin.Payments.Unzer.Infrastructure;

namespace Unzer.Plugin.Payments.Unzer.Services
{
    public class OrderShippedEventConsumer : IConsumer<ShipmentSentEvent>
    {
        private readonly UnzerPaymentSettings _unzerPaymentSettings;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly OrderSettings _orderSettings;

        public OrderShippedEventConsumer(UnzerPaymentSettings unzerPaymentSettings,
            IOrderService orderService,
            IEnumerable<IOrderProcessingService> orderProcessingService,
            OrderSettings orderSettings)
        {
            _unzerPaymentSettings = unzerPaymentSettings;
            _orderService = orderService;
            _orderProcessingService = orderProcessingService.Count() == 1 ? orderProcessingService.FirstOrDefault() : orderProcessingService.FirstOrDefault(o => o is DelayedPlaceOrderProcessingService);
            _orderSettings  = orderSettings;
        }

        public async Task HandleEventAsync(ShipmentSentEvent eventMessage)
        {
            var order = await _orderService.GetOrderByIdAsync(eventMessage.Shipment.OrderId);
            if (order.PaymentMethodSystemName.Contains(UnzerPaymentDefaults.SystemName) && !_orderSettings.CompleteOrderWhenDelivered && (_unzerPaymentSettings.AutoCapture == AutoCapture.OnOrderShipped || _unzerPaymentSettings.AutoCapture == AutoCapture.AutoCapture))
            {
                if (await _orderProcessingService.CanCaptureAsync(order))
                {
                    var errors = await _orderProcessingService.CaptureAsync(order);

                    if (errors.Count > 0)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("UnzerPayment Capture on shipped: ERROR");
                        foreach (var error in errors)
                            sb.AppendLine(error);

                        // order note update
                        await AddOrderNoteAsync(order, sb.ToString());
                    }
                    else
                        await AddOrderNoteAsync(order, "UnzerPayment Capture on shipped: OK");
                }
            }
        }

        private async Task AddOrderNoteAsync(Order order, string note)
        {
            await _orderService.InsertOrderNoteAsync(new OrderNote
            {
                OrderId = order.Id,
                Note = note,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });
        }
    }
}