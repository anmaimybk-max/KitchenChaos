using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Core.Model;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Core.Parser;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Deployment;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.IO;
using Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.Model;
using Unity.Services.Multiplayer.Editor.Shared.Analytics;
using Unity.Services.Multiplayer.Editor.Shared.UI.DeploymentConfigInspectorFooter;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.Services.Multiplayer.Editor.Matchmaker.Authoring.UI
{
    [CustomEditor(typeof(MatchmakerConfigAssetImporter))]
    [CanEditMultipleObjects]
    class MatchmakerQueueInspector : AssetImporterEditor
    {
        enum ApplyOutcome
        {
            Skipped,
            Abandoned,
            UpToDate,
            Written,
            Failed
        }

        static readonly JsonMergeSettings k_MergeSettings = new JsonMergeSettings
        {
            MergeArrayHandling = MergeArrayHandling.Replace,
            PropertyNameComparison = StringComparison.InvariantCultureIgnoreCase,
            MergeNullValueHandling = MergeNullValueHandling.Ignore,
        };

        static JsonSerializerSettings SerializerSettings => MatchmakerConfigLoader.GetSerializationSettings();
        [SerializeField]
        VisualTreeAsset m_MatchmakerEnvironmentUxml;

        /// <summary>
        /// This declaration allows the AssetEditorInspector to create a custom serialized object
        /// containing our own scriptable object type which displays the inspector.
        /// Using this allows the base editor importer class to handle apply/revert comparison for us.
        /// </summary>
        protected override Type extraDataType => typeof(MatchMakerQueueInspectorConfig);

        /// <summary>
        /// This is automatically called by the base AssetImporterEditor class and is used to initialize the extra data
        /// </summary>
        /// <param name="extraData"></param>
        /// <param name="targetIndex"></param>
        protected override void InitializeExtraDataInstance(Object extraData, int targetIndex)
        {
            var path = AssetDatabase.GetAssetPath(targets[targetIndex]);
            if (IsEnvironmentConfigPath(path))
                return;

            if (extraData is MatchMakerQueueInspectorConfig config)
            {
                config.Initialize(LoadConfigurationForDisplayOrDefault(path));
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var matchmakerFilePath = AssetDatabase.GetAssetPath(target);

            if (IsEnvironmentConfigPath(matchmakerFilePath))
            {
                m_MatchmakerEnvironmentUxml.CloneTree(root);
                ShowResourceBody(root);
            }
            else
            {
                AddMatchmakerConfigInspector(root);
            }
            SetupConfigFooter(root);
            root.Add(new IMGUIContainer(ApplyRevertGUI));
            return root;
        }

        static bool IsEnvironmentConfigPath(string path) =>
            path.EndsWith(IMatchmakerConfigParser.EnvironmentConfigExtension);

        protected override void Apply()
        {
            serializedObject.ApplyModifiedProperties();
            extraDataSerializedObject?.ApplyModifiedProperties();

            var writtenPaths = new List<string>(targets.Length);
            var eligibleTargetCount = 0;
            var failedTargetCount = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                var outcome = TryApplyTarget(i, out var writtenPath);
                if (outcome != ApplyOutcome.Skipped)
                {
                    eligibleTargetCount++;
                }

                if (outcome == ApplyOutcome.Written)
                {
                    writtenPaths.Add(writtenPath);
                }
                else if (outcome == ApplyOutcome.Failed)
                {
                    failedTargetCount++;
                }
            }

            using (new AssetDatabase.AssetEditingScope())
            {
                foreach (var path in writtenPaths)
                {
                    AssetDatabase.ImportAsset(path);
                }
            }

            if (failedTargetCount > 0)
            {
                throw new Exception($"Failed to apply {failedTargetCount} of {eligibleTargetCount} Matchmaker configuration target(s).");
            }
        }

        ApplyOutcome TryApplyTarget(int targetIndex, out string writtenPath)
        {
            writtenPath = null;
            var errorContext = assetTargets != null && targetIndex < assetTargets.Length
                ? assetTargets[targetIndex]
                : null;
            try
            {
                var matchmakerFilePath = AssetDatabase.GetAssetPath(targets[targetIndex]);
                if (IsEnvironmentConfigPath(matchmakerFilePath) || !(extraDataTargets[targetIndex] is MatchMakerQueueInspectorConfig editedConfig))
                {
                    return ApplyOutcome.Skipped;
                }

                if (!File.Exists(matchmakerFilePath))
                {
                    Debug.LogError($"Matchmaker queue config no longer exists on disk: {matchmakerFilePath}", errorContext);
                    return ApplyOutcome.Abandoned;
                }

                if (!TryLoadConfiguration(matchmakerFilePath, out var configOnDisk))
                {
                    return ApplyOutcome.Failed;
                }

                var mergedJson = MergeEditedConfigOntoDisk(editedConfig, configOnDisk, out var configOnDiskJson);
                if (mergedJson == configOnDiskJson)
                {
                    return ApplyOutcome.UpToDate;
                }

                File.WriteAllText(matchmakerFilePath, mergedJson);
                writtenPath = matchmakerFilePath;
                return ApplyOutcome.Written;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"Unable to save the configuration file back to its json format with the current values: {e.Message}",
                    errorContext);
                Debug.LogException(e);
                return ApplyOutcome.Failed;
            }
        }

        static string MergeEditedConfigOntoDisk(
            MatchMakerQueueInspectorConfig editedConfig, QueueConfig configOnDisk, out string configOnDiskJson)
        {
            var editedJson = editedConfig.ToJson(SerializerSettings);
            configOnDiskJson = JsonConvert.SerializeObject(configOnDisk, SerializerSettings);

            var configOnDiskObj = JObject.Parse(configOnDiskJson);
            var editedObj = JObject.Parse(editedJson);
            configOnDiskObj.Merge(editedObj, k_MergeSettings);

            var mergedConfig = configOnDiskObj.ToObject<QueueConfig>(JsonSerializer.Create(SerializerSettings));
            return JsonConvert.SerializeObject(mergedConfig, Formatting.Indented, SerializerSettings);
        }

        void SetupConfigFooter(VisualElement myInspector)
        {
            var deploymentConfigInspectorFooter = new DeploymentConfigInspectorFooter();

            var dashboardUrlResolver = MatchmakerAuthoringServices.Instance.GetService<MatchmakerDashboardQueueUrlResolver>();
            if (dashboardUrlResolver.TryGetQueueUrl(out _))
            {
                deploymentConfigInspectorFooter.DashboardLinkUrlGetter = () =>
                    Task.FromResult(dashboardUrlResolver.GetQueueUrlOrOpenProjectCloudSettings());
            }

            deploymentConfigInspectorFooter.BindGUI(
                AssetDatabase.GetAssetPath(target),
                MatchmakerAuthoringServices.Instance.GetService<ICommonAnalytics>(),
                "matchmaker");
            myInspector.Add(deploymentConfigInspectorFooter);
        }

        // The MatchmakerEnvironment file reuses this inspector but isn't structured like a MatchmakerQueue file,
        // so it falls back to showing its raw text content instead of the generated property fields.
        void ShowResourceBody(VisualElement myInspector)
        {
            var body = myInspector.Q<TextField>();
            if (targets.Length == 1)
            {
                body.visible = true;
                body.value = ReadResourceBody(targets[0]);
            }
            else
            {
                body.visible = false;
            }
        }

        static string ReadResourceBody(UnityEngine.Object resource)
        {
            const int k_MaxLines = 75;
            var path = AssetDatabase.GetAssetPath(resource);
            var lines = File.ReadLines(path).Take(k_MaxLines).ToList();
            if (lines.Count == k_MaxLines)
            {
                lines.Add("...");
            }
            return string.Join(Environment.NewLine, lines);
        }

        static QueueConfig LoadConfigurationForDisplayOrDefault(string path)
        {
            if (TryLoadConfiguration(path, out var config, logErrors: false))
            {
                return config;
            }

            Debug.LogWarning($"Matchmaker queue config could not be read; showing default values: {path}");
            return QueueConfig.GetDefault();
        }

        static bool TryLoadConfiguration(string path, out QueueConfig config, bool logErrors = true)
        {
            config = null;

            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return false;
            }

            if (file.Length == 0)
            {
                if (logErrors)
                {
                    Debug.LogError($"Matchmaker queue config is empty: {path}");
                }
                return false;
            }

            try
            {
                config = new MatchmakerConfigLoader(new FileSystem()).Parse(path) as QueueConfig;
                if (config != null)
                {
                    return true;
                }

                if (logErrors)
                {
                    Debug.LogError($"Matchmaker queue config does not contain a queue configuration: {path}");
                }
                return false;
            }
            catch (Exception e)
            {
                if (logErrors)
                {
                    Debug.LogError($"Error loading matchmaker queue data: {e.Message}");
                }
                return false;
            }
        }

        void AddMatchmakerConfigInspector(VisualElement root)
        {
            var inspector = new PropertyField
            {
                bindingPath = nameof(MatchMakerQueueInspectorConfig.Data)
            };
            inspector.Unbind();
            inspector.Bind(extraDataSerializedObject);

            root.Add(inspector);
        }
    }
}
