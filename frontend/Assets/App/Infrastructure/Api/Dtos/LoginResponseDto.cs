using System;
using Newtonsoft.Json;

namespace App.Infrastructure.Api.Dtos
{
    [Serializable]
    public sealed class LoginResponseDto
    {
        [JsonProperty("accessToken")]
        public string AccessToken { get; set; }

        [JsonProperty("nickname")]
        public string Nickname { get; set; }
    }
}
