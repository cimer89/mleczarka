using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Dodaje do kontrolerów rolnika i pracownika warstwę „Noszenie”: tylko ręce (maska awatara) z klipu Walk_Carry_Loop.
    /// Nogi i tułów dalej idą z chodu i biegu; wagę warstwy ustawia gra, gdy postać niesie skrzynkę.
    /// </summary>
    public static class CarryAnimSetup
    {
        const string Q = "Assets/_Mleczarka/Art/ThirdParty/Quaternius/";
        const string Gen = "Assets/_Mleczarka/Art/Generated/Characters/";

        [MenuItem("Mleczarka/Produkcja/13. Animacje postaci (noszenie, rower)")]
        public static void Run()
        {
            var clip = new[] { Q + "UniversalAnimationLibrary2/UAL2_Standard.fbx", Q + "UniversalAnimationLibrary/UAL1_Standard.fbx" }
                .SelectMany(p => AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>()).FirstOrDefault(c => c.name == "Walk_Carry_Loop");
            if (!clip) { Debug.LogError("[Mleczarka] Brak klipu Walk_Carry_Loop."); return; }
            string maskPath = Gen + "Maska_Rece.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (!mask) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, maskPath); }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                    part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
            }
            EditorUtility.SetDirty(mask);
            int n = 0;
            foreach (var path in new[] { Gen + "Rolnik.controller", "Assets/_Mleczarka/Art/FactoryInterior/Pracownik.controller" })
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (!ctrl) continue;
                var layers = ctrl.layers.ToList();
                int idx = layers.FindIndex(l => l.name == "Noszenie");
                if (idx >= 0) ctrl.RemoveLayer(idx);
                ctrl.AddLayer("Noszenie");
                var ls = ctrl.layers;
                var layer = ls[ls.Length - 1];
                layer.avatarMask = mask; layer.defaultWeight = 0; layer.blendingMode = AnimatorLayerBlendingMode.Override;
                var st = layer.stateMachine.AddState("Trzyma skrzynkę"); st.motion = clip;
                layer.stateMachine.defaultState = st;
                ls[ls.Length - 1] = layer; ctrl.layers = ls;
                AddBike(ctrl);
                EditorUtility.SetDirty(ctrl); n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Mleczarka] Warstwa noszenia dodana do {n} kontrolerów (klip {clip.name}, maska: ręce).");
        }

        /// <summary>Jazda na rowerze (kariera): stan „Rower” z klipem Driving_Loop — siedząca postać, ręce na kierownicy.</summary>
        static void AddBike(AnimatorController ctrl)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Q + "UniversalAnimationLibrary/UAL1_Standard.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == "Driving_Loop")
                       ?? AssetDatabase.LoadAllAssetsAtPath(Q + "UniversalAnimationLibrary/UAL1_Standard.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == "Sitting_Idle_Loop");
            if (!clip) return;
            var sm = ctrl.layers[0].stateMachine;
            var old = sm.states.FirstOrDefault(s => s.state.name == "Rower").state;
            if (old) sm.RemoveState(old);
            foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == null || t.destinationState.name == "Rower").ToArray()) sm.RemoveAnyStateTransition(t);
            if (!ctrl.parameters.Any(p => p.name == "Bike")) ctrl.AddParameter("Bike", AnimatorControllerParameterType.Bool);
            var st = sm.AddState("Rower"); st.motion = clip;
            var tin = sm.AddAnyStateTransition(st); tin.AddCondition(AnimatorConditionMode.If, 0, "Bike"); tin.hasExitTime = false; tin.duration = 0.2f; tin.canTransitionToSelf = false;
            var tout = st.AddTransition(sm.defaultState); tout.AddCondition(AnimatorConditionMode.IfNot, 0, "Bike"); tout.hasExitTime = false; tout.duration = 0.2f;
        }
    }
}
