using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// A handle to the network handler used by the Host's Session.
    /// </summary>
    public interface IHostSessionNetwork
    {
        /// <summary>
        /// State changed events will be fired when the internal state changes.
        /// </summary>
        event Action<NetworkState> StateChanged;

        /// <summary>
        /// Raised when the operation to start the network is cancelled.
        /// </summary>
        event Action<CancellationToken> StartCancelled;

        /// <summary>
        /// Raised when the operation to start the network fails.
        /// </summary>
        event Action<SessionError> StartFailed;

        /// <summary>
        /// Raised when the operation to stop the network fails.
        /// </summary>
        event Action<SessionError> StopFailed;

        /// <summary>
        /// Raised when the operation to migrate the network fails.
        /// </summary>
        event Action<SessionError> MigrationFailed;

        /// <summary>
        /// State of the underlying network handler.
        /// </summary>
        NetworkState State { get; }

        /// <summary>
        /// A handler that configures the network as a session start and stops.
        /// </summary>
        INetworkHandler NetworkHandler { get; set; }

        /// <summary>
        /// Starts the session associated network as a direct network using
        /// the provided options.
        /// </summary>
        /// <param name="networkOptions">
        /// The options used to configure the direct network,
        /// including port settings and other network parameters.
        /// </param>
        /// <returns>
        /// A task that will be completed when the network is started.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkOptions"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="SessionException">
        /// Thrown when the network is already started or when
        /// the network setup fails.
        /// </exception>
        /// <remarks>
        /// The default implementation forwards to <see
        /// cref="StartDirectNetworkAsync(DirectNetworkOptions, CancellationToken)"/>
        /// with <see cref="CancellationToken.None"/>, so the resulting
        /// operation cannot be cancelled.
        /// </remarks>
        Task StartDirectNetworkAsync(DirectNetworkOptions networkOptions)
        {
            return StartDirectNetworkAsync(networkOptions, CancellationToken.None);
        }

        /// <summary>
        /// Starts the session associated network as a
        /// direct network using the provided options.
        /// </summary>
        /// <param name="networkOptions">
        /// The options used to configure the direct network,
        /// including port settings and other network parameters.
        /// </param>
        /// <param name="cancelToken">
        /// Cancellation Token to cancel the request.
        /// </param>
        /// <returns>
        /// A task that will be completed when the network is started.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkOptions"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancelToken"/>
        /// is cancelled before the network is started.
        /// </exception>
        /// <exception cref="SessionException">
        /// Thrown when the network is already started or when
        /// the network setup fails.
        /// </exception>
        /// <remarks>
        /// When <paramref name="cancelToken"/> is cancelled,
        /// <see cref="StartCancelled"/> is raised and <see cref="State"/>
        /// returns to <see cref="NetworkState.Stopped"/>.
        /// </remarks>
        Task StartDirectNetworkAsync(DirectNetworkOptions networkOptions, CancellationToken cancelToken);

        /// <summary>
        /// Starts the session associated network as a relay network.
        /// </summary>
        /// <param name="networkOptions">
        /// The options used to configure the relay network.
        /// </param>
        /// <returns>
        /// A task that will be completed when the network is started.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkOptions"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="SessionException">
        /// Thrown when the network is already started or when
        /// the network setup fails.
        /// </exception>
        /// <remarks>
        /// The default implementation forwards to <see
        /// cref="StartRelayNetworkAsync(RelayNetworkOptions, CancellationToken)"/>
        /// with <see cref="CancellationToken.None"/>, so the resulting
        /// operation cannot be cancelled.
        /// </remarks>
        Task StartRelayNetworkAsync(RelayNetworkOptions networkOptions)
        {
            return StartRelayNetworkAsync(networkOptions, CancellationToken.None);
        }

        /// <summary>
        /// Starts the session associated network as a relay network.
        /// </summary>
        /// <param name="networkOptions">
        /// The options used to configure the relay network.
        /// </param>
        /// <param name="cancelToken">
        /// Cancellation Token to cancel the request.
        /// </param>
        /// <returns>
        /// A task that will be completed when the network is started.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkOptions"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancelToken"/>
        /// is cancelled before the network is started.
        /// </exception>
        /// <exception cref="SessionException">
        /// Thrown when the network is already started or when
        /// the network setup fails.
        /// </exception>
        /// <remarks>
        /// When <paramref name="cancelToken"/> is cancelled,
        /// <see cref="StartCancelled"/> is raised and <see cref="State"/>
        /// returns to <see cref="NetworkState.Stopped"/>.
        /// </remarks>
        Task StartRelayNetworkAsync(RelayNetworkOptions networkOptions, CancellationToken cancelToken);

#if GAMEOBJECTS_NETCODE_2_AVAILABLE || PACKAGE_DOCS_GENERATION
        /// <summary>
        /// Starts the session associated network as a distributed authority
        /// network.
        /// </summary>
        /// <param name="networkOptions">
        /// The options used to configure the relay network.
        /// </param>
        /// <returns>
        /// A task that will be completed when the network is started.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkOptions"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="SessionException">
        /// Thrown when the network is already started or when
        /// the network setup fails.
        /// </exception>
        /// <remarks>
        /// The default implementation forwards to <see
        /// cref="StartDistributedAuthorityNetworkAsync(RelayNetworkOptions, CancellationToken)"/>
        /// with <see cref="CancellationToken.None"/>, so the resulting
        /// operation cannot be cancelled.
        /// </remarks>
        Task StartDistributedAuthorityNetworkAsync(RelayNetworkOptions networkOptions)
        {
            return StartDistributedAuthorityNetworkAsync(networkOptions, CancellationToken.None);
        }

        /// <summary>
        /// Starts the session associated network as a distributed authority
        /// network.
        /// </summary>
        /// <param name="networkOptions">
        /// The options used to configure the relay network.
        /// </param>
        /// <param name="cancelToken">
        /// Cancellation Token to cancel the request.
        /// </param>
        /// <returns>
        /// A task that will be completed when the network is started.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="networkOptions"/>
        /// is <see langword="null"/>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancelToken"/>
        /// is cancelled before the network is started.
        /// </exception>
        /// <exception cref="SessionException">
        /// Thrown when the network is already started or when
        /// the network setup fails.
        /// </exception>
        /// <remarks>
        /// When <paramref name="cancelToken"/> is cancelled,
        /// <see cref="StartCancelled"/> is raised and <see cref="State"/>
        /// returns to <see cref="NetworkState.Stopped"/>.
        /// </remarks>
        Task StartDistributedAuthorityNetworkAsync(RelayNetworkOptions networkOptions, CancellationToken cancelToken);

#endif

        /// <summary>
        /// Stops the network.
        /// </summary>
        /// <returns>
        /// A task that will be completed after the network has been shut down.
        /// </returns>
        /// <exception cref="SessionException">
        /// Thrown when the network is not started or when
        /// stopping the network fails.
        /// </exception>
        Task StopNetworkAsync();

        /// <summary>
        /// The current network info
        /// </summary>
        internal NetworkInfo NetworkInfo { get; }
    }
}
