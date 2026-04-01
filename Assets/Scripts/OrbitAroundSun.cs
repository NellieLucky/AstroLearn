using UnityEngine;

public class OrbitAroundSun : MonoBehaviour
{
    public Transform sun;
    public float orbitSpeed = 10f;

    void Update()
    {
        if (sun != null)
        {
            transform.RotateAround(sun.position, Vector3.up, orbitSpeed * Time.deltaTime);
        }
    }
}
