using UnityEngine;

namespace KitchenChaos.Layout
{
    /// <summary>Authoring metadata for the step-4 layout. This is not gameplay logic.</summary>
    public sealed class KitchenLayoutMarker : MonoBehaviour
    {
        public enum Kind { IngredientShelf, Chopping, Grill, Soup, Assembly, Delivery, Trash, Conveyor, WetZone, Spawn, OutputSlot }
        public Kind kind;
        public string requirementId;
        [TextArea] public string description;
        public Vector2 footprint;
        public Transform interactionAnchor;
        public float warningAt;
        public float activeAt;
        public float activeDuration;
        public bool layoutOnly = true;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == Kind.WetZone ? Color.cyan : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * .04f, new Vector3(footprint.x, .08f, footprint.y));
            if (interactionAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(interactionAnchor.position, 1.5f);
            }
        }
    }
}
