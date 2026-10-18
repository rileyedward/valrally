using UnityEngine;

namespace BifrostRally.Core
{
    /// <summary>
    /// Builds a placeholder box car out of Unity primitives.
    /// </summary>
    public static class CarFactory
    {
        public static RaycastCar CreateBoxCar(Vector3 position, Quaternion rotation, CarSettings settings, FeelSettings feel, int layer, int groundMask, Material material = null)
        {
            var root = new GameObject("BifrostRallyCar");
            root.transform.SetPositionAndRotation(position, rotation);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.7f, 0.6f, 3.9f);

            var body = root.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            CreateVisual(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, collider.size, material);
            CreateVisual(PrimitiveType.Cube, "Cabin", root.transform, new Vector3(0f, 0.5f, -0.3f), new Vector3(1.4f, 0.45f, 1.8f), material);

            var pivots = new Transform[4];
            var tyres = new Transform[4];
            float diameter = settings.WheelRadius * 2f;
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("WheelPivot" + i).transform;
                pivot.SetParent(root.transform, false);
                pivots[i] = pivot;
                // A Unity cylinder is 2 units tall along local Y; the car rotates it onto the axle.
                tyres[i] = CreateVisual(PrimitiveType.Cylinder, "Tyre" + i, pivot, Vector3.zero, new Vector3(diameter, 0.12f, diameter), material);
            }

            var seat = new GameObject("Seat").transform;
            seat.SetParent(root.transform, false);
            seat.localPosition = new Vector3(-0.35f, 0.3f, -0.2f);

            SetLayer(root, layer);

            var car = root.AddComponent<RaycastCar>();
            car.Seat = seat;
            car.Init(settings, feel, groundMask, pivots, tyres);
            return car;
        }

        private static Transform CreateVisual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // Immediate, so the visuals never add mass or block the wheel rays.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (material != null)
            {
                go.GetComponent<Renderer>().sharedMaterial = material;
            }
            return go.transform;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayer(child.gameObject, layer);
            }
        }
    }
}
