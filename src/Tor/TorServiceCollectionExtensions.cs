using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Tor
{
    /// <summary>
    /// Extension methods for setting up Tor client services in an <see cref="IServiceCollection"/>.
    /// </summary>
    public static class TorServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a local Tor client using the provided configuration parameters.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="createParams">The creation parameters.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddTorClient(this IServiceCollection services, ClientCreateParams createParams)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (createParams == null)
                throw new ArgumentNullException(nameof(createParams));

            services.AddSingleton(createParams);
            services.AddSingleton<Client>(sp =>
            {
                var logger = sp.GetService<ILogger<Client>>();
                return Client.Create(createParams, logger);
            });

            return services;
        }

        /// <summary>
        /// Registers a remote Tor client connection using the provided connection parameters.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="remoteParams">The remote parameters.</param>
        /// <returns>The service collection.</returns>
        public static IServiceCollection AddTorClient(this IServiceCollection services, ClientRemoteParams remoteParams)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            if (remoteParams == null)
                throw new ArgumentNullException(nameof(remoteParams));

            services.AddSingleton(remoteParams);
            services.AddSingleton<Client>(sp =>
            {
                var logger = sp.GetService<ILogger<Client>>();
                return Client.CreateForRemote(remoteParams, logger);
            });

            return services;
        }
    }
}
