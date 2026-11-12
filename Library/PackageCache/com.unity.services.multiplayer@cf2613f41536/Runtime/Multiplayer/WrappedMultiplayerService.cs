using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_EDITOR
using Unity.Services.Core;
using UnityEditor;
#endif
using UnityEngine;

namespace Unity.Services.Multiplayer
{
    class WrappedMultiplayerService : IMultiplayerService
    {
        const string k_EnclosingType = nameof(WrappedMultiplayerService);

        /// <inheritdoc/>
        public event Action<AddingSessionOptions> AddingSessionStarted;

        /// <inheritdoc/>
        public event Action<AddingSessionOptions> AddingSessionCancelled;

        /// <inheritdoc/>
        public event Action<AddingSessionOptions, SessionException> AddingSessionFailed;

        /// <inheritdoc/>
        public event Action<ISession> SessionAdded
        {
            add => m_SessionManager.SessionAdded += value;
            remove => m_SessionManager.SessionAdded -= value;
        }

        /// <inheritdoc/>
        public event Action<ISession> SessionRemoved
        {
            add => m_SessionManager.SessionRemoved += value;
            remove => m_SessionManager.SessionRemoved -= value;
        }

        /// <inheritdoc/>
        IReadOnlyDictionary<string, ISession> IMultiplayerService.Sessions => m_SessionManager.Sessions;

        readonly ISessionQuerier m_SessionQuerier;
        readonly ISessionManager m_SessionManager;
        readonly IMatchmakerManager m_MatchmakerManager;
        readonly IModuleRegistry m_ModuleRegistry;

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="WrappedMultiplayerService"/> class.
        /// </summary>
        /// <param name="sessionQuerier">
        /// The querier used to search for available sessions.
        /// </param>
        /// <param name="sessionManager">
        /// The manager used to create, join, and reconnect to sessions.
        /// </param>
        /// <param name="matchmakerManager">
        /// The manager used to matchmake sessions.
        /// </param>
        /// <param name="moduleRegistry">
        /// The registry of module providers available to the service.
        /// </param>
        internal WrappedMultiplayerService(
            ISessionQuerier sessionQuerier,
            ISessionManager sessionManager,
            IMatchmakerManager matchmakerManager,
            IModuleRegistry moduleRegistry)
        {
            m_SessionQuerier = sessionQuerier;
            m_SessionManager = sessionManager;
            m_MatchmakerManager = matchmakerManager;
            m_ModuleRegistry = moduleRegistry;
        }

        /// <inheritdoc/>
        public Task<IHostSession> CreateSessionAsync(SessionOptions sessionOptions)
            => CreateSessionAsync(sessionOptions, CancellationToken.None);

        /// <inheritdoc/>
        public async Task<IHostSession> CreateSessionAsync(SessionOptions sessionOptions, CancellationToken cancellationToken)
        {
            var addingSessionOptions = new AddingSessionOptions(sessionOptions?.Type);
            AddingSessionStarted?.Invoke(addingSessionOptions);

            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    var sessionHandler = await m_SessionManager.CreateAsync(sessionOptions, Application.exitCancellationToken);
                    return sessionHandler.AsHost();
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                var linkedSessionHandler = await m_SessionManager.CreateAsync(sessionOptions, linkedTokenSource.Token);
                return linkedSessionHandler.AsHost();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                     Application.exitCancellationToken.IsCancellationRequested)
            {
                AddingSessionCancelled?.Invoke(addingSessionOptions);
                throw;
            }
            catch (Exception e)
            {
                throw HandleException(addingSessionOptions, e);
            }
        }

        /// <inheritdoc/>
        public Task<ISession> CreateOrJoinSessionAsync(string sessionId, SessionOptions sessionOptions)
            => CreateOrJoinSessionAsync(sessionId, sessionOptions, CancellationToken.None);

        /// <inheritdoc/>
        public async Task<ISession> CreateOrJoinSessionAsync(string sessionId, SessionOptions sessionOptions, CancellationToken cancellationToken)
        {
            var addingSessionOptions = new AddingSessionOptions(sessionOptions?.Type);
            AddingSessionStarted?.Invoke(addingSessionOptions);

            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await m_SessionManager.CreateOrJoinAsync(sessionId, sessionOptions, Application.exitCancellationToken);
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                return await m_SessionManager.CreateOrJoinAsync(sessionId, sessionOptions, linkedTokenSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                     Application.exitCancellationToken.IsCancellationRequested)
            {
                AddingSessionCancelled?.Invoke(addingSessionOptions);
                throw;
            }
            catch (Exception e)
            {
                throw HandleException(addingSessionOptions, e);
            }
        }

        /// <inheritdoc/>
        public Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions sessionOptions)
            => JoinSessionByIdAsync(sessionId, sessionOptions, CancellationToken.None);

        /// <inheritdoc/>
        public async Task<ISession> JoinSessionByIdAsync(string sessionId, JoinSessionOptions sessionOptions, CancellationToken cancellationToken)
        {
            var addingSessionOptions = new AddingSessionOptions(sessionOptions?.Type);
            AddingSessionStarted?.Invoke(addingSessionOptions);

            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await m_SessionManager.JoinByIdAsync(sessionId, sessionOptions, Application.exitCancellationToken);
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                return await m_SessionManager.JoinByIdAsync(sessionId, sessionOptions, linkedTokenSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                     Application.exitCancellationToken.IsCancellationRequested)
            {
                AddingSessionCancelled?.Invoke(addingSessionOptions);
                throw;
            }
            catch (Exception e)
            {
                throw HandleException(addingSessionOptions, e);
            }
        }

        /// <inheritdoc/>
        public Task<ISession> JoinSessionByCodeAsync(string sessionCode, JoinSessionOptions sessionOptions)
            => JoinSessionByCodeAsync(sessionCode, sessionOptions, CancellationToken.None);

        /// <inheritdoc/>
        public async Task<ISession> JoinSessionByCodeAsync(string sessionCode, JoinSessionOptions sessionOptions, CancellationToken cancellationToken)
        {
            var addingSessionOptions = new AddingSessionOptions(sessionOptions?.Type);
            AddingSessionStarted?.Invoke(addingSessionOptions);

            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await m_SessionManager.JoinByCodeAsync(sessionCode, sessionOptions, Application.exitCancellationToken);
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                return await m_SessionManager.JoinByCodeAsync(sessionCode, sessionOptions, linkedTokenSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                     Application.exitCancellationToken.IsCancellationRequested)
            {
                AddingSessionCancelled?.Invoke(addingSessionOptions);
                throw;
            }
            catch (Exception e)
            {
                throw HandleException(addingSessionOptions, e);
            }
        }

        /// <inheritdoc/>
        public Task<ISession> ReconnectToSessionAsync(string sessionId, ReconnectSessionOptions options)
            => ReconnectToSessionAsync(sessionId, options, CancellationToken.None);

        /// <inheritdoc/>
        public async Task<ISession> ReconnectToSessionAsync(string sessionId, ReconnectSessionOptions options, CancellationToken cancellationToken)
        {
            var addingSessionOptions = new AddingSessionOptions(options?.Type);
            AddingSessionStarted?.Invoke(addingSessionOptions);

            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await m_SessionManager.ReconnectAsync(sessionId, options, Application.exitCancellationToken);
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                return await m_SessionManager.ReconnectAsync(sessionId, options, linkedTokenSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                     Application.exitCancellationToken.IsCancellationRequested)
            {
                AddingSessionCancelled?.Invoke(addingSessionOptions);
                throw;
            }
            catch (Exception e)
            {
                throw HandleException(addingSessionOptions, e);
            }
        }

        /// <inheritdoc/>
        public Task<ISession> MatchmakeSessionAsync(QuickJoinOptions quickJoinOptions, SessionOptions sessionOptions)
            => MatchmakeSessionAsync(quickJoinOptions, sessionOptions, CancellationToken.None);

        /// <inheritdoc/>
        public async Task<ISession> MatchmakeSessionAsync(QuickJoinOptions quickJoinOptions, SessionOptions sessionOptions, CancellationToken cancellationToken)
        {
            var addingSessionOptions = new AddingSessionOptions(sessionOptions?.Type);
            AddingSessionStarted?.Invoke(addingSessionOptions);

            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await m_SessionManager.QuickJoinAsync(quickJoinOptions, sessionOptions, Application.exitCancellationToken);
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                return await m_SessionManager.QuickJoinAsync(quickJoinOptions, sessionOptions, linkedTokenSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                     Application.exitCancellationToken.IsCancellationRequested)
            {
                AddingSessionCancelled?.Invoke(addingSessionOptions);
                throw;
            }
            catch (Exception e)
            {
                throw HandleException(addingSessionOptions, e);
            }
        }

        /// <inheritdoc/>
        public async Task<ISession> MatchmakeSessionAsync(MatchmakerOptions matchOptions, SessionOptions sessionOptions, CancellationToken cancellationToken)
        {
            var addingSessionOptions = new AddingSessionOptions(sessionOptions?.Type);
            AddingSessionStarted?.Invoke(addingSessionOptions);

            try
            {
                if (matchOptions == null)
                {
                    throw new SessionException("MatchmakerOptions cannot be null.", SessionError.InvalidMatchmakerOptions);
                }

                if (!cancellationToken.CanBeCanceled)
                {
                    return await m_MatchmakerManager.StartAsync(matchOptions, sessionOptions?.WithMatchmaker(), Application.exitCancellationToken);
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                return await m_MatchmakerManager.StartAsync(matchOptions, sessionOptions?.WithMatchmaker(), linkedTokenSource.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested ||
                                                     Application.exitCancellationToken.IsCancellationRequested)
            {
                AddingSessionCancelled?.Invoke(addingSessionOptions);
                throw;
            }
            catch (Exception e)
            {
                throw HandleException(addingSessionOptions, e);
            }
        }

        /// <inheritdoc/>
        public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions queryOptions)
            => QuerySessionsAsync(queryOptions, CancellationToken.None);

        /// <inheritdoc/>
        public async Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions queryOptions, CancellationToken cancellationToken)
        {
            try
            {
                if (!cancellationToken.CanBeCanceled)
                {
                    return await m_SessionQuerier.QueryAsync(queryOptions, Application.exitCancellationToken);
                }

                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Application.exitCancellationToken);
                return await m_SessionQuerier.QueryAsync(queryOptions, linkedTokenSource.Token);
            }
            catch (Exception e) when (e is not SessionException && e is not OperationCanceledException)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        /// <inheritdoc/>
        public async Task<List<string>> GetJoinedSessionIdsAsync()
        {
            try
            {
                return await m_SessionManager.GetJoinedSessionIdsAsync();
            }
            catch (Exception e) when (e is not SessionException)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        /// <summary>
        /// Converts <paramref name="e"/> into a <see cref="SessionException"/>
        /// and raises <see cref="AddingSessionFailed"/>.
        /// </summary>
        /// <param name="addingSessionOptions">
        /// The options describing the session
        /// creation or joining attempt that failed.
        /// </param>
        /// <param name="e">The exception thrown by the attempt.</param>
        /// <returns>
        /// <paramref name="e"/> if it is already a <see
        /// cref="SessionException"/>; otherwise, a new <see
        /// cref="SessionException"/> wrapping <see cref="Exception.Message"/>
        /// with <see cref="SessionError.Unknown"/>.
        /// </returns>
        SessionException HandleException(AddingSessionOptions addingSessionOptions, Exception e)
        {
            // In the case of aggregated exceptions or other
            // exception types, we rethrow as SessionException
            SessionException sessionException;
            if (e is SessionException exception)
            {
                sessionException = exception;
            }
            else
            {
                sessionException = new SessionException(e.Message, SessionError.Unknown);
            }

            AddingSessionFailed?.Invoke(addingSessionOptions, sessionException);

            return sessionException;
        }
    }
}
