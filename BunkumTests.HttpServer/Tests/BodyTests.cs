using System.Net;
using Bunkum.Core;
using BunkumTests.HttpServer.Endpoints;

namespace BunkumTests.HttpServer.Tests;

public class BodyTests : ServerDependentTest
{
    [Test]
    [TestCase("/body/string")]
    [TestCase("/body/byteArray")]
    [TestCase("/body/stream")]
    public async Task CorrectResponseForAllTypes(string endpoint)
    {
        (BunkumServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<BodyEndpoints>();
        
        HttpResponseMessage msg = await client.PostAsync(endpoint, new StringContent("works"));
        Assert.Multiple(async () =>
        {
            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("works"));
        });
    }

    [Test]
    public async Task ReturnsBadRequestOnNoData()
    {
        (BunkumServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<BodyEndpoints>();
        
        HttpResponseMessage msg = await client.PostAsync("/body/string", null);
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task RejectsEmptyJsonBodyIfRequired()
    {
        (BunkumServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<BodyEndpoints>();
        
        // empty bodies are rejected
        HttpResponseMessage msg = await client.PostAsync("/body/json", null);
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // but valid bodies are accepted
        string requestBody = "{\"Field\":\"has body\"}";
        msg = await client.PostAsync("/body/json", new StringContent(requestBody));
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        string text = await msg.Content.ReadAsStringAsync();
        Assert.That(text, Is.EqualTo(requestBody));
    }

    [Test]
    public async Task AcceptsEmptyJsonBodyIfOptional()
    {
        (BunkumServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<BodyEndpoints>();
        
        // empty bodies are accepted
        HttpResponseMessage msg = await client.PostAsync("/body/json/optional", null);
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        string text = await msg.Content.ReadAsStringAsync();
        Assert.That(text, Is.EqualTo("{\"Field\":\"had no body\"}"));

        // and valid bodies are also accepted
        string requestBody = "{\"Field\":\"has body\"}";
        msg = await client.PostAsync("/body/json/optional", new StringContent(requestBody));
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        text = await msg.Content.ReadAsStringAsync();
        Assert.That(text, Is.EqualTo(requestBody));
    }

    [Test]
    public async Task RejectsEmptyXmlBodyIfRequired()
    {
        (BunkumServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<BodyEndpoints>();
        
        // empty bodies are rejected
        HttpResponseMessage msg = await client.PostAsync("/body/xml", null);
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // but valid bodies are accepted
        string requestBody = "<Serializable><Field>has body</Field></Serializable>";
        msg = await client.PostAsync("/body/xml", new StringContent(requestBody));
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        string text = await msg.Content.ReadAsStringAsync();
        Assert.That(text, Is.EqualTo(requestBody));
    }

    [Test]
    public async Task AcceptsEmptyXmlBodyIfOptional()
    {
        (BunkumServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<BodyEndpoints>();
        
        // empty bodies are accepted
        HttpResponseMessage msg = await client.PostAsync("/body/xml/optional", null);
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        string text = await msg.Content.ReadAsStringAsync();
        Assert.That(text, Is.EqualTo("<Serializable><Field>had no body</Field></Serializable>"));

        // and valid bodies are also accepted
        string requestBody = "<Serializable><Field>has body</Field></Serializable>";
        msg = await client.PostAsync("/body/xml/optional", new StringContent(requestBody));
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        text = await msg.Content.ReadAsStringAsync();
        Assert.That(text, Is.EqualTo(requestBody));
    }
}