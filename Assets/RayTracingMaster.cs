using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RayTracingMaster : MonoBehaviour
{
    public ComputeShader RayTracingShader;
    public Texture Skybox;
    public float SkyBrightness;

    // public int NumSpheres;
    // public float SpherePlacementRadius;
    // public Vector2 SphereRadius;

    public Color albedo = new Color(0.5f, 0.5f, 0.5f);
    [Range(0,1)] public float smoothness = 0.5f;
    [Range(0,1)] public float specular = 0.5f;
    public Color emission = new Color(0.5f, 0.5f, 0.5f);

    [Range(1,12)] public int bounces = 8;

    private RenderTexture _target;
    private RenderTexture _converged;
    
    private uint _currentSample = 0;
    private Material _addMaterial;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();
    private ComputeBuffer _sphereBuffer;

    struct MeshObject
    {
        public Matrix4x4 localToWorldMatrix;
        public int indices_offset;
        public int indices_count;

        public Vector3 albedo;
        public float smoothness;
        public float specular;
        public Vector3 emission;
    }

    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<MeshObject> _oldMeshObjects = new List<MeshObject>();
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
        public Vector3  emission;
        public float smoothness;
    }

    private static bool _meshObjectsNeedRebuilding = false;
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

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();
        Render(destination);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _meshObjectsNeedRebuilding = true;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            var resWidth = 1920;
            var resHeight = 1080;

            RenderTexture rt = new RenderTexture(resWidth, resHeight, 24);
            GetComponent<Camera>().targetTexture = rt;
            Texture2D screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
            GetComponent<Camera>().Render();
            RenderTexture.active = rt;
            screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
            GetComponent<Camera>().targetTexture = null;
            RenderTexture.active = null; // JC: added to avoid errors
            Destroy(rt);
            byte[] bytes = screenShot.EncodeToPNG();
            string filename = "screenshot" + Time.time.ToString("F2") + ".png";
            System.IO.File.WriteAllBytes(filename, bytes);
            Debug.Log(string.Format("Took screenshot to: {0}", filename));
        }

        // check if each _meshObject has changed
        for (int i = 0; i < _meshObjects.Count; i++)
        {
            var mesh = _meshObjects[i];
            mesh.albedo = new Vector3(albedo.r, albedo.g, albedo.b);
            mesh.smoothness = smoothness;
            mesh.specular = specular;
            mesh.emission = new Vector3(emission.r, emission.g, emission.b);

            if (_oldMeshObjects[i].albedo != mesh.albedo ||
                _oldMeshObjects[i].smoothness != mesh.smoothness ||
                _oldMeshObjects[i].specular != mesh.specular ||
                _oldMeshObjects[i].emission != mesh.emission)
            {
                _meshObjectsNeedRebuilding = true;
                break;
            }
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

        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);

        SetComputeBuffer("_Indices", _indexBuffer);

        RayTracingShader.SetMatrix("_CameraToWorld", Camera.main.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", Camera.main.projectionMatrix.inverse);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        RayTracingShader.SetFloat("_Seed", Random.value);
        RayTracingShader.SetFloat("_SkyBrightness", SkyBrightness);

        RayTracingShader.SetInt("_Bounces", bounces);

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
        if (_vertexBuffer != null)
            _vertexBuffer.Release();
        if (_indexBuffer != null)
            _indexBuffer.Release();
        if (_meshObjectBuffer != null)
            _meshObjectBuffer.Release();
    }
    private void SetUpScene()
    {

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

        List<Sphere> spheres = new List<Sphere>();

        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            if (obj.isLight == false) {
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
                    indices_count = indices.Length,
                    albedo = new Vector3(albedo.r, albedo.g, albedo.b),
                    specular = specular,
                    emission = new Vector3(emission.r, emission.g, emission.b),
                    smoothness = smoothness,
                });
            } else {
                RayTracingObject light = obj;
                Sphere sphere = new Sphere();
                // Radius and radius
                Vector2 randomPos = Random.insideUnitCircle * 50;
                sphere.radius = light.lightSize + 1 * (light.lightSize - light.lightSize);
                sphere.position = new Vector3(light.transform.position.x, light.transform.position.y, light.transform.position.z);
        
                sphere.albedo = new Vector3(obj.lightColor.r, obj.lightColor.g, obj.lightColor.b) * obj.intensity;
                sphere.specular = Vector3.zero;
                sphere.emission = new Vector3(obj.lightColor.r, obj.lightColor.g, obj.lightColor.b) * obj.intensity;
                
                sphere.smoothness = 1;
                
                // Add the sphere to the list
                spheres.Add(sphere);
            }
        }

        _oldMeshObjects = _meshObjects;

        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 104);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
        CreateComputeBuffer(ref _sphereBuffer, spheres, 56);
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
        } else {
            Debug.LogError("Buf with name "+name+" is null");
        }
    }
}