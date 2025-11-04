using UnityEngine;

namespace Belen.UI
{
    // Shows/hides a CanvasGroup prompt based on presence.
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPromptController : MonoBehaviour
    {
        public Belen.Interaction.PresenceDetector presence;
        public float fadeSpeed = 4f;

        private CanvasGroup _cg;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
        }

        private void Update()
        {
            if (presence == null) return;
            float target = presence.IsPresent ? 0f : 1f;
            _cg.alpha = Mathf.MoveTowards(_cg.alpha, target, fadeSpeed * Time.deltaTime);
            bool blocks = _cg.alpha > 0.01f;
            _cg.blocksRaycasts = blocks;
            _cg.interactable = blocks;
        }
    }
}

