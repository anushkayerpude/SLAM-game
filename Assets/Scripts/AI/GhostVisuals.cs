using UnityEngine;

namespace NightWarden.AI
{
    /// <summary>
    /// Transforms the security guards into terrifying spectral ghosts.
    /// Handles vertical levitation/hovering, floating phantom eyes, ectoplasm particles,
    /// eerie cyan spectral light emission, and pulsating translucent phantom shaders.
    /// </summary>
    public class GhostVisuals : MonoBehaviour
    {
        [Header("Hover & Levitation")]
        [SerializeField] private float hoverAmplitude = 0.18f;
        [SerializeField] private float hoverFrequency = 3.2f;
        [SerializeField] private Transform visualTransform;

        [Header("Spectral Glow")]
        [SerializeField] private Color spectralColor = new Color(0.15f, 0.95f, 0.85f, 0.65f); // Neon phantom cyan
        [SerializeField] private Color eyeGlowColor = new Color(0.3f, 1.8f, 1.5f, 1.0f);

        private float _seed;
        private Light _spotlight;
        private Light _auraLight;
        private Material _ghostMaterial;
        private ParticleSystem _ectoParticles;

        private void Awake()
        {
            _seed = Random.Range(0f, 100f);

            // Locate or auto-setup visual container
            if (visualTransform == null)
            {
                visualTransform = transform;
            }

            SetupGhostMaterialsAndEyes();
            SetupSpectralLights();
            SetupEctoplasmParticles();
        }

        private void SetupGhostMaterialsAndEyes()
        {
            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _ghostMaterial = new Material(litShader);
                _ghostMaterial.name = "Mat_Ghost_Instance";
                
                // Configure transparent surface in URP
                _ghostMaterial.SetFloat("_Surface", 1f); // Transparent
                _ghostMaterial.SetFloat("_Blend", 0f);   // Alpha
                _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _ghostMaterial.SetInt("_ZWrite", 0);
                _ghostMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

                _ghostMaterial.SetColor("_BaseColor", spectralColor);
                _ghostMaterial.SetFloat("_Smoothness", 0.95f);
                _ghostMaterial.SetFloat("_Metallic", 0.0f);
                _ghostMaterial.EnableKeyword("_EMISSION");
                _ghostMaterial.SetColor("_EmissionColor", new Color(0.1f, 1.2f, 1.0f) * 1.8f);

                mr.material = _ghostMaterial;
            }

            // Create Piercing Floating Ghost Eyes
            CreateGhostEye("Ghost_Eye_Left", new Vector3(-0.16f, 0.72f, 0.42f));
            CreateGhostEye("Ghost_Eye_Right", new Vector3(0.16f, 0.72f, 0.42f));

            // Floating Spectral Shroud / Tapering Ghost Tail
            GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tail.name = "Ghost_SpectralTail";
            tail.transform.SetParent(transform, false);
            tail.transform.localPosition = new Vector3(0f, -0.65f, -0.15f);
            tail.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            tail.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            Destroy(tail.GetComponent<Collider>());
            if (_ghostMaterial != null) tail.GetComponent<MeshRenderer>().material = _ghostMaterial;
        }

        private void CreateGhostEye(string eyeName, Vector3 localPos)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = eyeName;
            eye.transform.SetParent(transform, false);
            eye.transform.localPosition = localPos;
            eye.transform.localScale = Vector3.one * 0.14f;
            Destroy(eye.GetComponent<Collider>());

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material eyeMat = new Material(litShader);
            eyeMat.SetColor("_BaseColor", Color.white);
            eyeMat.EnableKeyword("_EMISSION");
            eyeMat.SetColor("_EmissionColor", eyeGlowColor * 3.5f);
            eye.GetComponent<MeshRenderer>().material = eyeMat;
        }

        private void SetupSpectralLights()
        {
            _spotlight = GetComponentInChildren<Light>();
            if (_spotlight != null)
            {
                _spotlight.color = new Color(0.2f, 1.0f, 0.9f); // Phantom teal
                _spotlight.intensity = 4.0f;
                _spotlight.range = 24f;
            }

            // Ambient ghost aura light
            GameObject auraObj = new GameObject("Ghost_AuraLight");
            auraObj.transform.SetParent(transform, false);
            auraObj.transform.localPosition = Vector3.up * 0.8f;
            _auraLight = auraObj.AddComponent<Light>();
            _auraLight.type = LightType.Point;
            _auraLight.color = new Color(0.1f, 0.95f, 0.85f);
            _auraLight.intensity = 2.0f;
            _auraLight.range = 6.5f;
            _auraLight.shadows = LightShadows.None;
        }

        private void SetupEctoplasmParticles()
        {
            GameObject ectoObj = new GameObject("Ghost_EctoplasmParticles");
            ectoObj.transform.SetParent(transform, false);
            ectoObj.transform.localPosition = Vector3.zero;

            _ectoParticles = ectoObj.AddComponent<ParticleSystem>();
            var main = _ectoParticles.main;
            main.maxParticles = 65;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.5f;
            main.startSize = 0.35f;
            main.startColor = new Color(0.2f, 1.0f, 0.85f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _ectoParticles.emission;
            emission.rateOverTime = 28f;

            var shape = _ectoParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;

            var velOverLifetime = _ectoParticles.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.y = new ParticleSystem.MinMaxCurve(0.6f); // Drift upwards

            var colorOverLifetime = _ectoParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.2f, 1.0f, 0.85f), 0f), new GradientColorKey(new Color(0.05f, 0.4f, 0.8f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;
        }

        private void Update()
        {
            // Vertical levitation hover sine wave
            float hover = Mathf.Sin(Time.time * hoverFrequency + _seed) * hoverAmplitude;
            if (visualTransform != null && visualTransform != transform)
            {
                visualTransform.localPosition = new Vector3(visualTransform.localPosition.x, hover, visualTransform.localPosition.z);
            }

            // Pulsate spectral emission
            if (_ghostMaterial != null)
            {
                float pulse = 1.4f + Mathf.Sin(Time.time * 4.5f + _seed) * 0.5f;
                _ghostMaterial.SetColor("_EmissionColor", new Color(0.1f, 1.2f, 1.0f) * pulse);
            }
        }
    }
}
