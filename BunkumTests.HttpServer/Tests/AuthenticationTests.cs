using System.Net;
using Bunkum.HttpServer;
using BunkumTests.HttpServer.Authentication;
using BunkumTests.HttpServer.Endpoints;

namespace BunkumTests.HttpServer.Tests;

public class AuthenticationTests : ServerDependentTest
{
    [Test]
    public async Task WorksWhenAuthenticated()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<AuthenticationEndpoints>();
        
        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/auth"));
        
        Assert.Multiple(async () =>
        {
            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("{\"userId\":1,\"username\":\"Dummy\"}"));
        });
    }
    
    [Test]
    public async Task FailsWhenNotAuthenticated()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<AuthenticationEndpoints>();
        
        client.DefaultRequestHeaders.Add("dummy-skip-auth", "true");
        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/auth"));
        
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}