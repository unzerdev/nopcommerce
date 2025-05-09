using System.Net;
using System.Text.Json.Serialization;

namespace Unzer.Plugin.Payments.Unzer.Models.Api
{
    public class UnzerApiResponse : IUnzerApiResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }
        [JsonPropertyName("isPending")]
        public bool IsPending { get; set; }
        [JsonPropertyName("isError")]
        public bool IsError { get; set; }
        [JsonPropertyName("isResumed")]
        public bool IsResumed { get; set; }

        public HttpStatusCode HttpStatusCode { get; set; }
        public string RequestContent { get; set; }

        public UnzerApiErrorResponse ErrorResponse { get; set; }
    }
}
