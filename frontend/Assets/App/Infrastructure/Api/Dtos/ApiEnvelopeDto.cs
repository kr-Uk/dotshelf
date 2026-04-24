using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    public sealed class ApiEnvelopeDto<T>
    {
        [JsonProperty("httpStatus")]
        public string HttpStatus { get; set; }

        [JsonProperty("isSuccess")]
        public bool IsSuccess { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonIgnore]
        public bool Succeeded => IsSuccess || Success;

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }
    }
}
