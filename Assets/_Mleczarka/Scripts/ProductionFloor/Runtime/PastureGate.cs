using Mleczarka.Gameplay;
using UnityEngine;

namespace Mleczarka.ProductionFloor
{
    /// <summary>
    /// Furtka w ogrodzeniu pastwiska: otwiera się do środka, gdy gracz podchodzi, i zamyka, gdy odejdzie.
    /// Skrzydło (dziecko „Furtka_Skrzydlo”) obraca się na zawiasie razem ze swoją kolizją.
    /// </summary>
    public class PastureGate : MonoBehaviour
    {
        public Transform leaf;
        public float openAngle = 95f, openDistance = 3.2f, speed = 140f;
        Quaternion _closed; float _angle;

        void Start()
        {
            if (!leaf) leaf = transform.Find("Furtka_Skrzydlo");
            if (leaf) _closed = leaf.localRotation;
        }

        void Update()
        {
            if (!leaf) return;
            var g = GameController.Instance;
            bool near = g && g.player && Flat(g.player.transform.position - transform.position) < openDistance;
            float target = near ? openAngle : 0f;
            if (Mathf.Approximately(_angle, target)) return;
            if (near && Mathf.Approximately(_angle, 0f)) GameAudio.Play("Furtka_Skrzypi", leaf.position, 0.7f, 1f, 2.2f);
            _angle = Mathf.MoveTowards(_angle, target, speed * Time.deltaTime);
            leaf.localRotation = _closed * Quaternion.Euler(0, _angle, 0);
        }

        static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }
    }
}
