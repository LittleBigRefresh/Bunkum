using System.Net;
using Bunkum.CustomHttpListener.Parsing;
using Bunkum.HttpServer;
using Bunkum.HttpServer.Endpoints;
using Bunkum.HttpServer.Responses;
using BunkumTests.HttpServer.Tests;

namespace BunkumTests.HttpServer.Endpoints;

public class ResponseEndpoints : EndpointGroup
{
    [Endpoint("/response/string")]
    public string String(RequestContext context)
    {
        return "works";
    }
    
    [Endpoint("/response/responseObject")]
    public Response ResponseObject(RequestContext context)
    {
        return new Response("works", ContentType.Plaintext);
    }
    
    [Endpoint("/response/responseObjectWithCode")]
    public Response ResponseObjectWithCode(RequestContext context)
    {
        return new Response("works", ContentType.Plaintext, HttpStatusCode.Accepted); // random code lol
    }

    [Endpoint("/response/serializedXml", Method.Get, ContentType.Xml)]
    [Endpoint("/response/serializedJson", Method.Get, ContentType.Json)]
    public ResponseSerializationObject SerializedObject(RequestContext context)
    {
        return new ResponseSerializationObject();
    }
}