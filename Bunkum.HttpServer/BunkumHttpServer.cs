using System.Collections.Immutable;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Bunkum.HttpServer.Authentication;
using Bunkum.HttpServer.Authentication.Dummy;
using Bunkum.HttpServer.Configuration;
using Bunkum.HttpServer.Database;
using Bunkum.HttpServer.Database.Dummy;
using Bunkum.HttpServer.Endpoints;
using Bunkum.HttpServer.Extensions;
using Bunkum.HttpServer.Responses;
using Bunkum.HttpServer.Storage;
using JetBrains.Annotations;
using NotEnoughLogs;
using NotEnoughLogs.Loggers;

namespace Bunkum.HttpServer;

public class BunkumHttpServer
{
    private readonly HttpListener _listener;
    private readonly List<EndpointGroup> _endpoints = new();
    private readonly LoggerContainer<BunkumContext> _logger;

    private IAuthenticationProvider<IUser> _authenticationProvider = new DummyAuthenticationProvider();
    private IDatabaseProvider<IDatabaseContext> _databaseProvider = new DummyDatabaseProvider();
    private IDataStore _dataStore = new NullDataStore();
    
    private Config? _config;
    private Type? _configType;
    private BunkumConfig _bunkumConfig;

    public EventHandler<HttpListenerContext>? NotFound;

    public bool AssumeAuthenticationRequired = false;

    public BunkumHttpServer(params string[] listenEndpoints)
    {
        this._logger = new LoggerContainer<BunkumContext>();
        this._logger.RegisterLogger(new ConsoleLogger());
        
        this._listener = new HttpListener();
        this._listener.IgnoreWriteExceptions = true;
        foreach (string endpoint in listenEndpoints)
        {
            this._logger.LogInfo(BunkumContext.Startup, "Listening at URI " + endpoint);
            this._listener.Prefixes.Add(endpoint);
        }

        this._bunkumConfig = Config.LoadFromFile<BunkumConfig>("bunkum.json", this._logger);
    }

    public void Start()
    {
        this.RunStartupTasks();
        Task.Factory.StartNew(async () => await this.Block());
    }
    
    public async Task StartAndBlockAsync()
    {
        this.RunStartupTasks();
        await this.Block();
    }

    private void RunStartupTasks()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        
        this._logger.LogInfo(BunkumContext.Startup, "Starting up...");
        if (this._authenticationProvider is DummyAuthenticationProvider)
        {
            this._logger.LogWarning(BunkumContext.Startup, "The server was started with a dummy authentication provider. " +
                                                            "If your endpoints rely on authentication, users will always have full access.");
        }
        
        this._logger.LogDebug(BunkumContext.Startup, "Initializing database provider...");
        this._databaseProvider.Initialize();
        
        this._logger.LogDebug(BunkumContext.Startup, "Starting listener...");
        try
        {
            this._listener.Start();
        }
        catch(Exception e)
        {
            this._logger.LogCritical(BunkumContext.Startup, $"An exception occured while trying to start the listener: \n{e}");
            this._logger.LogCritical(BunkumContext.Startup, "Visit this page to view troubleshooting steps: " +
                                                             "https://littlebigrefresh.github.io/Docs/refresh-troubleshooting");
            
            this._logger.Dispose();
            BunkumConsole.WaitForInputAndExit(1);
        }

        stopwatch.Stop();
        this._logger.LogInfo(BunkumContext.Startup, $"Ready to go! Startup tasks took {stopwatch.ElapsedMilliseconds}ms.");
    }

    private async Task Block()
    {
        while (true)
        {
            HttpListenerContext context = await this._listener.GetContextAsync();

            await Task.Factory.StartNew(() =>
            {
                //Create a new lazy to get a database context, if the value is never accessed, a database instance is never passed
                Lazy<IDatabaseContext> database = new Lazy<IDatabaseContext>(this._databaseProvider.GetContext());
                //Handle the request
                this.HandleRequest(context, database);
                
                if(database.IsValueCreated)
                    database.Value.Dispose();
            });
        }
    }

    [Pure]
    private Response? InvokeEndpointByRequest(HttpListenerContext context, Lazy<IDatabaseContext> database)
    {
        foreach (EndpointGroup group in this._endpoints)
        {
            foreach (MethodInfo method in group.GetType().GetMethods())
            {
                ImmutableArray<EndpointAttribute> attributes = method.GetCustomAttributes<EndpointAttribute>().ToImmutableArray();
                if(attributes.Length == 0) continue;

                foreach (EndpointAttribute attribute in attributes)
                {
                    if (!attribute.UriMatchesRoute(
                            context.Request.Url,
                            MethodUtils.FromString(context.Request.HttpMethod),
                            out Dictionary<string, string> parameters))
                    {
                        continue;
                    }
                    
                    this._logger.LogTrace(BunkumContext.Request, $"Handling request with {group.GetType().Name}.{method.Name}");

                    IUser? user = null;
                    if (method.GetCustomAttribute<AuthenticationAttribute>()?.Required ?? this.AssumeAuthenticationRequired)
                    {
                        user = this._authenticationProvider.AuthenticateUser(context.Request, database.Value);
                        if (user == null)
                            return new Response(Array.Empty<byte>(), ContentType.Plaintext, HttpStatusCode.Forbidden);
                    }

                    HttpStatusCode nullCode = method.GetCustomAttribute<NullStatusCodeAttribute>()?.StatusCode ??
                                              HttpStatusCode.NotFound;

                    // Build list to invoke endpoint method with
                    List<object?> invokeList = new() { 
                        new RequestContext // 1st argument is always the request context. This is fact, and is backed by an analyzer.
                        {
                            Request = context.Request,
                            Logger = this._logger,
                            DataStore = this._dataStore,
                        },
                    };

                    // Next, lets iterate through the method's arguments and add some based on what we find.
                    foreach (ParameterInfo param in method.GetParameters().Skip(1))
                    {
                        Type paramType = param.ParameterType;

                        // Pass in the request body as a parameter
                        if (param.Name == "body")
                        {
                            // If the request has no body and we have a body parameter, then it's probably safe to assume it's required.
                            // Fire a bad request back if this is the case.
                            if (!context.Request.HasEntityBody)
                                return new Response(Array.Empty<byte>(), ContentType.Plaintext, HttpStatusCode.BadRequest);

                            MemoryStream body = new((int)context.Request.ContentLength64);
                            context.Request.InputStream.CopyTo(body);
                            body.Position = 0;
                            
                            if(paramType == typeof(Stream)) invokeList.Add(body);
                            else if(paramType == typeof(string)) invokeList.Add(Encoding.Default.GetString(body.GetBuffer()));
                            else if(paramType == typeof(byte[])) invokeList.Add(body.GetBuffer());
                            else if(attribute.ContentType == ContentType.Xml)
                            {
                                XmlSerializer serializer = new(paramType);
                                try
                                {
                                    object? obj = serializer.Deserialize(new StreamReader(body));
                                    if (obj == null) throw new Exception();
                                    invokeList.Add(obj);
                                }
                                catch (Exception e)
                                {
                                    this._logger.LogError(BunkumContext.UserContent, $"Failed to parse object data: {e}\n\nXML: {body}");
                                    return new Response(Array.Empty<byte>(), ContentType.Plaintext, HttpStatusCode.BadRequest);
                                }
                            }
                            // We can't find a valid type to send or deserialization failed
                            else return new Response(Array.Empty<byte>(), ContentType.Plaintext, HttpStatusCode.BadRequest);

                            continue;
                        }
                        
                        if (paramType.IsAssignableTo(typeof(IUser)))
                        {
                            // Users will always be non-null at this point. Once again, this is backed by an analyzer.
                            Debug.Assert(user != null);
                            invokeList.Add(user);
                        }
                        else if(paramType.IsAssignableTo(typeof(IDatabaseContext)))
                        {
                            // Pass in a database context if the endpoint needs one.
                            invokeList.Add(database.Value);
                        }
                        else if (paramType.IsAssignableTo(this._configType))
                        {
                            if (this._config == null)
                                throw new InvalidOperationException("A config was attempted to be passed into an endpoint, but there was no config set on startup!");
                            
                            invokeList.Add(this._config);
                        }
                        else if (paramType.IsAssignableTo(typeof(BunkumConfig)))
                        {
                            invokeList.Add(this._config);
                        }
                        else if (paramType == typeof(string))
                        {
                            // Attempt to pass in a route parameter based on the method parameter's name
                            invokeList.Add(parameters!.GetValueOrDefault(param.Name));
                        }
                    }

                    object? val = method.Invoke(group, invokeList.ToArray());

                    // ReSharper disable once ConvertSwitchStatementToSwitchExpression
                    switch (val)
                    {
                        case null:
                            return new Response(Array.Empty<byte>(), attribute.ContentType, nullCode);
                        case Response response:
                            return response;
                        default:
                            return new Response(val, attribute.ContentType);
                    }
                }
            }
        }

        return null;
    }

    private void HandleRequest(HttpListenerContext context, Lazy<IDatabaseContext> database)
    {
        Stopwatch requestStopwatch = new();
        requestStopwatch.Start();

        try
        {
            context.Response.AddHeader("Server", "Bunkum");

            string path = context.Request.Url!.AbsolutePath;

            Response? resp = this.InvokeEndpointByRequest(context, database);

            if (resp == null)
            {
                context.Response.AddHeader("Content-Type", ContentType.Plaintext.GetName());
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.WriteString("Not found: " + path);
                
                this.NotFound?.Invoke(this, context);
            }
            else
            {
                context.Response.AddHeader("Content-Type", resp.Value.ContentType.GetName());
                context.Response.StatusCode = (int)resp.Value.StatusCode;
                context.Response.OutputStream.Write(resp.Value.Data);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);

            try
            {
                context.Response.AddHeader("Content-Type", ContentType.Plaintext.GetName());
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

#if DEBUG
                context.Response.WriteString(e.ToString());
#else
                context.Response.WriteString("Internal Server Error");
#endif
            }
            catch
            {
                // ignored
            }
        }
        finally
        {
            try
            {
                requestStopwatch.Stop();

                this._logger.LogInfo(BunkumContext.Request, $"Served request to {context.Request.RemoteEndPoint}: " +
                                                          $"{context.Response.StatusCode} on " +
                                                          $"{context.Request.HttpMethod} '{context.Request.Url?.PathAndQuery}' " +
                                                          $"({requestStopwatch.ElapsedMilliseconds}ms)");

                context.Response.Close();
            }
            catch
            {
                // ignored
            }
        }
    }
    
    private void AddEndpointGroup(Type type)
    {
        EndpointGroup? doc = (EndpointGroup?)Activator.CreateInstance(type);
        Debug.Assert(doc != null);
        
        this._endpoints.Add(doc);
    }

    public void AddEndpointGroup<TDoc>() where TDoc : EndpointGroup => this.AddEndpointGroup(typeof(TDoc));

    public void DiscoverEndpointsFromAssembly(Assembly assembly)
    {
        List<Type> types = assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(EndpointGroup)))
            .ToList();

        foreach (Type type in types) this.AddEndpointGroup(type);
    }

    public void UseAuthenticationProvider(IAuthenticationProvider<IUser> provider)
    {
        this._authenticationProvider = provider;
    }

    public void UseDatabaseProvider(IDatabaseProvider<IDatabaseContext> provider)
    {
        this._databaseProvider = provider;
    }

    public void UseDataStore(IDataStore dataStore)
    {
        this._dataStore = dataStore;
    }

    // TODO: Configuration hot reload
    // TODO: .ini? would be helpful as it supports comments and we can document in the file itself
    public void UseJsonConfig<TConfig>(string filename) where TConfig : Config, new()
    {
        this._config = Config.LoadFromFile<TConfig>(filename, this._logger);
        this._configType = typeof(TConfig);
    }
}