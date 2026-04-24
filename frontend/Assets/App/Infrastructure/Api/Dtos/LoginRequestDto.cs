using System;
using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    [Serializable]
    public sealed class LoginRequestDto
    {
        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }
    }
}
