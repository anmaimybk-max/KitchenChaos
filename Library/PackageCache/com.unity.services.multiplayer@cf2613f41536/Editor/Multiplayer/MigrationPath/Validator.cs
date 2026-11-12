using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
// Unqualified, SessionState binds to the Unity.Services.Multiplayer enum of that name, not to UnityEditor's.
using EditorSessionState = UnityEditor.SessionState;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Unity.Services.Multiplayer.Editor.MigrationPath
{
    internal static class Validator
    {
        private const string k_LobbyPackageIdentifier =
            "com.unity.services.lobby";

        private const string k_MatchmakerPackageIdentifier =
            "com.unity.services.matchmaker";

        private const string k_MultiplayPackageIdentifier =
            "com.unity.services.multiplay";

        private const string k_MultiplayerSDKPackageIdentifier =
            "com.unity.services.multiplayer";

        private const string k_RelayPackageIdentifier =
            "com.unity.services.relay";

        private const string k_MigrationDocumentationURL =
            "https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/migration-path";

        private const string k_UnityMultiplayerServices =
            "Unity Multiplayer Services";

        internal const string k_WarnedSessionStateKey =
            "Unity.Services.Multiplayer.MigrationPath.Warned";

        private static readonly string[] k_IncompatiblePackageNames =
        {
            k_LobbyPackageIdentifier, k_MatchmakerPackageIdentifier,
            k_MultiplayPackageIdentifier, k_RelayPackageIdentifier
        };

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            Initialize(PackageInfo.GetAllRegisteredPackages);
        }

        internal static void Initialize(
            Func<PackageInfo[]> installedPackagesSource)
        {
            Events.registeredPackages -= OnRegisteredPackages;
            Events.registeredPackages += OnRegisteredPackages;
            WarnOnInstalledIncompatiblePackages(installedPackagesSource);
        }

        // Registration events only fire on a change, never for a project that already has the legacy packages.
        private static void WarnOnInstalledIncompatiblePackages(
            Func<PackageInfo[]> installedPackagesSource)
        {
            try
            {
                if (EditorSessionState.GetBool(k_WarnedSessionStateKey, false))
                {
                    return;
                }

                var installedPackages =
                    Array.AsReadOnly(installedPackagesSource());
                string message;
                if (!TryBuildIncompatibilityWarning(installedPackages, true,
                    out message))
                {
                    return;
                }

                Debug.LogWarning(
                    $"{message}See {k_MigrationDocumentationURL} for the migration path.");
                EditorSessionState.SetBool(k_WarnedSessionStateKey, true);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"Error checking installed package compatibility: {e.Message}");
            }
        }

        private static string Describe(PackageInfo packageInfo)
        {
            return
                $"\t- {packageInfo.displayName} ({packageInfo.name}) version {packageInfo.version}";
        }

        private static void AppendIncompatiblePackages(
            StringBuilder stringBuilder,
            in ReadOnlyCollection<PackageInfo> items,
            bool alreadyInstalled)
        {
            var isSingle = items.Count == 1;
            stringBuilder.AppendLine(alreadyInstalled
                ? $"The following {(isSingle ? "package is" : "packages are")} installed in this project:"
                : $"The following {(isSingle ? "package has" : "packages have")} been added:");
            foreach (var packageInfo in items)
            {
                stringBuilder.AppendLine(Describe(packageInfo));
            }

            stringBuilder.AppendLine(
                $"However, {(isSingle ? "it is" : "they are")} incompatible with the " +
                "Unity Multiplayer Service SDK.");
        }

        private static void AppendPackagesWithIncompatibleDependencies(
            StringBuilder stringBuilder,
            in ReadOnlyCollection<IGrouping<PackageInfo, DependencyInfo>>
            packages,
            bool alreadyInstalled)
        {
            foreach (var group in packages)
            {
                var hasSingleDependency = group.Count() == 1;
                stringBuilder.AppendLine(
                    $"The following {(alreadyInstalled ? "installed package has" : "added package has")} " +
                    $"{(hasSingleDependency ? "an incompatible dependency" : "incompatible dependencies")} " +
                    "with the Multiplayer Services SDK:");
                stringBuilder.AppendLine(
                    $"\t- {group.Key.name} version {group.Key.version}");

                stringBuilder.AppendLine(
                    $"The incompatible {(hasSingleDependency ? "dependency is" : "dependencies are")}:");
                foreach (var dependency in group)
                {
                    stringBuilder.AppendLine(
                        $"\t- {dependency.name} version {dependency.version}");
                }
            }
        }

        private static string WarningMessage(
            in CompatibilityInfo compatibilityInfo,
            bool alreadyInstalled)
        {
            var stringBuilder = new StringBuilder();

            if (compatibilityInfo.PackageWithoutDependencies.Count > 0)
            {
                AppendIncompatiblePackages(stringBuilder,
                    compatibilityInfo.PackageWithoutDependencies,
                    alreadyInstalled);
            }

            if (compatibilityInfo.PackageWithDependencies.Count > 0)
            {
                AppendPackagesWithIncompatibleDependencies(stringBuilder,
                    compatibilityInfo.PackageWithDependencies,
                    alreadyInstalled);
            }

            var packagesToRemove = compatibilityInfo
                .PackageWithoutDependencies
                .Concat(compatibilityInfo.PackageWithDependencies.Select(
                    group => group.Key))
                .ToList();

            stringBuilder.AppendLine();
            stringBuilder.AppendLine(
                $"Please remove the following {(packagesToRemove.Count == 1 ? "package" : "packages")}:");
            foreach (var packageInfo in packagesToRemove)
            {
                stringBuilder.AppendLine(Describe(packageInfo));
            }

            stringBuilder.AppendLine(
                "If you wish to use the Unity Multiplayer Services SDK.");
            return stringBuilder.ToString();
        }

        private static void OnRegisteredPackages(
            PackageRegistrationEventArgs eventArgs)
        {
            try
            {
                var isMultiplayerSDKPackageRemoved = eventArgs.removed
                    .Select(packageInfo => packageInfo.name)
                    .Contains(k_MultiplayerSDKPackageIdentifier);
                if (isMultiplayerSDKPackageRemoved)
                {
                    Events.registeredPackages -= OnRegisteredPackages;
                    return;
                }

                string message;
                if (!TryBuildIncompatibilityWarning(eventArgs.added, false,
                    out message))
                {
                    return;
                }

                Debug.LogWarning(message);
                EditorSessionState.SetBool(k_WarnedSessionStateKey, true);

                var choice = EditorUtility.DisplayDialogComplex(
                    k_UnityMultiplayerServices,
                    message, "Ok", "Close", "Help");
                if (choice == 2)
                {
                    Application.OpenURL(k_MigrationDocumentationURL);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"Error processing package registration event: {e.Message}");
            }
        }

        internal static bool TryBuildIncompatibilityWarning(
            in ReadOnlyCollection<PackageInfo> packages,
            bool alreadyInstalled,
            out string message)
        {
            var compatibilityInfo = CheckCompatibility(packages);
            if (compatibilityInfo.IsCompatible)
            {
                message = null;
                return false;
            }

            message = WarningMessage(compatibilityInfo, alreadyInstalled);
            return true;
        }

        internal static CompatibilityInfo CheckCompatibility(
            in ReadOnlyCollection<PackageInfo> added)
        {
            var incompatiblePackages = added
                .Where(packageInfo =>
                k_IncompatiblePackageNames.Contains(packageInfo.name))
                .ToList();

            var incompatibleDependentPackages = added.SelectMany(
                packageInfo =>
                    packageInfo.dependencies
                        .Where(dependency =>
                            k_IncompatiblePackageNames.Contains(dependency
                                .name))
                        .Select(dependency => new
                        {
                            Package = packageInfo,
                            Dependency = dependency
                        }))
                .GroupBy(tuple => tuple.Package, tuple => tuple.Dependency)
                .Where(group => group.Any())
                .ToList();

            return new CompatibilityInfo(
                incompatibleDependentPackages.AsReadOnly(),
                incompatiblePackages.AsReadOnly());
        }

        internal readonly struct CompatibilityInfo
        {
            public readonly
            ReadOnlyCollection<IGrouping<PackageInfo, DependencyInfo>>
            PackageWithDependencies;

            public readonly ReadOnlyCollection<PackageInfo>
            PackageWithoutDependencies;

            public bool IsCompatible =>
                !PackageWithDependencies.Any() &&
                !PackageWithoutDependencies.Any();

            public CompatibilityInfo(
                ReadOnlyCollection<IGrouping<PackageInfo, DependencyInfo>>
                packageWithDependencies,
                ReadOnlyCollection<PackageInfo> packageWithoutDependencies)
            {
                PackageWithDependencies = packageWithDependencies;
                PackageWithoutDependencies = packageWithoutDependencies;
            }
        }
    }
}
