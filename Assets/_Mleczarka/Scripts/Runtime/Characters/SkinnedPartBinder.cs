using System.Collections.Generic;
using UnityEngine;

namespace Mleczarka.Characters
{
    /// <summary>
    /// Podpina części modułowe (ubrania, włosy) pod szkielet ciała po nazwach kości.
    /// Wszystkie paczki Quaternius (UBC, Outfits) mają ten sam szkielet, więc nazwy się zgadzają.
    /// </summary>
    public static class SkinnedPartBinder
    {
        public static Dictionary<string, Transform> MapBones(Transform root)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!map.ContainsKey(t.name)) map.Add(t.name, t);
            return map;
        }

        /// <summary>Przenosi renderer pod nowy korzeń i przepina kości. Zwraca liczbę kości bez odpowiednika.</summary>
        public static int Bind(SkinnedMeshRenderer part, Transform newParent, Dictionary<string, Transform> bones)
        {
            int missing = 0;
            var src = part.bones;
            var dst = new Transform[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] != null && bones.TryGetValue(src[i].name, out var b)) dst[i] = b;
                else missing++;
            }
            part.transform.SetParent(newParent, false);
            part.bones = dst;
            if (part.rootBone != null && bones.TryGetValue(part.rootBone.name, out var rb)) part.rootBone = rb;
            return missing;
        }
    }
}
