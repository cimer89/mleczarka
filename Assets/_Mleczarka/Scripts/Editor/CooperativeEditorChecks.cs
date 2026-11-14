using UnityEditor;
using UnityEngine;

namespace Mleczarka.EditorTools
{
    public static class CooperativeEditorChecks
    {
        [MenuItem("Mleczarka/Testy/Spoldzielnia")]
        public static void Run()
        {
            Debug.Log("[Mleczarka] Spółdzielnia: " + CooperativeChecks.Run());
        }
    }
}
