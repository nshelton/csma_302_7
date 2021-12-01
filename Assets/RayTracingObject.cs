using UnityEngine;
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    public bool isLight = false;
    public Color lightColor = Color.white;
    public float intensity = 5;
    public float lightSize = 1;

    private void OnEnable()
    {
        RayTracingMaster.RegisterObject(this);
    }
    private void OnDisable()
    {
        RayTracingMaster.UnregisterObject(this);
    }
}