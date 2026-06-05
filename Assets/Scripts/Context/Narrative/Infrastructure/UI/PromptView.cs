using UnityEngine;
using UnityEngine.UIElements;
using Belen.Narrative.Domain;
using Belen.HeadTracking.Infrastructure;

namespace Belen.Narrative.Infrastructure
{
    // UI Toolkit View: fades an idle prompt in/out based on presence.
    // Shows when absent, hides when a user is present.
    [RequireComponent(typeof(UIDocument))]
    public class PromptView : MonoBehaviour
    {
        public float fadeSpeed = 4f;

        private UIDocument _doc;
        private VisualElement _root;
        private float _alpha = 1f;
        private bool _present;

        public void Initialize(PresenceView presence)
        {
            if (presence == null) return;
            presence.OnPresent += HandlePresent;
            presence.OnAbsent += HandleAbsent;
            _present = presence.IsPresent;
        }

        private void HandlePresent() => _present = true;
        private void HandleAbsent() => _present = false;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            _root = _doc != null ? _doc.rootVisualElement : null;
        }

        private void Update()
        {
            if (_root == null)
            {
                if (_doc != null) _root = _doc.rootVisualElement;
                if (_root == null) return;
            }

            _alpha = PromptFade.Step(_alpha, _present, fadeSpeed, Time.deltaTime);
            _root.style.opacity = _alpha;
            _root.style.display = _alpha > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
