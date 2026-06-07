using System.Collections.Generic;
using UnityEngine;

namespace Hcp.Presentation.Infrastructure
{
    // Runtime list of scenes to show in the launch menu and configurator selector.
    // Populated by the editor menu (HCP > Refresh Scenes), which scans Assets/Scenes
    // and also registers them in Build Settings so they can be loaded by name at runtime.
    [CreateAssetMenu(menuName = "HCP/Scene Catalog", fileName = "SceneCatalog")]
    public class SceneCatalog : ScriptableObject
    {
        [Tooltip("Scene names (without path/extension), excluding Menu and Configurator.")]
        public List<string> scenes = new List<string>();
    }
}
