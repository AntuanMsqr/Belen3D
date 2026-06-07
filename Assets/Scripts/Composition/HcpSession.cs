namespace Hcp.Composition
{
    // Tiny cross-scene handoff: which scene the Configurator should target, plus the
    // well-known menu scene name used by the Esc overlay and the menu "Editar" action.
    public static class HcpSession
    {
        public const string MenuScene = "HcpMenu";
        public const string ConfiguratorScene = "HcpConfigurator";

        // Set by the menu's "Editar" button; read+cleared by the Configurator on start.
        public static string SceneToEdit;
    }
}
