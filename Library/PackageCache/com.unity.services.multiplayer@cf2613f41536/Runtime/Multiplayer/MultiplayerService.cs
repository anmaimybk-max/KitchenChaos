using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// Facade for session browsing, session management and matchmaking.
    /// </summary>
    public interface IMultiplayerService
    {
        /// <summary>
        /// Raised when a new session creation or joining request is initiated.
        /// </summary>
        public event Action<AddingSessionOptions> AddingSessionStarted;

        /// <summary>
        /// Raised when a new session creation or
        /// joining request is cancelled by the user.
        /// </summary>
        public event Action<AddingSessionOptions> AddingSessionCancelled;

        /// <summary>
        /// Raised when a new session creation or joining request fails.
        /// </summary>
        public event Action<AddingSessionOptions, SessionException> AddingSessionFailed;

        /// <summary>
        /// Raised when a new session is added to <see cref="Sessions"/>.
        /// </summary>
        public event Action<ISession> SessionAdded;

        /// <summary>
        /// Raised when an active session is removed from <see cref="Sessions"/>.
        /// </summary>
        public event Action<ISession> SessionRemoved;

        /// <summary>
        /// The list of active sessions
        /// </summary>
        public IReadOnlyDictionary<string, ISession> Sessions { get; }

        /// <summary>
        /// Creates a Session.
        /// </summary>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The created session</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<IHostSession> CreateSessionAsync(SessionOptions sessionOptions);

        /// <summary>
        /// Creates a Session.
        /// </summary>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to cancel the request.
        /// </param>
        /// <returns>The created session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        public Task<IHostSession> CreateSessionAsync(SessionOptions sessionOptions, CancellationToken cancellationToken);

        /// <summary>
        /// Tries to join a Session, creates it if no
        /// Session associated to the provided ID exists.
        /// </summary>
        /// <param name="sessionId">The Session ID</param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The created or joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<ISession> CreateOrJoinSessionAsync(string sessionId, SessionOptions sessionOptions);

        /// <summary>
        /// Tries to join a Session, creates it if no
        /// Session associated to the provided ID exists.
        /// </summary>
        /// <param name="sessionId">The Session ID.</param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to cancel the request.</param>
        /// <returns>The created or joined session.
        /// </returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        public Task<ISession> CreateOrJoinSessionAsync(string sessionId, SessionOptions sessionOptions, CancellationToken cancellationToken);

        /// <summary>
        /// Joins a Session by the session ID with default session options.
        /// </summary>
        /// <param name="sessionId">The ID for the session.</param>
        /// <returns>The joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <remarks>
        /// Equivalent to calling
        /// <see cref="JoinSessionByIdAsync(string, JoinSessionOptions)"/> with
        /// <see langword="null"/> options, which the session manager replaces
        /// with a default <see cref="JoinSessionOptions"/> instance.
        /// </remarks>
        public Task<ISession> JoinSessionByIdAsync(string sessionId)
        {
            return JoinSessionByIdAsync(sessionId, null);
        }

        /// <summary>
        /// Joins a Session by the session ID.
        /// </summary>
        /// <param name="sessionId">The ID for the session.</param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions sessionOptions);

        /// <summary>
        /// Joins a Session by the session ID.
        /// </summary>
        /// <param name="sessionId">The ID for the session.</param>
        /// <param name="cancellationToken">
        /// The cancellation token to cancel the request.
        /// </param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        /// <remarks>
        /// The default interface implementation forwards to <see
        /// cref="JoinSessionByIdAsync(string, JoinSessionOptions)"/>
        /// and ignores <paramref name="cancellationToken"/>.
        /// It exists so that existing implementations of <see
        /// cref="IMultiplayerService"/> keep compiling; implementers
        /// that support cancellation override this member, and only
        /// then is <see cref="OperationCanceledException"/> raised.
        /// </remarks>
        public Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions sessionOptions,
            CancellationToken cancellationToken)
        {
            return JoinSessionByIdAsync(sessionId, sessionOptions);
        }

        /// <summary>
        /// Joins a Session through a join code with default session options.
        /// </summary>
        /// <param name="sessionCode">The join code for the session.</param>
        /// <returns>The joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <remarks>
        /// Equivalent to calling
        /// <see cref="JoinSessionByCodeAsync(string, JoinSessionOptions)"/>
        /// with <see langword="null"/> options, which the session manager
        /// replaces with a default <see cref="JoinSessionOptions"/> instance.
        /// </remarks>
        public Task<ISession> JoinSessionByCodeAsync(string sessionCode)
        {
            return JoinSessionByCodeAsync(sessionCode, null);
        }

        /// <summary>
        /// Joins a Session through a join code.
        /// </summary>
        /// <param name="sessionCode">The join code for the session.</param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<ISession> JoinSessionByCodeAsync(string sessionCode, JoinSessionOptions sessionOptions);

        /// <summary>
        /// Joins a Session through a join code.
        /// </summary>
        /// <param name="sessionCode">The join code for the session.</param>
        /// <param name="cancellationToken">
        /// The cancellation token to cancel the request.
        /// </param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        /// <remarks>
        /// The default interface implementation forwards to <see
        /// cref="JoinSessionByCodeAsync(string, JoinSessionOptions)"/>
        /// and ignores <paramref name="cancellationToken"/>.
        /// It exists so that existing implementations of <see
        /// cref="IMultiplayerService"/> keep compiling; implementers
        /// that support cancellation override this member, and only
        /// then is <see cref="OperationCanceledException"/> raised.
        /// </remarks>
        public Task<ISession> JoinSessionByCodeAsync(string sessionCode, JoinSessionOptions sessionOptions, CancellationToken cancellationToken)
        {
            return JoinSessionByCodeAsync(sessionCode, sessionOptions);
        }

        /// <summary>
        /// Attempts to reconnect to an existing already joined
        /// session following a disconnect, without reconnection options.
        /// </summary>
        /// <param name="sessionId">The ID for the session.</param>
        /// <returns>The reconnected session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <remarks>
        /// Equivalent to calling
        /// <see cref="ReconnectToSessionAsync(string, ReconnectSessionOptions)"/>
        /// with <see langword="null"/> options. The session is then set up
        /// with a generated session type and without a network handler, so
        /// use the overload taking <see cref="ReconnectSessionOptions"/>
        /// to reconnect with a specific type or a network handler.
        /// </remarks>
        public Task<ISession> ReconnectToSessionAsync(string sessionId)
        {
            return ReconnectToSessionAsync(sessionId, null);
        }

        /// <summary>
        /// Attempts to reconnect to an existing already
        /// joined session following a disconnect.
        /// </summary>
        /// <param name="sessionId">The ID for the session.</param>
        /// <param name="options">The reconnection options.</param>
        /// <returns>The reconnected session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<ISession> ReconnectToSessionAsync(string sessionId, ReconnectSessionOptions options);

        /// <summary>
        /// Attempts to reconnect to an existing already
        /// joined session following a disconnect.
        /// </summary>
        /// <param name="sessionId">The ID for the session.</param>
        /// <param name="cancellationToken">
        /// The cancellation token to cancel the request.
        /// </param>
        /// <param name="options">The reconnection options.</param>
        /// <returns>The reconnected session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        /// <remarks>
        /// The default interface implementation forwards to <see
        /// cref="ReconnectToSessionAsync(string, ReconnectSessionOptions)"/>
        /// and ignores <paramref name="cancellationToken"/>.
        /// It exists so that existing implementations of <see
        /// cref="IMultiplayerService"/> keep compiling; implementers
        /// that support cancellation override this member, and only
        /// then is <see cref="OperationCanceledException"/> raised.
        /// </remarks>
        public Task<ISession> ReconnectToSessionAsync(string sessionId, ReconnectSessionOptions options, CancellationToken cancellationToken)
        {
            return ReconnectToSessionAsync(sessionId, options);
        }

        /// <summary>
        /// Find and join a Session with Unity matchmaker.
        /// </summary>
        /// <param name="matchOptions">
        /// The matchmaking queue options to join the Session.
        /// </param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The created or joined session.</returns>
        /// <exception cref="SessionException">
        /// <para>Provides a specific session error type and error message.</para>
        /// <para>-or-</para>
        /// <para>
        /// <paramref name="matchOptions"/> is <see langword="null"/>, reported
        /// as <see cref="SessionError.InvalidMatchmakerOptions"/>.
        /// </para>
        /// </exception>
        /// <remarks>
        /// Equivalent to calling
        /// <see cref="MatchmakeSessionAsync(MatchmakerOptions, SessionOptions, CancellationToken)"/>
        /// with <see cref="CancellationToken.None"/>, so the matchmaking
        /// ticket cannot be cancelled by the caller.
        /// </remarks>
        public Task<ISession> MatchmakeSessionAsync(MatchmakerOptions matchOptions, SessionOptions sessionOptions)
        {
            return MatchmakeSessionAsync(matchOptions, sessionOptions, CancellationToken.None);
        }

        /// <summary>
        /// Find and join a Session with Unity matchmaker.
        /// </summary>
        /// <param name="matchOptions">
        /// The matchmaking queue options to join the Session.
        /// </param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancellation Token to cancel the request.
        /// </param>
        /// <returns>The created or joined session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <remarks>
        /// Unlike the other cancellable session APIs, cancelling
        /// <paramref name="cancellationToken"/> on this overload does NOT throw
        /// <see cref="OperationCanceledException"/>. Instead, the matchmaker
        /// ticket is cancelled via the Matchmaker service and the call throws a
        /// <see cref="SessionException"/> with
        /// <see cref="SessionError.MatchmakerCancelled"/>. This matches the
        /// pre-existing behavior of <c>MatchmakerManager.StartAsync</c>, which
        /// converts the resulting <see cref="System.Threading.Tasks.TaskCanceledException"/>
        /// into that error code before it can propagate.
        /// </remarks>
        public Task<ISession> MatchmakeSessionAsync(MatchmakerOptions matchOptions, SessionOptions sessionOptions, CancellationToken cancellationToken);

        /// <summary>
        /// Find and join a Session using session filters. This operation will
        /// be retried at an interval up to a timeout specified in the options.
        /// Optionally creates a session if none is found after a timeout.
        /// </summary>
        /// <param name="quickJoinOptions">
        /// The quick join options used to find, join or create session.
        /// </param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <returns>The matchmade session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<ISession> MatchmakeSessionAsync(QuickJoinOptions quickJoinOptions, SessionOptions sessionOptions);

        /// <summary>
        /// Find and join a Session using session filters. This operation will
        /// be retried at an interval up to a timeout specified in the options.
        /// Optionally creates a session if none is found after a timeout.
        /// </summary>
        /// <param name="quickJoinOptions">
        /// The quick join options used to find, join or create session.
        /// </param>
        /// <param name="sessionOptions">
        /// The options for the resulting session.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to cancel the request.
        /// </param>
        /// <returns>The matchmade session.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        public Task<ISession> MatchmakeSessionAsync(QuickJoinOptions quickJoinOptions, SessionOptions sessionOptions, CancellationToken cancellationToken);

        /// <summary>
        /// Query available sessions
        /// </summary>
        /// <param name="queryOptions">The query options for the search</param>
        /// <returns>The result of the query with a list of sessions</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions queryOptions);

        /// <summary>
        /// Query available sessions.
        /// </summary>
        /// <param name="queryOptions">
        /// The query options for the search.
        /// </param>
        /// <param name="cancellationToken">
        /// The cancellation token to cancel the request.
        /// </param>
        /// <returns>The result of the query with a list of sessions.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="cancellationToken"/> is cancelled.
        /// </exception>
        public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions queryOptions, CancellationToken cancellationToken);

        /// <summary>
        /// Get a list of session IDs that the current player is part of.
        /// </summary>
        /// <returns>A list of sessions.</returns>
        /// <exception cref="SessionException">
        /// Provides a specific session error type and error message.
        /// </exception>
        public Task<List<string>> GetJoinedSessionIdsAsync();
    }

    /// <summary>
    /// The entry class of the Multiplayer SDK and session system.
    /// </summary>
    public static class MultiplayerService
    {
        /// <summary>
        /// A static instance of the Multiplayer service and session system.
        /// </summary>
        public static IMultiplayerService Instance { get; set; }
    }
}
