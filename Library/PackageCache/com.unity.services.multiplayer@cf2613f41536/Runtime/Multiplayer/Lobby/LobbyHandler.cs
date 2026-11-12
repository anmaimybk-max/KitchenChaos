using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication.Internal;
using Unity.Services.Core.Scheduler.Internal;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Internal;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Unity.Services.Multiplayer
{
    interface ILobbyBuilder
    {
        ILobbyHandler Build();
    }

    class LobbyBuilder : ILobbyBuilder
    {
        readonly IActionScheduler m_ActionScheduler;
        readonly ILobbyServiceInternal m_LobbyService;
        readonly IPlayerId m_PlayerId;
        readonly IServiceID m_ServiceId;
        readonly IAccessToken m_AccessToken;
        readonly IAccessTokenObserver m_AccessTokenObserver;
        readonly bool m_UsePolling;

        public LobbyBuilder(
            IActionScheduler mActionScheduler,
            ILobbyServiceInternal mLobbyService,
            IPlayerId mPlayerId,
            IServiceID mServiceID,
            IAccessToken mAccessToken,
            IAccessTokenObserver mAccessTokenObserver,
            bool usePolling)
        {
            m_ActionScheduler = mActionScheduler;
            m_LobbyService = mLobbyService;
            m_PlayerId = mPlayerId;
            m_ServiceId = mServiceID;
            m_AccessToken = mAccessToken;
            m_AccessTokenObserver = mAccessTokenObserver;
            m_UsePolling = usePolling;
        }

        public ILobbyHandler Build()
        {
            return new LobbyHandler(
                m_ActionScheduler,
                m_LobbyService,
                m_PlayerId,
                m_ServiceId,
                m_AccessToken,
                m_AccessTokenObserver,
                m_UsePolling);
        }
    }

    internal interface ILobbyHandler
    {
        public event Action LobbyChanged;
        public event Action LobbyExit;
        public event Action<string> PlayerJoined;
        [Obsolete("PlayerLeft has been deprecated. Use PlayerLeaving instead")]
        public event Action<string> PlayerLeft;
        public event Action<string> PlayerLeaving;
        public event Action<string> PlayerHasLeft;
        public event Action DataChanged;
        public event Action PlayerDataChanged;
        public event Action KickedFromLobby;
        public event Action LobbyDeleted;
        public event Action<string> LobbyHostChanged;

        public bool ConcurrencyControlEnabled { get; set; }

        public bool IsAuthorized { get; }
        public bool IsHost { get; }
        public bool IsServer { get; }
        public bool IsMember { get; }

        public Lobby Lobby { get; }
        public LobbyState State { get; }
        public MigrationDataInfo MigrationDataInfo { get; }

        public void AssignLobby(Lobby lobby, LobbyState state);
        public Task CreateLobbyAsync(string lobbyName, int maxPlayers, CreateLobbyOptions options = null,
            CancellationToken cancellationToken = default);

        public Task CreateOrJoinLobbyAsync(string id, string lobbyName, int maxPlayers,
            CreateLobbyOptions options = null, CancellationToken cancellationToken = default);

        public Task RefreshLobbyAsync();
        public Task GetLobbyAsync(string lobbyId, CancellationToken cancellationToken = default);
        public Task UpdateLobbyAsync(UpdateLobbyOptions updateLobbyOptions);
        public Task UpdateLobbyDataAsync(Dictionary<string, DataObject> data);
        public Task UpdateCurrentPlayerAsync(UpdatePlayerOptions updatePlayerOptions);
        public Task UpdatePlayerAsync(string playerId, UpdatePlayerOptions updatePlayerOptions);
        public Task DeleteLobbyAsync();
        public Task JoinLobbyByIdAsync(string lobbyId, JoinLobbyByIdOptions options = null,
            CancellationToken cancellationToken = default);
        public Task JoinLobbyByCodeAsync(string lobbyCode, JoinLobbyByCodeOptions options = null,
            CancellationToken cancellationToken = default);
        public Task QuickJoinLobbyAsync(QuickJoinLobbyOptions options, CancellationToken cancellationToken = default);
        public Task<List<string>> GetJoinedLobbiesAsync(CancellationToken cancellationToken = default);
        public Task ReconnectToLobbyAsync(string lobbyId = null, CancellationToken cancellationToken = default);
        public Task RemovePlayerAsync(string playerId);
        public Task UploadMigrationDataAsync(byte[] data, TimeSpan timeout);
        public Task<LobbyMigrationData> DownloadMigrationDataAsync(TimeSpan timeout);
        public Task SubscribeToLobbyEventsAsync(LobbyEventCallbacks callbacks);
        public Task ResetAsync();
    }

    internal class LobbyHandler : ILobbyHandler
    {
        public event Action LobbyChanged;
        public event Action LobbyExit;
        public event Action<string> PlayerJoined;
        public event Action<string> PlayerLeft;
        public event Action<string> PlayerLeaving;
        public event Action<string> PlayerHasLeft;
        public event Action DataChanged;
        public event Action PlayerDataChanged;
        public event Action KickedFromLobby;
        public event Action LobbyDeleted;
        public event Action<string> LobbyHostChanged;

        public bool IsAuthorized => m_AccessToken.AccessToken != null;

        public bool IsHost
        {
            get
            {
                if (Lobby == null)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(Lobby.HostId))
                {
                    return false;
                }

                if (m_PlayerId != null)
                {
                    return Lobby.HostId.Equals(m_PlayerId.PlayerId);
                }

                if (m_ServiceID != null)
                {
                    return Lobby.HostId.Equals(m_ServiceID.ServiceID);
                }

                return false;
            }
        }

        public bool IsServer => m_ServiceID?.ServiceID != null;

        public bool IsMember => Lobby?.Players?.Exists(player => player.Id.Equals(m_PlayerId?.PlayerId)) ?? false;

        public Lobby Lobby { get; private set; }
        public LobbyState State { get; private set; }
        public MigrationDataInfo MigrationDataInfo { get; private set; }
        public bool ConcurrencyControlEnabled { get; set; }

        internal long? m_HeartbeatActionId;
        internal long? m_PollingActionId;
        internal LobbyEventCallbacks m_Callbacks;
        internal ILobbyEvents m_Events;

        static readonly TimeSpan k_PollingDelay = TimeSpan.FromSeconds(1d);
        static readonly TimeSpan k_MigrationDataExpiryDelay = TimeSpan.FromSeconds(5d);

        readonly LobbySettings Settings = new LobbySettings();

        readonly IActionScheduler m_ActionScheduler;
        readonly ILobbyServiceInternal m_LobbyService;
        readonly IPlayerId m_PlayerId;
        readonly IServiceID m_ServiceID;
        readonly IAccessToken m_AccessToken;
        readonly IAccessTokenObserver m_AccessTokenObserver;
        readonly bool m_UsePolling;

        internal LobbyHandler(
            IActionScheduler actionScheduler,
            ILobbyServiceInternal lobbyService,
            IPlayerId playerId,
            IServiceID serviceID,
            IAccessToken accessToken,
            IAccessTokenObserver accessTokenObserver,
            bool usePolling)
        {
            m_ActionScheduler = actionScheduler;
            m_LobbyService = lobbyService;
            m_PlayerId = playerId;
            m_ServiceID = serviceID;
            m_AccessToken = accessToken;
            m_AccessTokenObserver = accessTokenObserver;
            m_UsePolling = usePolling;
        }

        public void AssignLobby(Lobby lobby, LobbyState state)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            Lobby = lobby;
            State = state;

            RegisterPlayerEvents();

            if (IsHost)
            {
                ScheduleHeartbeat();
            }

            LobbyChanged?.Invoke();
        }

        internal LobbyEventCallbacks LobbyCallbacks()
        {
            m_Callbacks = new LobbyEventCallbacks();
            m_Callbacks.LobbyChanged += OnLobbyChanged;
            m_Callbacks.PlayerJoined += OnPlayerJoined;
            m_Callbacks.DataChanged += OnDataChanged;
            m_Callbacks.DataRemoved += OnDataChanged;
            m_Callbacks.PlayerDataAdded += OnPlayerDataChanged;
            m_Callbacks.PlayerDataChanged += OnPlayerDataChanged;
            m_Callbacks.PlayerDataRemoved += OnPlayerDataChanged;
            m_Callbacks.LobbyDeleted += OnLobbyDeleted;
            m_Callbacks.KickedFromLobby += OnKickedFromLobby;

            return m_Callbacks;
        }

        async Task LobbyUnsubscribeCallbacksAsync()
        {
            if (m_Callbacks == null) return;

            m_Callbacks.KickedFromLobby -= OnKickedFromLobby;
            m_Callbacks.LobbyChanged -= OnLobbyChanged;
            m_Callbacks.PlayerJoined -= OnPlayerJoined;
            m_Callbacks.DataChanged -= OnDataChanged;
            m_Callbacks.DataRemoved -= OnDataChanged;
            m_Callbacks.PlayerDataChanged -= OnPlayerDataChanged;
            m_Callbacks.PlayerDataRemoved -= OnPlayerDataChanged;
            m_Callbacks.LobbyDeleted -= OnLobbyDeleted;
            m_Callbacks.KickedFromLobby -= OnKickedFromLobby;

            if (m_Events != null)
            {
                await m_Events.UnsubscribeAsync();
            }
        }

        internal void OnKickedFromLobby()
        {
            CancelPolling();
            KickedFromLobby?.Invoke();
        }

        internal void OnLobbyDeleted()
        {
            CancelPolling();
            LobbyDeleted?.Invoke();
        }

        internal void OnLobbyChanged(ILobbyChanges changes)
        {
            Logger.LogVerbose("OnLobbyChanged");
            if (changes == null)
            {
                return;
            }

            // handle player leaving here to avoid race on local lobby update removing the PlayerLeft event index
            var playerIdsLeaving = new List<string>();
            if (changes.PlayerLeft.Changed || changes.PlayerLeft.Added)
            {
                playerIdsLeaving = OnPlayerLeaving(changes.PlayerLeft.Value);
            }

            // if the lobby was not deleted OR the change version is not older than the current lobby version, apply the
            // changes and invoke the callback
            if (!changes.LobbyDeleted || (changes.Version.Changed && changes.Version.Value >= Lobby.Version))
            {
                // apply server side changes to the local lobby object
                changes.ApplyToLobby(Lobby);
                LobbyChanged?.Invoke();
            }

            if (changes.HostId.Changed || changes.HostId.Added)
            {
                LobbyHostChanged?.Invoke(changes.HostId.Value);
            }

            // Now that the changes have been applied to the Lobby, invoke the events
            OnPlayerHasLeft(playerIdsLeaving);

            // If the local player is the new host, start heartbeating
            if (IsHost && !m_HeartbeatActionId.HasValue)
            {
                ScheduleHeartbeat();
            }
        }

        internal void OnDataChanged(Dictionary<string, ChangedOrRemovedLobbyValue<DataObject>> _)
        {
            DataChanged?.Invoke();
        }

        internal void OnPlayerDataChanged(
            Dictionary<int, Dictionary<string, ChangedOrRemovedLobbyValue<PlayerDataObject>>> _)
        {
            PlayerDataChanged?.Invoke();
        }

        internal void OnPlayerJoined(List<LobbyPlayerJoined> players)
        {
            if (PlayerJoined == null) return;

            foreach (var player in players)
            {
                PlayerJoined.Invoke(player.Player.Id);
            }
        }

        internal List<string> OnPlayerLeaving(List<int> players)
        {
            var playerIdsLeaving = new List<string>();
            foreach (var playerIndex in players.Where(playerIndex => Lobby?.Players?.Count > playerIndex))
            {
                // Here, the changes have not been applied to the Lobby yet
                PlayerLeft?.Invoke(Lobby.Players[playerIndex].Id);
                PlayerLeaving?.Invoke(Lobby.Players[playerIndex].Id);
                playerIdsLeaving.Add(Lobby.Players[playerIndex].Id);
            }

            return playerIdsLeaving;
        }

        internal void OnPlayerHasLeft(List<string> playerIdsLeaving)
        {
            foreach (var playerId in playerIdsLeaving)
            {
                PlayerHasLeft?.Invoke(playerId);
            }
        }

        public async Task CreateLobbyAsync(string lobbyName, int maxPlayers, CreateLobbyOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lobby = await m_LobbyService.CreateLobbyAsync(lobbyName, maxPlayers, options);
                AssignLobby(lobby, LobbyState.Joined);
                await InitLobbyEventsAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        async Task InitLobbyEventsAsync()
        {
            if (m_UsePolling)
            {
                await Task.Run(() =>
                {
                    PollForLobbyEvents(LobbyCallbacks());
                }, Application.exitCancellationToken);
            }
            else
            {
                await SubscribeToLobbyEventsAsync(LobbyCallbacks());
            }
        }

        public async Task CreateOrJoinLobbyAsync(string id, string name, int maxPlayers,
            CreateLobbyOptions options = null, CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lobby = await m_LobbyService.CreateOrJoinLobbyAsync(id, name, maxPlayers, options);
                AssignLobby(lobby, LobbyState.Joined);
                await InitLobbyEventsAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task RefreshLobbyAsync()
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            try
            {
                Lobby = await m_LobbyService.GetLobbyAsync(Lobby.Id);
                LobbyChanged?.Invoke();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task GetLobbyAsync(string lobbyId, CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lobby = await m_LobbyService.GetLobbyAsync(lobbyId);
                AssignLobby(lobby, LobbyState.Joined);
                await InitLobbyEventsAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task UpdateLobbyAsync(UpdateLobbyOptions updateLobbyOptions)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            try
            {
                var newLobby = await m_LobbyService.UpdateLobbyAsync(Lobby.Id, updateLobbyOptions, ConcurrencyControlEnabled);
                if (newLobby.Version > Lobby.Version)
                {
                    Lobby = newLobby;
                    LobbyChanged?.Invoke();
                }
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task UpdateLobbyDataAsync(Dictionary<string, DataObject> data)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            try
            {
                Lobby = await m_LobbyService.UpdateLobbyAsync(Lobby.Id, new UpdateLobbyOptions() { Data = data }, ConcurrencyControlEnabled);
                LobbyChanged?.Invoke();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task UpdateCurrentPlayerAsync(UpdatePlayerOptions updatePlayerOptions)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            try
            {
                Lobby = await m_LobbyService.UpdatePlayerAsync(Lobby.Id, m_PlayerId?.PlayerId, updatePlayerOptions, ConcurrencyControlEnabled);
                LobbyChanged?.Invoke();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task UpdatePlayerAsync(string playerId, UpdatePlayerOptions updatePlayerOptions)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            try
            {
                Lobby = await m_LobbyService.UpdatePlayerAsync(Lobby.Id, playerId, updatePlayerOptions, ConcurrencyControlEnabled);
                LobbyChanged?.Invoke();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task DeleteLobbyAsync()
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            try
            {
                await m_LobbyService.DeleteLobbyAsync(Lobby.Id, ConcurrencyControlEnabled);
            }
            catch (LobbyServiceException lobbyException)
            {
                // Do not throw if lobby does not exists
                if (lobbyException.Reason != LobbyExceptionReason.LobbyNotFound)
                    throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }

            try
            {
                await ResetAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task JoinLobbyByIdAsync(string lobbyId, JoinLobbyByIdOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lobby = await m_LobbyService.JoinLobbyByIdAsync(lobbyId, options);
                AssignLobby(lobby, LobbyState.Joined);
                await InitLobbyEventsAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task JoinLobbyByCodeAsync(string lobbyCode, JoinLobbyByCodeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lobby = await m_LobbyService.JoinLobbyByCodeAsync(lobbyCode, options);
                AssignLobby(lobby, LobbyState.Joined);
                await InitLobbyEventsAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task QuickJoinLobbyAsync(QuickJoinLobbyOptions options, CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lobby = await m_LobbyService.QuickJoinLobbyAsync(options);
                AssignLobby(lobby, LobbyState.Joined);
                await InitLobbyEventsAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task<List<string>> GetJoinedLobbiesAsync(CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await m_LobbyService.GetJoinedLobbiesAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public async Task ReconnectToLobbyAsync(string lobbyId = null, CancellationToken cancellationToken = default)
        {
            ValidateAuthorization();
            ValidateNoActiveLobby();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lobby = await m_LobbyService.ReconnectToLobbyAsync(lobbyId ?? Lobby.Id);
                AssignLobby(lobby, LobbyState.Joined);
                await InitLobbyEventsAsync();
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public Task RemovePlayerAsync(string playerId)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();
            return RemovePlayerTask();

            async Task RemovePlayerTask()
            {
                try
                {
                    await m_LobbyService.RemovePlayerAsync(Lobby.Id, playerId, ConcurrencyControlEnabled);
                }
                catch (LobbyServiceException lobbyException)
                {
                    throw LobbyConverter.ToSessionException(lobbyException);
                }
                catch (Exception e)
                {
                    throw new SessionException(e.Message, SessionError.Unknown);
                }
            }
        }

        private bool IsMigrationDataCacheValid()
        {
            return MigrationDataInfo != null && MigrationDataInfo.Expires >=
                DateTime.UtcNow.Add(k_MigrationDataExpiryDelay);
        }

        private async Task GetMigrationDataInfoAsync()
        {
            try
            {
                MigrationDataInfo = await m_LobbyService.GetMigrationDataInfoAsync(Lobby.Id);
            }
            catch (LobbyServiceException lobbyException)
            {
                throw LobbyConverter.ToSessionException(lobbyException);
            }
            catch (Exception e)
            {
                throw new SessionException(e.Message, SessionError.Unknown);
            }
        }

        public Task<LobbyMigrationData> DownloadMigrationDataAsync(TimeSpan timeout)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();
            return DownloadTask();
            async Task<LobbyMigrationData> DownloadTask()
            {
                if (!IsMigrationDataCacheValid())
                    await GetMigrationDataInfoAsync();
                try
                {
                    var migrationData = await m_LobbyService.DownloadMigrationDataAsync(MigrationDataInfo, new LobbyDownloadMigrationDataOptions { Timeout = timeout });
                    return migrationData;
                }
                catch (LobbyServiceException lobbyException)
                {
                    throw LobbyConverter.ToSessionException(lobbyException);
                }
                catch (Exception e)
                {
                    throw new SessionException(e.Message, SessionError.Unknown);
                }
            }
        }

        public Task UploadMigrationDataAsync(byte[] data, TimeSpan timeout)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();
            return UploadTask();
            async Task UploadTask()
            {
                if (!IsMigrationDataCacheValid())
                    await GetMigrationDataInfoAsync();
                if (MigrationDataInfo != null && data.Length > MigrationDataInfo.MaxSize)
                    throw new ArgumentOutOfRangeException(nameof(data),
                        $"data length must be less than {MigrationDataInfo.MaxSize}");
                try
                {
                    var options = new LobbyUploadMigrationDataOptions { Timeout = timeout };
                    await m_LobbyService.UploadMigrationDataAsync(MigrationDataInfo, data, options);
                }
                catch (LobbyServiceException lobbyException)
                {
                    throw LobbyConverter.ToSessionException(lobbyException);
                }
                catch (Exception e)
                {
                    throw new SessionException(e.Message, SessionError.Unknown);
                }
            }
        }

        void RegisterPlayerEvents()
        {
            if (m_PlayerId != null)
            {
                m_PlayerId.PlayerIdChanged -= OnPlayerIdChanged;
                m_PlayerId.PlayerIdChanged += OnPlayerIdChanged;
            }

            if (m_AccessTokenObserver != null)
            {
                m_AccessTokenObserver.AccessTokenChanged -= OnAccessTokenChanged;
                m_AccessTokenObserver.AccessTokenChanged += OnAccessTokenChanged;
            }
        }

        void UnregisterPlayerEvents()
        {
            if (m_PlayerId != null)
            {
                m_PlayerId.PlayerIdChanged -= OnPlayerIdChanged;
            }

            if (m_AccessTokenObserver != null)
            {
                m_AccessTokenObserver.AccessTokenChanged -= OnAccessTokenChanged;
            }
        }

        internal bool PollForLobbyEvents(LobbyEventCallbacks callbacks)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            m_Events = m_LobbyService.SetCacherLobbyCallbacks(Lobby.Id, callbacks);
            if (m_Events == null)
            {
                Logger.LogVerbose("Failed to set cacher lobby callbacks");
                return false;
            }

            SchedulePolling(TimeSpan.Zero);

            return true;
        }

        void SchedulePolling(TimeSpan delay)
        {
            if (!Application.exitCancellationToken.IsCancellationRequested)
            {
                m_PollingActionId ??= m_ActionScheduler.ScheduleAction(RunScheduledPolling, delay.TotalSeconds);
            }
            else
            {
                CancelPolling();
            }
        }

        void CancelPolling()
        {
            if (!m_PollingActionId.HasValue)
            {
                return;
            }

            m_ActionScheduler.CancelAction(m_PollingActionId.Value);
            m_PollingActionId = null;
        }

        async void RunScheduledPolling()
        {
            m_PollingActionId = null;

            try
            {
                await RefreshLobbyAsync();
            }
            catch (SessionException e)
            {
                if (e.Error == SessionError.NotInLobby)
                {
                    Logger.LogWarning("No longer in a lobby. Cancelling polling loop.");
                    return;
                }
            }

            SchedulePolling(k_PollingDelay);
        }

        /// <summary>
        /// Subscribes to lobby events asynchronously.
        /// </summary>
        /// <param name="callbacks">The callbacks to handle lobby events.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="SessionException">Thrown when the player is not authorized or not in an active lobby.</exception>
        /// <exception cref="LobbyServiceException">Thrown when there is an error subscribing to lobby events.</exception>
        /// <remarks>If the subscription to lobby events is successful, we query
        /// the lobby service using
        /// <see cref="ILobbyService.GetLobbyAsync(string, string)"/>
        /// to ensure we have the latest lobby version.<br/>
        /// If the response is <see langword="null"/>, we already have the
        /// latest version. Otherwise, we update our lobby with the response
        /// (the latest version).</remarks>
        public async Task SubscribeToLobbyEventsAsync(LobbyEventCallbacks callbacks)
        {
            ValidateAuthorization();
            ValidateInActiveLobby();

            try
            {
                m_Events = await m_LobbyService.SubscribeToLobbyEventsAsync(Lobby.Id, callbacks);
                if (m_Events != null)
                {
                    var newLobby = await m_LobbyService.GetLobbyAsync(Lobby.Id, Lobby.Version.ToString());
                    if (newLobby != null)
                    {
                        Lobby = newLobby;
                    }
                }
            }
            catch (LobbyServiceException ex)
            {
                switch (ex.Reason)
                {
                    case LobbyExceptionReason.AlreadySubscribedToLobby:
                        Logger.LogWarning(
                            $"Already subscribed to lobby[{Lobby.Id}]. We did not need to try and subscribe again. Exception Message: {ex.Message}");
                        break;
                    case LobbyExceptionReason.SubscriptionToLobbyLostWhileBusy:
                        Logger.LogError(
                            $"Subscription to lobby events was lost while it was busy trying to subscribe. Exception Message: {ex.Message}");
                        throw;
                    case LobbyExceptionReason.LobbyEventServiceConnectionError:
                        Logger.LogError($"Failed to connect to lobby events. Exception Message: {ex.Message}");
                        throw;
                    default: throw;
                }
            }
        }

        public async Task ResetAsync()
        {
            if (State != LobbyState.None)
            {
                CancelHeartbeat();
                await LobbyUnsubscribeCallbacksAsync();
                m_Events = null;
                Lobby = null;
                UnregisterPlayerEvents();
                State = LobbyState.None;
                LobbyExit?.Invoke();
                LobbyChanged?.Invoke();
            }
        }

        void ValidateAuthorization()
        {
            if (!IsAuthorized)
            {
                throw new SessionException("Player is not authorized.", SessionError.NotAuthorized);
            }
        }

        void ValidateNoActiveLobby()
        {
            if (Lobby != null)
            {
                throw new SessionException("Player is already in a session", SessionError.LobbyAlreadyExists);
            }
        }

        void ValidateInActiveLobby()
        {
            if (Lobby == null)
            {
                throw new SessionException("Player is not part of an active session.", SessionError.NotInLobby);
            }
        }

        private async void OnPlayerIdChanged(string obj)
        {
            await ResetAsync();
        }

        private async void OnAccessTokenChanged(string accessToken)
        {
            if (accessToken == null)
            {
                await ResetAsync();
            }
        }

        void ScheduleHeartbeat()
        {
            if (Application.isPlaying)
            {
                m_HeartbeatActionId =
                    m_ActionScheduler.ScheduleAction(RunScheduledHeartbeat, Settings.HeartbeatSeconds);
            }
        }

        void CancelHeartbeat()
        {
            if (m_HeartbeatActionId.HasValue)
            {
                m_ActionScheduler.CancelAction(m_HeartbeatActionId.Value);
                m_HeartbeatActionId = null;
            }
        }

        async void RunScheduledHeartbeat()
        {
            if (Application.isPlaying)
            {
                try
                {
                    m_HeartbeatActionId = null;
                    await SendHeartbeatAsync();
                }
                catch (Exception e)
                {
                    Logger.LogCallVerboseWithMessage(nameof(LobbyHandler), $"Exception was thrown: {e.Message}");
                }

                ScheduleHeartbeat();
            }
        }

        async Task SendHeartbeatAsync()
        {
            await m_LobbyService.SendHeartbeatPingAsync(Lobby.Id);
        }
    }
}
