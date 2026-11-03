using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Animaze.Client;

/// <summary>DI extensions for <see cref="AnimazeClient"/>.</summary>
public static class AnimazeServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="AnimazeClient"/> wired against the supplied
    /// <see cref="AnimazeClientOptions"/>.
    /// </summary>
    public static IServiceCollection AddAnimazeClient(
        this IServiceCollection services,
        Action<AnimazeClientOptions>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is not null)
        {
            _ = services.Configure(configure);
        }
        services.TryAddSingleton(sp =>
        {
            AnimazeClientOptions options =
                sp.GetService<IOptions<AnimazeClientOptions>>()?.Value
                ?? new AnimazeClientOptions();
            ILogger<AnimazeClient>? logger = sp.GetService<ILogger<AnimazeClient>>();
            return new AnimazeClient(options, logger);
        });
        return services;
    }
}
