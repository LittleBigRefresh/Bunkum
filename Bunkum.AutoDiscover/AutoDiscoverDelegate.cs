using Bunkum.Core;

namespace Bunkum.AutoDiscover;

/// <summary>
/// The function to use to determine the URL.
/// </summary>
/// <param name="game">
/// The TitleID or identifier of the game to be patched.
/// </param>
/// <param name="context">
/// The <see cref="RequestContext"/> to use to obtain extra information on what URL to return, if wanted.
/// </param>
public delegate string AutoDiscoverDelegate(RequestContext context, string? game);