using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControlPlatform : MonoBehaviour
{
    private Collider collider;

    // Start is called before the first frame-update
    void Start()
    {
        // Get the Collider component attached to this GameObject.
        collider = gameObject.GetComponent<Collider>();

        if (collider == null)
        {
            Debug.LogError("ControlPlatform requires a Collider component on the same GameObject.");
        }
    }

    // Function to check if the camera's X and Z coordinates are within the collider's bounds.
    public bool IsCameraXZInBounds(Vector3 position)
    {
        if (collider == null)
        {
            return false;
        }

        Bounds bounds = collider.bounds;

        // Check X dimension: position.x must be greater than or equal to the minimum X and less than or equal to the maximum X.
        bool isInX = position.x >= bounds.min.x && position.x <= bounds.max.x;

        // Check Z dimension: position.z must be greater than or equal to the minimum Z and less than or equal to the maximum Z.
        bool isInZ = position.z >= bounds.min.z && position.z <= bounds.max.z;

        // The position is in the XZ bounds only if both checks are true.
        return isInX && isInZ;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
