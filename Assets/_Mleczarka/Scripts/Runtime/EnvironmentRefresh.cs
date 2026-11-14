using UnityEngine;

namespace Mleczarka
{
    /// <summary>Przy starcie gry przelicza światło otoczenia z aktualnego nieba (HDRI).</summary>
    public class EnvironmentRefresh : MonoBehaviour
    {
        void Start() => DynamicGI.UpdateEnvironment();
    }
}
