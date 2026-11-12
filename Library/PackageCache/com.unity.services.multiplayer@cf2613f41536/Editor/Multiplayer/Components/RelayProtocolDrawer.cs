using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Services.Multiplayer.Editor.Components
{
    [CustomPropertyDrawer(typeof(RelayProtocol))]
    class RelayProtocolDrawer : PropertyDrawer
    {
        static readonly List<RelayProtocol> k_Choices = new()
        {
            RelayProtocol.UDP,
            RelayProtocol.DTLS,
            RelayProtocol.WSS,
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // The inspector owns and repositions the property it passes in, so the callbacks
            // below keep their own copy.
            var boundProperty = property.Copy();

            var field = new PopupField<RelayProtocol>(
                boundProperty.displayName,
                k_Choices,
                ToChoice(boundProperty.intValue));
            field.AddToClassList(BaseField<RelayProtocol>.alignedFieldUssClassName);

            field.RegisterValueChangedCallback(evt =>
            {
                boundProperty.intValue = (int)evt.newValue;
                boundProperty.serializedObject.ApplyModifiedProperties();
            });

            // A bindingPath is not an option here: Unity's enum binding only handles
            // PopupField<string>, EnumField and EnumFlagsField, all of which would offer every
            // RelayProtocol name, Unknown and Default included.
            field.TrackPropertyValue(boundProperty,
                changed => field.SetValueWithoutNotify(ToChoice(changed.intValue)));

            // Property tracking only reports a change once something re-reads the serialized
            // object, which undo does not do on its own.
            Undo.UndoRedoCallback onUndoRedo = () =>
            {
                var serializedObject = boundProperty.serializedObject;
                if (serializedObject.targetObject == null)
                    return;

                serializedObject.Update();
                field.SetValueWithoutNotify(ToChoice(boundProperty.intValue));
            };
            field.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += onUndoRedo);
            field.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= onUndoRedo);

            return field;
        }

        static RelayProtocol ToChoice(int serializedValue)
        {
            var protocol = (RelayProtocol)serializedValue;
            return k_Choices.Contains(protocol) ? protocol : RelayProtocol.Default;
        }
    }
}
