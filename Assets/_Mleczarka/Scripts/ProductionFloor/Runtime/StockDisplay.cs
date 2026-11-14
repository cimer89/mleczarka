using System.Linq;
using UnityEngine;
using Mleczarka.Core;
using Mleczarka.Gameplay;

namespace Mleczarka.ProductionFloor
{
    public enum StockLocation { Packed, Warehouse, Van }
    public sealed class StockDisplay : MonoBehaviour
    {
        public StockLocation location;
        public Product product;
        public GameObject itemPrefab;
        GameObject[] boxes;
        Material material;
        void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) { enabled = false; return; }
            material = new Material(shader); material.color = product == Product.Uht ? Color.white : new Color(0.65f, 0.85f, 1);
            boxes = new GameObject[12];
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = itemPrefab ? Instantiate(itemPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = "Produkt " + (i + 1);
                foreach (var collider in box.GetComponentsInChildren<Collider>()) Destroy(collider); box.transform.SetParent(transform, false);
                box.transform.localPosition = new Vector3((i % 4 - 1.5f) * 0.45f, (i / 4) * 0.6f, 0);
                box.transform.localScale = itemPrefab ? Vector3.one : new Vector3(0.38f, 0.55f, 0.38f);
                if (!itemPrefab) box.GetComponent<Renderer>().sharedMaterial = material; boxes[i] = box;
            }
        }
        void Update()
        {
            var gc = GameController.Instance;
            if (!gc || boxes == null) return;
            var p = gc.Sim.S.players[0];
            int qty = location == StockLocation.Packed ? p.packingRack[(int)product] : location == StockLocation.Warehouse ? p.store[(int)product] : p.vanCargo[(int)product];
            for (int i = 0; i < boxes.Length; i++) boxes[i].SetActive(i < qty);
        }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
