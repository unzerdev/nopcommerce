using System.Text.Json.Serialization;

namespace Unzer.Plugin.Payments.Unzer.Models.Api
{
    public class UnzerApiErrorResponse
    {
        [JsonPropertyName("id")]
        public string id { get; set; }
        [JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }
        [JsonPropertyName("isPending")]
        public bool IsPending { get; set; }
        [JsonPropertyName("isResumed")]
        public bool IsResumed { get; set; }
        [JsonPropertyName("isError")]
        public bool IsError { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("timestamp")]
        public string TimeStamp { get; set; }
        [JsonPropertyName("traceId")]
        public string TraceId { get; set; }
        [JsonPropertyName("responseCode")]
        public string responseCode { get; set; }
        [JsonPropertyName("responseMessage")]
        public string responseMessage { get; set; }
        [JsonPropertyName("fieldErrors")]
        public FieldError[] FieldErrors { get; set; }
        [JsonPropertyName("errors")]
        public Error[] Errors { get; set; }
    }

    public class Error
    {
        public string code { get; set; }
        public string merchantMessage { get; set; }
        public string customerMessage { get; set; }
    }

    public class FieldError
    {
        [JsonPropertyName("field")]
        public string Field { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
