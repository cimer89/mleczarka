using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Mleczarka.ProductionFloor.EditorTools
{
    /// <summary>
    /// Skok rolnika bez „ślizgania”: krótsze odbicie (Jump_Start), po dotknięciu ziemi w biegu od razu chód/bieg
    /// zamiast stojącego lądowania, a lądowanie w miejscu da się przerwać ruchem.
    /// Uruchamia się samo raz po kompilacji (zmienia tylko przejścia w Rolnik.controller), można też z menu.
    /// </summary>
    [InitializeOnLoad]
    public static class JumpAnimFix
    {
        const string Path = "Assets/_Mleczarka/Art/Generated/Characters/Rolnik.controller";

        static JumpAnimFix() => EditorApplication.delayCall += () => { if (!Application.isPlaying) Apply(false); };

        [MenuItem("Mleczarka/Produkcja/15. Popraw skok i lądowanie")]
        static void Menu() => Apply(true);

        static AnimatorState S(AnimatorStateMachine sm, string name) => sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

        static void Apply(bool force)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
            if (!ctrl) return;
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState jump = S(sm, "Skok"), air = S(sm, "W powietrzu"), land = S(sm, "Lądowanie"), loco = S(sm, "Ruch");
            if (!jump || !air || !land || !loco) { Debug.LogWarning("[Mleczarka] Skok: brak stanów w Rolnik.controller."); return; }
            // rower: stopy na pedałach i dłonie na kierownicy (BikeRiderIK) wymagają IK Pass w warstwie bazowej
            var layers = ctrl.layers;
            if (!layers[0].iKPass) { layers[0].iKPass = true; ctrl.layers = layers; EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets(); Debug.Log("[Mleczarka] Rower: włączone IK Pass w warstwie bazowej."); }
            bool done = jump.transitions.Any(t => t.destinationState == loco);
            if (done && !force) return;

            // 1) odbicie krótsze: z Jump_Start do pętli w powietrzu po 35% klipu (wcześniej 90% — przykucnięcie ciągnęło się po ziemi)
            foreach (var t in jump.transitions.Where(t => t.destinationState == air)) { t.hasExitTime = true; t.exitTime = 0.35f; t.duration = 0.1f; }

            // 2) ziemia + ruch → od razu chód/bieg (z odbicia i z powietrza), przed zwykłym lądowaniem
            void ToLoco(AnimatorState from, bool exit)
            {
                foreach (var old in from.transitions.Where(t => t.destinationState == loco).ToArray()) from.RemoveTransition(old);
                var t = from.AddTransition(loco);
                t.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
                t.AddCondition(AnimatorConditionMode.Greater, 1.0f, "Speed");
                t.hasExitTime = exit; t.exitTime = 0.3f; t.duration = 0.12f;
                // na początek listy — ma pierwszeństwo przed lądowaniem
                var list = from.transitions.ToList(); list.Remove(t); list.Insert(0, t); from.transitions = list.ToArray();
            }
            ToLoco(jump, true);   // z odbicia dopiero po 30% (w klatce skoku postać jeszcze stoi na ziemi)
            ToLoco(air, false);

            // 3) lądowanie w miejscu: krótsze i przerywane ruchem
            foreach (var t in land.transitions.Where(t => t.destinationState == loco).ToArray()) land.RemoveTransition(t);
            var move = land.AddTransition(loco);
            move.AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed"); move.hasExitTime = false; move.duration = 0.1f;
            var end = land.AddTransition(loco); end.hasExitTime = true; end.exitTime = 0.4f; end.duration = 0.15f;

            // 4) z chodu do „w powietrzu” tylko przy dłuższym spadku (gra i tak wygładza Grounded)
            foreach (var t in loco.transitions.Where(t => t.destinationState == air)) t.duration = 0.2f;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            Debug.Log("[Mleczarka] Skok: poprawione przejścia animacji (odbicie, lądowanie w biegu).");
        }
    }
}
