using Newtonsoft.Json;

namespace Bunkum.HttpServer.Authentication.Dummy;

public class DummyUser : IUser
{
    [JsonProperty("userId")]
    public ulong UserId { get; set; } = 1;
    [JsonProperty("username")]
    public string Username { get; set; } = "Dummy";
}