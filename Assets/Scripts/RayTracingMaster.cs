using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture Skybox;
    public float SkyBrightness;

    public int NumSpheres;
    public float SpherePlacementRadius;
    public Vector2 SphereRadius;
    public Vector3 SpherePosition;
    private RenderTexture _target;
    private RenderTexture _converged;
    
    private uint _currentSample = 0;
    private Material _addMaterial;
    private ComputeBuffer _sphereBuffer;

    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();

    struct MeshObject {
    public Matrix4x4 localToWorldMatrix;
    public int indices_offset;
    public int indices_count;
    }

    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

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
        if (transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
        }
    }

    private void Render(RenderTexture destination)
    {
        // Make sure we have a current render target
        InitRenderTexture();

        // Update mesh data if needed
        RebuildMeshObjectBuffers();

        // Set the target and dispatch the compute shader
        RayTracingShader.SetTexture(0, "Result", _target);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", Skybox);

        SetComputeBuffer("_Spheres", _sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);

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
        CreateLighting();
    }
    private void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
        
        if (_meshObjectBuffer != null)
            _meshObjectBuffer.Release();

        if (_vertexBuffer != null)
            _vertexBuffer.Release();
        
        if (_indexBuffer != null)
            _indexBuffer.Release();
    }

    private void CreateLighting(){

        List<Sphere> spheres = new List<Sphere>();

        Sphere s = new Sphere();
        Vector3 _SpherePosition = SpherePosition;

        // lighting 1:
        s.position = _SpherePosition;
        s.radius = 3;
        s.emission = new Vector3(15.0f, 15.0f, 15.0f);
        s.albedo = Vector3.one;
        s.specular = Vector3.one;
        s.smoothness = 1;

        // lighting 2:
        // s.position = _SpherePosition;
        // s.radius = 4;
        // s.emission = new Vector3(8.0f, 8.0f, 8.0f);
        // s.albedo = new Vector3(1.0f, 0.4f, 0.9f);
        // s.specular = Vector3.one;
        // s.smoothness = 3;

        // lighting 3:
        // s.position = _SpherePosition;
        // s.radius = 1;
        // s.emission = new Vector3(3.0f, 3.0f, 3.0f);
        // s.albedo = Vector3.one;
        // s.specular = Vector3.one;
        // s.smoothness = 0;

        spheres.Add(s);

         // Assign to compute buffer
        _sphereBuffer = new ComputeBuffer(spheres.Count, SPHERE_STRIDE);
        _sphereBuffer.SetData(spheres);

    }

    private void CreateRandomSpheres()
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

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }
    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }
        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;

        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();

        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;

            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);

            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));

            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
            localToWorldMatrix = obj.transform.localToWorldMatrix,
            indices_offset = firstIndex,
            indices_count = indices.Length
            });
        }
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 72);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
    where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }
                // Set data on the buffer
                buffer.SetData(data);
            }
    }
    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }
}