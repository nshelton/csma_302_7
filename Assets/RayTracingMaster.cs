using UnityEngine;
using System.Collections.Generic;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture Skybox;
    public float SkyBrightness;

    public int NumSpheres;
    public float SpherePlacementRadius;
    public Vector2 SphereRadius;

    private RenderTexture _target;
    private RenderTexture _converged;
    
    private uint _currentSample = 0;
    private Material _addMaterial;
    private ComputeBuffer _sphereBuffer;

    private int SPHERE_STRIDE = 56;
    // 56 bytes total
    struct Sphere {
        public Vector3 position; //3 floats * 4 bytes = 12 bytes
        public float radius; 
        public Vector3 albedo;
        public Vector3 specular;
        public Vector3 emission;
        public float smoothness;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Render(destination);
    }

    private void Update()
    {
        if (transform.hasChanged || DirectionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
            DirectionalLight.transform.hasChanged = false;
        }
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();
        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", Skybox);

        if ( _sphereBuffer != null) {
            RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
        }

        RayTracingShader.SetMatrix("_CameraToWorld", Camera.main.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", Camera.main.projectionMatrix.inverse);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        RayTracingShader.SetFloat("_Seed", Random.value);
        RayTracingShader.SetFloat("_SkyBrightness", SkyBrightness);

        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        if (_addMaterial == null) {
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        }

        _addMaterial.SetFloat("_Sample", _currentSample);

        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);

        _currentSample++;
    }

    private void InitRenderTexture()
    {
        if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
        {
            // Release render texture if we already have one
            if (_target != null)
                _target.Release();
            if (_converged != null)
                _converged.Release();

            // Get a render target for Ray Tracing
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

            _target.enableRandomWrite = true;
            _target.Create();

            _converged = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

            _converged.enableRandomWrite = true;
            _converged.Create();
        }
    }

    private void OnEnable()
    {
        _currentSample = 0;
        SetUpScene();
    }
    private void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }
    private void SetUpScene()
    {
        List<Sphere> spheres = new List<Sphere>();


        // Add a number of random spheres
        for (int i = 0; i < NumSpheres; i++)
        {
            Sphere sphere = new Sphere();
            // Radius and radius
            sphere.radius = SphereRadius.x + Random.value * (SphereRadius.y - SphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);
            // Reject spheres that are intersecting others
            foreach (Sphere other in spheres)
            {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }

            // Albedo and specular color
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.3f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            bool emissive = Random.value < 0.3f;
            if (emissive) {
                sphere.emission = new Vector3(color.r, color.g, color.b) * 3f;
            }
            
            sphere.smoothness = Random.value;
            
            // Add the sphere to the list
            spheres.Add(sphere);
        SkipSphere:
            continue;
        }
        // Assign to compute buffer
        _sphereBuffer = new ComputeBuffer(spheres.Count, SPHERE_STRIDE);
        _sphereBuffer.SetData(spheres);
    }
}