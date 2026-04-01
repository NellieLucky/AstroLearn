using UnityEngine;

public class OrbitAroundPlanet : MonoBehaviour
{
    public Transform target;
    public float orbitSpeed = 50f;

    void Update()
    {
        transform.RotateAround(target.position, Vector3.up, orbitSpeed * Time.deltaTime);
    }
}
