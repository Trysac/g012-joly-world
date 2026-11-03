using UnityEngine;

namespace JolyWorld.Game
{
    /// <summary>
    /// Small pooled bits that fly, fall and shrink: coin and gem sparkles, puffs when a critter
    /// pops, dust when the hero lands, splashes in the river and the dash's trail.
    /// </summary>
    public sealed class Effects : MonoBehaviour
    {
        const int Size = 220;

        sealed class Bit
        {
            public Transform Transform;
            public Renderer Renderer;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float Gravity;
            public float Life;
            public float Age;
            public float Scale;
        }

        readonly Bit[] bits = new Bit[Size];
        MaterialPropertyBlock block;
        int next;

        public int Active { get; private set; }

        public void Init(GameArt art)
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < Size; i++)
            {
                var host = new GameObject("Bit");
                host.transform.SetParent(transform, false);
                host.AddComponent<MeshFilter>().sharedMesh = i % 2 == 0 ? Models.Bit : Models.Spark;
                var renderer = host.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = art.Glowing;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                host.SetActive(false);
                bits[i] = new Bit { Transform = host.transform, Renderer = renderer };
            }
        }

        void Emit(Vector3 at, Vector3 velocity, Color color, float scale, float life, float gravity)
        {
            Bit bit = bits[next];
            next = (next + 1) % Size;
            bit.Transform.position = at;
            bit.Velocity = velocity;
            bit.Spin = Random.insideUnitSphere * 540f;
            bit.Gravity = gravity;
            bit.Life = life;
            bit.Age = 0f;
            bit.Scale = scale;
            bit.Transform.localScale = Vector3.one * scale;
            block.SetColor("_Color", color);
            bit.Renderer.SetPropertyBlock(block);
            bit.Transform.gameObject.SetActive(true);
        }

        public void Burst(Vector3 at, Color color, int count, float speed, float scale = 0.14f, float life = 0.6f, float gravity = 9f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Random.insideUnitSphere;
                direction.z *= 0.5f;
                Emit(at, direction * speed + Vector3.up * speed * 0.4f, Color.Lerp(color, Color.white, Random.value * 0.35f),
                    scale * Random.Range(0.6f, 1.3f), life * Random.Range(0.7f, 1.2f), gravity);
            }
        }

        public void Sparkle(Vector3 at, Color color) => Burst(at, color, 10, 3.2f, 0.11f, 0.45f, 2f);

        public void Puff(Vector3 at) => Burst(at, Color.white, 14, 3f, 0.22f, 0.5f, -1f);

        public void Dust(Vector3 feet)
        {
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Emit(feet + new Vector3(side * 0.2f, 0.05f, Random.Range(-0.2f, 0.2f)), new Vector3(side * Random.Range(1f, 2.2f), Random.Range(0.3f, 1f), 0f),
                    Palette.Hex("#efe6d0"), Random.Range(0.1f, 0.18f), 0.35f, 0f);
            }
        }

        public void Splash(Vector3 at) => Burst(at, Palette.Water, 22, 4.5f, 0.15f, 0.8f, 14f);

        public void Trail(Vector3 at, Color color) =>
            Emit(at + Random.insideUnitSphere * 0.2f, Vector3.zero, color, 0.18f, 0.25f, 0f);

        void Update()
        {
            float dt = Time.deltaTime;
            int active = 0;
            foreach (Bit bit in bits)
            {
                if (!bit.Transform.gameObject.activeSelf)
                {
                    continue;
                }
                bit.Age += dt;
                if (bit.Age >= bit.Life)
                {
                    bit.Transform.gameObject.SetActive(false);
                    continue;
                }
                active++;
                bit.Velocity += Vector3.down * bit.Gravity * dt;
                bit.Velocity *= 1f - Mathf.Min(1f, dt * 1.5f);
                bit.Transform.position += bit.Velocity * dt;
                bit.Transform.Rotate(bit.Spin * dt);
                bit.Transform.localScale = Vector3.one * bit.Scale * (1f - bit.Age / bit.Life);
            }
            Active = active;
        }

        public void Clear()
        {
            foreach (Bit bit in bits)
            {
                bit.Transform.gameObject.SetActive(false);
            }
        }
    }
}
