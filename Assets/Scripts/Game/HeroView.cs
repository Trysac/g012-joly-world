using JolyWorld.Core;
using UnityEngine;

namespace JolyWorld.Game
{
    /// <summary>
    /// The hero: a child in a white T-shirt, khaki shorts and blue trainers, like the 2021
    /// prototype's, built from low-poly parts with pivots at the shoulders and hips and animated in
    /// code (idle breathing, walking and running, jumping, falling, dashing, a hurt flash and a
    /// cheer at the goal).
    /// </summary>
    public sealed class HeroView : MonoBehaviour
    {
        Transform body;
        Transform head;
        Transform leftArm, rightArm, leftLeg, rightLeg;
        Renderer[] renderers;
        float stride;
        float yaw = 25f;
        float squash = 1f;
        float time;

        /// <summary>Plays the goal cheer instead of the running animation.</summary>
        public bool Cheering { get; set; }

        public static HeroView Create(GameArt art, Transform parent = null)
        {
            var root = new GameObject("Hero");
            root.transform.SetParent(parent, false);
            HeroView view = root.AddComponent<HeroView>();
            view.Build(art.LowPoly);
            return view;
        }

        Transform Part(string name, Transform parent, Vector3 at, Material material, System.Action<MeshBuilder> build)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = at;
            if (build != null)
            {
                var builder = new MeshBuilder(name.GetHashCode()) { Variation = 0.03f };
                build(builder);
                host.AddComponent<MeshFilter>().sharedMesh = builder.ToMesh("Hero " + name);
                host.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            return host.transform;
        }

        void Build(Material material)
        {
            body = Part("Body", transform, Vector3.zero, material, null);
            // Torso: the white T-shirt with short sleeves and a little logo.
            Part("Torso", body, new Vector3(0f, 0.72f, 0f), material, b =>
            {
                b.Blob(new Vector3(0f, 0.08f, 0f), new Vector3(0.24f, 0.27f, 0.17f), Palette.Shirt, 0.03f);
                b.Block(new Vector3(0f, -0.14f, 0f), new Vector3(0.4f, 0.12f, 0.3f), Palette.Shorts);
                b.Blob(new Vector3(0.05f, 0.12f, -0.16f), new Vector3(0.06f, 0.06f, 0.02f), Palette.Shoes, 0f, 0);
            });
            head = Part("Head", body, new Vector3(0f, 1.08f, 0f), material, b =>
            {
                b.Blob(new Vector3(0f, 0.12f, 0f), new Vector3(0.24f, 0.25f, 0.23f), Palette.Skin, 0.03f);
                b.TwoToneBlob(new Vector3(-0.02f, 0.2f, 0f), new Vector3(0.27f, 0.22f, 0.26f), Palette.Skin, Palette.Hair, 0.05f, 0.08f);
                b.Blob(new Vector3(0.2f, 0.14f, -0.08f), new Vector3(0.035f, 0.05f, 0.035f), Palette.Hex("#1a1010"), 0f, 0);
                b.Blob(new Vector3(0.2f, 0.14f, 0.08f), new Vector3(0.035f, 0.05f, 0.035f), Palette.Hex("#1a1010"), 0f, 0);
                b.Blob(new Vector3(0f, 0.1f, -0.22f), new Vector3(0.05f, 0.07f, 0.04f), Palette.Skin, 0f, 0);
                b.Blob(new Vector3(0f, 0.1f, 0.22f), new Vector3(0.05f, 0.07f, 0.04f), Palette.Skin, 0f, 0);
            });
            leftArm = Arm("LeftArm", -1f, material);
            rightArm = Arm("RightArm", 1f, material);
            leftLeg = Leg("LeftLeg", -1f, material);
            rightLeg = Leg("RightLeg", 1f, material);
            renderers = GetComponentsInChildren<Renderer>();
        }

        Transform Arm(string name, float side, Material material) => Part(name, body, new Vector3(0f, 0.92f, side * 0.25f), material, b =>
        {
            b.Blob(new Vector3(0f, -0.04f, 0f), new Vector3(0.09f, 0.08f, 0.08f), Palette.Shirt, 0f, 0);
            b.Block(new Vector3(0f, -0.2f, 0f), new Vector3(0.1f, 0.26f, 0.1f), Palette.Skin);
            b.Blob(new Vector3(0f, -0.36f, 0f), Vector3.one * 0.065f, Palette.Skin, 0f, 0);
        });

        Transform Leg(string name, float side, Material material) => Part(name, body, new Vector3(0f, 0.5f, side * 0.11f), material, b =>
        {
            b.Block(new Vector3(0f, -0.06f, 0f), new Vector3(0.17f, 0.14f, 0.16f), Palette.Shorts);
            b.Block(new Vector3(0f, -0.25f, 0f), new Vector3(0.11f, 0.28f, 0.11f), Palette.Skin);
            b.Block(new Vector3(0.05f, -0.44f, 0f), new Vector3(0.24f, 0.11f, 0.14f), Palette.Shoes, Palette.Shade(Palette.Shoes, 0.2f));
            b.Block(new Vector3(0.05f, -0.5f, 0f), new Vector3(0.25f, 0.03f, 0.15f), Color.white);
        });

        /// <summary>Puts the hero where the motor is and poses it.</summary>
        public void Sync(Motor motor, Vitals vitals, float dt)
        {
            time += dt;
            transform.localPosition = new Vector3(motor.Position.X, motor.Position.Y, 0f);
            float speed = Mathf.Abs(motor.Velocity.X);

            // Turn to face the way the hero walks, three-quarters to the camera like in 2021.
            float wanted = motor.Facing > 0 ? 25f : 155f;
            if (Cheering)
            {
                wanted = 90f;
            }
            yaw = Mathf.MoveTowardsAngle(yaw, wanted, dt * 900f);
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            float swing, armSwing, lean = 0f;
            if (Cheering)
            {
                swing = 0f;
                armSwing = 0f;
                float hop = Mathf.Abs(Mathf.Sin(time * 7f));
                body.localPosition = Vector3.up * hop * 0.25f;
                leftArm.localRotation = Quaternion.Euler(150f + hop * 20f, 0f, 0f);
                rightArm.localRotation = Quaternion.Euler(-150f - hop * 20f, 0f, 0f);
                leftLeg.localRotation = Quaternion.identity;
                rightLeg.localRotation = Quaternion.identity;
                SetVisible(true);
                return;
            }
            if (motor.Dashing)
            {
                swing = 0f;
                armSwing = 0f;
                lean = -30f;
                leftArm.localRotation = Quaternion.Euler(0f, 0f, 70f);
                rightArm.localRotation = Quaternion.Euler(0f, 0f, 70f);
                leftLeg.localRotation = Quaternion.Euler(0f, 0f, 35f);
                rightLeg.localRotation = Quaternion.Euler(0f, 0f, -25f);
            }
            else if (!motor.Grounded)
            {
                bool rising = motor.Velocity.Y > 0f;
                leftLeg.localRotation = Quaternion.Euler(0f, 0f, rising ? 45f : 20f);
                rightLeg.localRotation = Quaternion.Euler(0f, 0f, rising ? -20f : -10f);
                leftArm.localRotation = Quaternion.Euler(rising ? 55f : 100f, 0f, rising ? 25f : 0f);
                rightArm.localRotation = Quaternion.Euler(rising ? -55f : -100f, 0f, rising ? -25f : 0f);
                swing = 0f;
                armSwing = 0f;
            }
            else
            {
                float rate = Mathf.Lerp(0f, 13f, speed / Motor.SprintSpeed);
                stride += dt * rate;
                float amount = Mathf.Clamp01(speed / Motor.RunSpeed);
                swing = Mathf.Sin(stride) * 40f * amount;
                armSwing = swing * 0.9f;
                leftLeg.localRotation = Quaternion.Euler(0f, 0f, swing);
                rightLeg.localRotation = Quaternion.Euler(0f, 0f, -swing);
                leftArm.localRotation = Quaternion.Euler(0f, 0f, -armSwing);
                rightArm.localRotation = Quaternion.Euler(0f, 0f, armSwing);
                lean = -speed * 1.2f;
            }

            // Breathing when still, a bounce in the step when running, a squash on landing.
            float bob = motor.Grounded ? (speed > 0.5f ? Mathf.Abs(Mathf.Cos(stride)) * 0.05f : Mathf.Sin(time * 2.5f) * 0.012f) : 0f;
            squash = Mathf.MoveTowards(squash, 1f, dt * 4f);
            body.localPosition = Vector3.up * bob;
            body.localScale = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
            body.localRotation = Quaternion.Euler(0f, 0f, lean);
            head.localRotation = Quaternion.Euler(0f, 0f, -lean * 0.4f);

            // Flicker while safe after a hit.
            SetVisible(vitals == null || !vitals.Invulnerable || Mathf.Repeat(time * 12f, 1f) < 0.6f);
        }

        /// <summary>A quick squash, as when the hero lands.</summary>
        public void Squash(float amount = 0.78f) => squash = amount;

        void SetVisible(bool visible)
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = visible;
            }
        }
    }
}
