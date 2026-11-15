using UnityEngine;

namespace Mleczarka.Player
{
    /// <summary>Rekwizyty do czynności (wiadro do dojenia, łyżka do mieszania) — obiekt w scenie, ustawiany narzędziem w edytorze.</summary>
    public class ActionProps : MonoBehaviour
    {
        public GameObject bucket, spoon;
        static ActionProps _i;
        public static ActionProps I => _i ? _i : (_i = FindFirstObjectByType<ActionProps>());
    }
}
