namespace Unzer.Plugin.Payments.Unzer.Infrastructure
{
    public enum AutoCapture
    {
        None,
        OnOrderShipped,
        OnOrderDelivered,
        AutoCapture,
        OnAuthForDownloadableProduct,
        OnAuthForNoneDeliverProduct
    }

    public enum WebHookEventType
    {
        authorize,
        charge,
        payment,
        chargeback,
        payout
    }

    public enum PayPageMode
    {
        charge,
        authorize,
        preauthorize
    }

    public enum PayPageType
    {
        hosted,
        embedded,
        linkpay
    }

    public enum PayPageRecurrenceType
    {
        scheduled,
        unscheduled
    }
    public enum PayPageCheckoutType
    {
        full,
        payment_only,
        no_shipping
    }

    public enum CustomerType
    {
        B2B,
        B2C,
        ALL
    }
}
