using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unzer.Plugin.Payments.Unzer.Models.Api;

public class GetPaymentResponse : UnzerApiResponse
{
    public string id { get; set; }
    public State state { get; set; }
    public Amount amount { get; set; }
    public string currency { get; set; }
    public string orderId { get; set; }
    public string invoiceId { get; set; }
    public Resources resources { get; set; }
    public Transaction[] transactions { get; set; }
}

public class State
{
    public int id { get; set; }
    public string name { get; set; }
}

public class Amount
{
    public string total { get; set; }
    public string charged { get; set; }
    public string canceled { get; set; }
    public string remaining { get; set; }
}

public class Transaction
{
    public string date { get; set; }
    public string type { get; set; }
    public string status { get; set; }
    public string url { get; set; }
    public string amount { get; set; }
}
