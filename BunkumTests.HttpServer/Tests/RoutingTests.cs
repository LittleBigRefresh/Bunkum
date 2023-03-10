using System.Net;
using Bunkum.CustomHttpListener.Parsing;
using Bunkum.HttpServer;
using Bunkum.HttpServer.Endpoints;
using Bunkum.HttpServer.Responses;
using BunkumTests.HttpServer.Endpoints;

namespace BunkumTests.HttpServer.Tests;

public class RoutingTests : ServerDependentTest
{
    [Test]
    public void IdentifiesFullRouteCorrectly()
    {
        EndpointAttribute attribute = new("/1234/1234");
        
        Assert.That(attribute.FullRoute, Is.EqualTo("/1234/1234"));
        bool matches = attribute.UriMatchesRoute(new Uri("/1234/1234"), Method.Get, out Dictionary<string, string> parameters);
        
        Assert.Multiple(() =>
        {
            Assert.That(matches, Is.True);
            Assert.That(parameters, Is.Empty);
        });
    }

    [Test]
    public void IdentifiesRouteParametersCorrectly()
    {
        EndpointAttribute attribute = new("/1234/1234/{param}");
        
        Assert.That(attribute.FullRoute, Is.EqualTo("/1234/1234/_"));
        
        bool matches = attribute.UriMatchesRoute(new Uri("/1234/1234/asdf"), Method.Get, out Dictionary<string, string> parameters);
        Assert.Multiple(() =>
        {
            Assert.That(matches, Is.True);
            Assert.That(parameters, Is.Not.Empty);
            Assert.That(parameters["param"], Is.EqualTo("asdf"));
        });
    }
    
    [Test]
    public async Task ReturnsCorrectEndpointWhenBothStartWithSameName()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<RouteStartsWithEndpoints>();
        
        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/sw/asdf"));
        Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("asdf"));
        
        msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/sw/a"));
        Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("a"));
    }

    [Test]
    // ReSharper disable StringLiteralTypo
    [TestCase("asdf")]
    [TestCase("{test}")]
    [TestCase("{input}")]
    [TestCase(";w'qas de'qed;'l.q';l3e2e")]
    // ReSharper restore StringLiteralTypo
    public async Task GetsRouteParameter(string text)
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<RouteParameterEndpoints>();
        
        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/param/" + text));
        Assert.Multiple(async () =>
        {
            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo(text));
        });
    }
    
    [Test]
    public async Task GetsMultipleRouteParameters()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<RouteParameterEndpoints>();
        
        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/params/asdf/fdsa"));
        Assert.Multiple(async () =>
        {
            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("asdf,fdsa"));
        });
    }
}