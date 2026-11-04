using System.Reflection;

namespace Bunkum.Core.Endpoints;

public class LoadedEndpointData
{
    public readonly EndpointAttribute Attribute;
    public readonly MethodInfo Method;

    public LoadedEndpointData(EndpointAttribute attribute, MethodInfo method)
    {
        this.Attribute = attribute;
        this.Method = method;
    }
}