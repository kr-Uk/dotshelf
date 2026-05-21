using System;
using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    [Serializable]
    public sealed class ApiErrorResponseDto
    {
        [JsonProperty("statusCode")]
        public long StatusCode { get; set; }

        [JsonProperty("httpStatus")]
        public string HttpStatus { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }
    }
}
