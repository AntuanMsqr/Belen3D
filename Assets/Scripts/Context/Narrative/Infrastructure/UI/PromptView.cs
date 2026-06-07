using UnityEngine;
using UnityEngine.UIElements;
using Hcp.Narrative.Domain;
using Hcp.HeadTracking.Infrastructure;

namespace Hcp.Narrative.Infrastructure
{
    // UI Toolkit View: fades an idle prompt in/out based on presence.
    // Shows when absent, hides when a user is present.
    [RequireComponent(typeof(UIDocument))]
    public class PromptView : MonoBehaviour
    {
        public float fadeSpeed = 4f;

        private UIDocument doc;
        private VisualElement root;
        private float alpha = 1f;
        private bool present;

        public void Initialize(PresenceView presence)
        {
            if (presence == null) return;
            presence.OnPresent += HandlePresent;
            presence.OnAbsent += HandleAbsent;
            present = presence.IsPresent;
        }

        private void HandlePresent() => present = true;
        private void HandleAbsent() => present = false;

        private void OnEnable()
        {
            doc = GetComponent<UIDocument>();
            root = doc != null ? doc.rootVisualElement : null;
        }

        private void Update()
        {
            if (root == null)
            {
                if (doc != null) root = doc.rootVisualElement;
                if (root == null) return;
            }

            alpha = PromptFade.Step(alpha, present, fadeSpeed, Time.deltaTime);
            root.style.opacity = alpha;
            root.style.display = alpha > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
