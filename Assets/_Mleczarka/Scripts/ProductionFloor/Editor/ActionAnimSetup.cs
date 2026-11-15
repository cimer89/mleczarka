using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mleczarka.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Animacje czynności rolnika (Rolnik.controller, parametr „Act”): bierze ze stołu, przelewa, otwiera, doi w przyklęku, podaje —
    /// klipy z biblioteki Quaternius UAL (CC0). Do tego obiekt „Rekwizyty czynności” w scenie (wiadro do dojenia, łyżka).
    /// </summary>
    public static class ActionAnimSetup
    {
        const string Q = "Assets/_Mleczarka/Art/ThirdParty/Quaternius/";
        const string Ctrl = "Assets/_Mleczarka/Art/Generated/Characters/Rolnik.controller";
        static readonly (ActionAnim.Kind kind, string state, string clip, bool loop)[] Acts =
        {
            (ActionAnim.Kind.Bierze, "Czynność: bierze", "PickUp_Table", false),
            (ActionAnim.Kind.Przelewa, "Czynność: przelewa", "Farm_Watering", true),
            (ActionAnim.Kind.Otwiera, "Czynność: otwiera", "Chest_Open", false),
            (ActionAnim.Kind.Doi, "Czynność: doi", "Fixing_Kneeling", true),
            (ActionAnim.Kind.Podaje, "Czynność: podaje", "Interact", false),
        };

        [MenuItem("Mleczarka/Kariera/9. Animacje czynności i rekwizyty")]
        public static void Run()
        {
            if (Application.isPlaying) return;
            var log = new StringBuilder();
            var clips = new[] { "UniversalAnimationLibrary/UAL1_Standard.fbx", "UniversalAnimationLibrary2/UAL2_Standard.fbx" }
                .SelectMany(f => AssetDatabase.LoadAllAssetsAtPath(Q + f).OfType<AnimationClip>()).Where(c => !c.name.StartsWith("__preview")).ToList();
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Ctrl);
            if (!ctrl) { Debug.LogError("[Mleczarka] Brak " + Ctrl); return; }
            if (!ctrl.parameters.Any(p => p.name == "Act")) ctrl.AddParameter("Act", AnimatorControllerParameterType.Int);
            var sm = ctrl.layers[0].stateMachine;
            var loco = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Ruch") ?? sm.defaultState;
            // poprzednia wersja
            foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState && t.destinationState.name.StartsWith("Czynność")).ToArray()) sm.RemoveAnyStateTransition(t);
            foreach (var st in sm.states.Select(s => s.state).Where(s => s.name.StartsWith("Czynność")).ToArray()) sm.RemoveState(st);
            foreach (var (kind, name, clipName, loop) in Acts)
            {
                var clip = clips.FirstOrDefault(c => c.name == clipName);
                if (!clip) { log.AppendLine($"brak klipu {clipName}"); continue; }
                var st = sm.AddState(name); st.motion = clip;
                var tin = sm.AddAnyStateTransition(st);
                tin.AddCondition(AnimatorConditionMode.Equals, (int)kind, "Act"); tin.AddCondition(AnimatorConditionMode.IfNot, 0, "Bike");
                tin.hasExitTime = false; tin.duration = 0.18f; tin.canTransitionToSelf = false;
                var tout = st.AddTransition(loco); tout.AddCondition(AnimatorConditionMode.NotEqual, (int)kind, "Act"); tout.hasExitTime = false; tout.duration = 0.25f;
                var tmove = st.AddTransition(loco); tmove.AddCondition(AnimatorConditionMode.Greater, 0.8f, "Speed"); tmove.hasExitTime = false; tmove.duration = 0.15f;
                if (loop && !clip.isLooping)
                {
                    // klip nie jest pętlą: wracamy do środka czynności, dopóki trwa
                    var again = st.AddTransition(st); again.AddCondition(AnimatorConditionMode.Equals, (int)kind, "Act"); again.hasExitTime = true; again.exitTime = 0.9f; again.duration = 0.2f; again.offset = 0.25f;
                }
                else if (!loop)
                {
                    var end = st.AddTransition(loco); end.hasExitTime = true; end.exitTime = 0.95f; end.duration = 0.2f;
                }
                log.AppendLine($"{name}: {clip.name} ({clip.length:0.0} s, pętla {clip.isLooping})");
            }
            EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();

            // rekwizyty w scenie
            var props = Object.FindFirstObjectByType<ActionProps>();
            if (!props) props = new GameObject("Rekwizyty czynności").AddComponent<ActionProps>();
            props.bucket = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Mleczarka/Art/Career/Wiadro.fbx");
            props.spoon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Mleczarka/Art/Career/Lyzka.fbx");
            log.AppendLine($"Rekwizyty: wiadro {(props.bucket ? "jest" : "BRAK")}, łyżka {(props.spoon ? "jest" : "BRAK")}");
            EditorUtility.SetDirty(props);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Screenshots"); File.WriteAllText("Screenshots/czynnosci.txt", log.ToString());
            Debug.Log("[Mleczarka] Animacje czynności: Screenshots/czynnosci.txt\n" + log);
        }
    }
}
