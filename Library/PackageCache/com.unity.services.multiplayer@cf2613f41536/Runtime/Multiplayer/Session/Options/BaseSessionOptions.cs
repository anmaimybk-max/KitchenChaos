using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Unity.Services.Multiplayer
{
    /// <summary>
    /// Common options for all session flows.
    /// </summary>
    /// <seealso cref="SessionOptions"/>
    /// <seealso cref="JoinSessionOptions"/>
    public abstract class BaseSessionOptions
    {
        /// <summary>
        /// The session type is a client-side key used to uniquely identify a
        /// session
        /// </summary>
        public string Type { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Custom game-specific properties that apply to an individual player
        /// (e.g. 'role' or 'skill').
        /// </summary>
        /// <remarks>
        /// Up to 10 properties may be set per player. <see
        /// cref="PlayerProperty">Player properties</see> have different
        /// visibility levels.
        /// </remarks>
        public Dictionary<string, PlayerProperty> PlayerProperties { get; set; } = new Dictionary<string, PlayerProperty>();

        internal Dictionary<Type, IModuleOption> Options { get; set; } = new Dictionary<Type, IModuleOption>();

        // Entries can be missing when Options is assigned wholesale, so only ever read this via TryGetValue.
        internal Dictionary<Type, string> OptionSources { get; } = new Dictionary<Type, string>();

        internal bool HasOption<T>() where T : class, IModuleOption => Options.ContainsKey(typeof(T));
        internal T GetOption<T>() where T : class, IModuleOption => Options.ContainsKey(typeof(T)) ? Options[typeof(T)] as T : null;
    }

    /// <summary>
    /// Provides extension methods to configure session options.
    /// </summary>
    /// <remarks>
    /// <para>
    /// To configure the network topology.
    /// <list type="table">
    /// <listheader>
    /// <term>Topology</term>
    /// <description>Methods</description>
    /// </listheader>
    /// <item>
    /// <term>Direct Connection</term>
    /// <description>
    /// <see cref="WithDirectNetwork{T}(T)"/><br/>
    /// <see cref="WithDirectNetwork{T}(T, DirectNetworkOptions)"/><br/>
    /// <see cref="WithDirectNetwork{T}(T,string,string,int)"/><br/>
    /// </description>
    /// </item>
    /// <item>
    /// <term>Relay Connection</term>
    /// <description>
    /// <see cref="WithRelayNetwork{T}(T,string)"/><br/>
    /// <see cref="WithRelayNetwork{T}(T,RelayNetworkOptions)"/><br/>
    /// </description>
    /// </item>
    /// <item>
    /// <term>Distributed Authority</term>
    /// <description>
    /// <see cref="WithDistributedAuthorityNetwork{T}(T,string)"/><br/>
    /// <see cref="WithDistributedAuthorityNetwork{T}(T,RelayNetworkOptions)"/><br/>
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// To configure the network handler.<br/>
    /// <see cref="WithNetworkHandler{T}"/>
    /// </para>
    /// <para>
    /// To configure host migration.<br/>
    /// <see cref="WithHostMigration{T}(T,Unity.Services.Multiplayer.IMigrationDataHandler)"/><br/>
    /// <see cref="WithHostMigration{T}(T,Unity.Services.Multiplayer.IMigrationDataHandler, TimeSpan,TimeSpan)"/>
    /// </para>
    /// <para>
    /// To configure player name.<br/>
    /// <see cref="WithPlayerName{T}"/>
    /// </para>
    /// </remarks>
    public static partial class SessionOptionsExtensions
    {
        /// <summary>
        /// Adds a module option to the session options if it does not already exist.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the session options, derived from <see cref="BaseSessionOptions"/>.
        /// </typeparam>
        /// <typeparam name="U">
        /// The type of the module option, implementing <see cref="IModuleOption"/>.
        /// </typeparam>
        /// <param name="options">
        /// The session options instance to modify.
        /// </param>
        /// <param name="moduleOptions">
        /// The module option to add.
        /// </param>
        /// <returns>The modified session options instance.</returns>
        /// <exception cref="SessionException">
        /// Thrown with <see cref="SessionError.InvalidParameter"/> if an option of the
        /// specified type was already added.
        /// </exception>
        internal static T WithOption<T, U>(this T options, U moduleOptions, [CallerMemberName] string sourceMethod = null)
            where T : BaseSessionOptions where U : IModuleOption
        {
            var type = typeof(U);
            if (!options.Options.TryAdd(type, moduleOptions))
            {
                throw new SessionException(BuildOptionConflictMessage(options, type, sourceMethod),
                    SessionError.InvalidParameter);
            }

            options.OptionSources[type] = sourceMethod;
            return options;
        }

        static string BuildOptionConflictMessage(BaseSessionOptions options, Type optionType, string sourceMethod)
        {
            options.OptionSources.TryGetValue(optionType, out var existingSourceMethod);

            if (string.IsNullOrEmpty(sourceMethod) || string.IsNullOrEmpty(existingSourceMethod))
            {
                return "This option is already set on these session options and cannot be set twice.";
            }

            if (string.Equals(sourceMethod, existingSourceMethod, StringComparison.Ordinal))
            {
                return $"{sourceMethod} can only be called once on the same session options.";
            }

            return $"{sourceMethod} cannot be combined with {existingSourceMethod} on the same session options. Remove one of them.";
        }

        /// <summary>
        /// Adds or updates a module option in the session options.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the session options, derived from <see cref="BaseSessionOptions"/>.
        /// </typeparam>
        /// <typeparam name="U">
        /// The type of the module option, implementing <see cref="IModuleOption"/>.
        /// </typeparam>
        /// <param name="options">
        /// The session options instance to modify.
        /// </param>
        /// <param name="moduleOptions">
        /// The module option to add or update.
        /// </param>
        /// <returns>The modified session options instance.</returns>
        internal static T WithOptionAddOrUpdate<T, U>(this T options, U moduleOptions, [CallerMemberName] string sourceMethod = null)
            where T : BaseSessionOptions where U : IModuleOption
        {
            options.Options[typeof(U)] = moduleOptions;
            options.OptionSources[typeof(U)] = sourceMethod;
            return options;
        }
    }
}
