using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    public sealed class ApiEnvelopeDto<T>
    {
        [JsonProperty("httpStatus")]
        public string HttpStatus { get; set; }

        [JsonProperty("isSuccess")]
        public bool IsSuccess { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }
    }
}
