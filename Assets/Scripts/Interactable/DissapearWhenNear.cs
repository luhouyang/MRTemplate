using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DissapearWhenNear : MonoBehaviour
{
    // The distance at which the object begins to fade out.
    private float fadeDistance = 3.0f;

    // The target transparency (alpha) of the object when the camera is inside the fade distance.
    [Range(0, 1)]
    private float targetTransparency = 0.0f;

    // The speed at which the object fades in and out.
    private float fadeSpeed = 7.0f;

    private Renderer objectRenderer;
    private Material objectMaterial;

    // The starting alpha value of the material.
    private float initialAlpha;

    void Start()
    {
        // Get the Renderer component of this GameObject.
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError("Renderer component not found on this GameObject. Please add a Mesh Renderer or other Renderer component.");
            this.enabled = false;
            return;
        }

        // Get a reference to the material to change its transparency.
        // We use .material to create a new instance, so we don't affect other objects using the same shared material.
        objectMaterial = objectRenderer.material;
        initialAlpha = objectMaterial.color.a;

        // NOTE: With MRTK and HoloLens, we use CameraCache to get the main camera.
        // The CameraCache ensures we have a reference to the active, ready camera.
        if (CameraCache.Main == null)
        {
            Debug.LogError("CameraCache.Main is null. Make sure MRTK is properly configured and a camera is available.");
            this.enabled = false;
        }
    }

    void Update()
    {
        // Check if both the camera and the material are valid before proceeding.
        if (CameraCache.Main != null && objectMaterial != null)
        {
            // Use CameraCache.Main to get the head's position.
            Vector3 headPosition = CameraCache.Main.transform.position;
            Vector3 headForward = CameraCache.Main.transform.forward;

            // Log the head position and forward vector for debugging purposes
            //Debug.Log($"Head Position: {headPosition}, Head Forward: {headForward}");

            // Calculate the distance between the camera's position and this GameObject.
            float distance = Vector3.Distance(transform.position, headPosition);

            // Determine the target alpha based on the distance.
            float targetAlpha = (distance < fadeDistance) ? targetTransparency : initialAlpha;

            // Get the current color of the material.
            Color currentColor = objectMaterial.color;

            // Smoothly move the alpha value towards the target alpha using Lerp.
            float newAlpha = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * fadeSpeed);

            // Create a new color with the new alpha value and apply it to the material.
            objectMaterial.color = new Color(currentColor.r, currentColor.g, currentColor.b, newAlpha);

            // NOTE: For this to work, you MUST set the Rendering Mode of your material to 'Transparent' or 'Fade'
            // in the Unity Inspector. You can find this setting under the material's properties.
        }
    }

    // This function can be used to set the material's rendering mode to Transparent at runtime.
    // It's helpful if you want to switch it from Opaque.
    private void SetMaterialRenderingModeToTransparent(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }
}
