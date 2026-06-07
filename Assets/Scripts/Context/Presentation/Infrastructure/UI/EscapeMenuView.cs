using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hcp.Presentation.Infrastructure
{
    // Esc overlay available in any scene: pops a panel with "Menú principal" / "Reanudar".
    // Builds its own UIDocument tree in code. The menu scene name is injected by the caller.
    public class EscapeMenuView : MonoBehaviour
    {
        private UIDocument _doc;
        private VisualElement _overlay;
        private string _menuScene;
        private bool _open;

        public void Initialize(PanelSettings panel, string menuScene)
        {
            _menuScene = menuScene;
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            Build();
            SetOpen(false);
        }

        private void Build()
        {
            var root = _doc.rootVisualElement;
            root.Clear();

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0; _overlay.style.top = 0; _overlay.style.right = 0; _overlay.style.bottom = 0;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);

            var card = new VisualElement();
            card.style.paddingLeft = 20; card.style.paddingRight = 20;
            card.style.paddingTop = 16; card.style.paddingBottom = 16;
            card.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.98f);
            card.style.alignItems = Align.Center;
            _overlay.Add(card);

            var title = new Label("Pausa");
            title.style.color = Color.white;
            title.style.fontSize = 22;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            card.Add(title);

            var toMenu = new Button(GoToMenu) { text = "Menú principal" };
            StyleButton(toMenu);
            card.Add(toMenu);

            var resume = new Button(() => SetOpen(false)) { text = "Reanudar" };
            StyleButton(resume);
            card.Add(resume);

            root.Add(_overlay);
        }

        private static void StyleButton(Button b)
        {
            b.style.width = 220;
            b.style.height = 34;
            b.style.marginTop = 4;
            b.style.marginBottom = 4;
            b.style.fontSize = 15;
            b.style.color = new Color(0.1f, 0.1f, 0.1f);
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) SetOpen(!_open);
#endif
        }

        private void SetOpen(bool open)
        {
            _open = open;
            if (_overlay != null)
                _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void GoToMenu()
        {
            if (!string.IsNullOrEmpty(_menuScene))
                SceneManager.LoadScene(_menuScene, LoadSceneMode.Single);
        }
    }
}
