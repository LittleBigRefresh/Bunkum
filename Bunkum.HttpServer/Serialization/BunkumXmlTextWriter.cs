using System.Text;
using System.Xml;

namespace Bunkum.HttpServer.Serialization;

public class BunkumXmlTextWriter : XmlTextWriter
{
    public BunkumXmlTextWriter(Stream stream) : base(stream, Encoding.Default)
    { }

    public override void WriteEndElement()
    {
        base.WriteFullEndElement();
    }

    public override Task WriteEndElementAsync()
    {
        return base.WriteFullEndElementAsync();
    }

    public override void WriteStartDocument() {}
    public override void WriteStartDocument(bool standalone) {}
}