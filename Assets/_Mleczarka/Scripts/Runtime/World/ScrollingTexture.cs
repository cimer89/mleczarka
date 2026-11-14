using UnityEngine;

namespace Mleczarka.World
{
    /// <summary>Przesuwa mapę normalnych materiału (płynąca woda). Działa na materiale współdzielonym, więc jedna instancja na materiał wystarczy.</summary>
    public class ScrollingTexture : MonoBehaviour
    {
        public Material material;
        public Vector2 speed = new Vector2(0.0f, 0.035f);
        public string property = "_BumpMap";
        Vector2 _off;
        void Update()
        {
            if (!material) return;
            _off += speed * Time.deltaTime;
            _off.x %= 1f; _off.y %= 1f;
            material.SetTextureOffset(property, _off);
        }
    }
}
