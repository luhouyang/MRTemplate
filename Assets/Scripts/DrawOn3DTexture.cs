using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.MRTemplate
{
    [AddComponentMenu("Scripts/DrawOn3DTexture")]
    public class DrawOn3DTexture : MonoBehaviour
    {
        [Header("Heatmap Settings")]
        public Texture2D HeatmapLookUpTable;

        [SerializeField]
        private float drawBrushSize = 1500.0f; // spread of the heatmap visualization

        [SerializeField]
        private float drawIntensity = 30.0f; // amplitude / min & max of the color lookup table

        [SerializeField]
        private float minThreshDeltaHeatMap = 0.001f; // Mostly for performance to reduce spreading heatmap for small values.

        [SerializeField]
        private bool useRaycastForUV = true; // Use mesh raycast hit info for more accurate UV mapping

        public bool UseLiveInputStream = true;
        public Material HeatmapOverlayMaterialTemplate; // Heatmap initial material

        // Keypad response markers
        private Transform markerContainer;
        private List<GameObject> spawnedMarkers = new List<GameObject>();

        // Eye gaze realted variables
        private Texture2D myDrawTex;
        private Renderer myRenderer;
        private EyeTrackingTarget eyeTarget = null;

        private EyeTrackingTarget EyeTarget
        {
            get
            {
                if (eyeTarget == null)
                {
                    eyeTarget = this.GetComponent<EyeTrackingTarget>();
                }
                return eyeTarget;
            }
        }

        private void Start()
        {
            if (EyeTarget != null)
            {
                EyeTarget.WhileLookingAtTarget.AddListener(OnLookAt);
            }

            // Initialize the draw texture
            InitializeDrawTexture();
        }

        #region KEYPAD MARKER RELATED
        public void SpawnMarkerAtPosition(int index, Vector3 worldPosition, Vector3 surfaceNormal, GameObject[] markerPrefabs)
        {
            if (markerPrefabs == null || markerPrefabs.Length == 0)
            {
                Debug.LogError("Marker Prefabs array is not assigned or is empty. Cannot spawn marker.");
                return;
            }

            if (index >= markerPrefabs.Length)
            {
                Debug.LogError($"Invalid marker key: {index}.");
                return;
            }

            // The normal vector points directly outwards from the surface.
            // Quaternion.LookRotation creates a rotation where the Z-axis of the marker points along the normal.
            Quaternion spawnRotation = Quaternion.LookRotation(surfaceNormal);

            GameObject prefabToSpawn = markerPrefabs[index];
            if (prefabToSpawn != null)
            {
                // Instantiate the chosen prefab using the provided position and calculated rotation.
                // If markerContainer is assigned, parent the new marker to it for a clean hierarchy.
                // Instantiate the marker and store its reference in a variable.
                GameObject newMarker = Instantiate(prefabToSpawn, worldPosition, spawnRotation, markerContainer);

                // Add the newly created marker to our list for tracking.
                spawnedMarkers.Add(newMarker);
                //Debug.Log($"Spawned marker '{prefabToSpawn.name}' at {worldPosition}.");
            }
            else
            {
                Debug.LogError($"Prefab at index {index} in markerPrefabs is null.");
            }
        }
        #endregion

        #region EYE GAZE HEATMAP
        private void InitializeDrawTexture()
        {
            if (myDrawTex == null && HeatmapOverlayMaterialTemplate != null)
            {
                int textureSize = 1024; // Can be exposed as a parameter
                myDrawTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

                Color clearColor = new Color(0, 0, 0, 0);
                for (int x = 0; x < myDrawTex.width; x++)
                {
                    for (int y = 0; y < myDrawTex.height; y++)
                    {
                        myDrawTex.SetPixel(x, y, clearColor);
                    }
                }
                myDrawTex.Apply();

                SetupOverlayMaterial();
            }
        }

        private void SetupOverlayMaterial()
        {
            if (MyRenderer == null || myDrawTex == null)
                return;

            Material overlayMaterial = Instantiate(HeatmapOverlayMaterialTemplate);
            overlayMaterial.mainTexture = myDrawTex;

            Material[] currentMats = MyRenderer.sharedMaterials;
            Material[] newMats = new Material[currentMats.Length + 1];

            for (int i = 0; i < currentMats.Length; i++)
            {
                newMats[i] = currentMats[i];
            }
            newMats[currentMats.Length] = overlayMaterial;

            MyRenderer.sharedMaterials = newMats;
        }

        public void OnLookAt()
        {
            if (UseLiveInputStream && (EyeTarget != null) && (EyeTarget.IsLookedAt))
            {
                DrawAtThisHitPos(EyeTrackingTarget.LookedAtPoint);
            }
        }

        public void ToggleLiveHeatmap(bool val)
        {
            if (val)
            {
                EyeTarget.WhileLookingAtTarget.AddListener(OnLookAt);
            }
            else
            {
                EyeTarget.WhileLookingAtTarget.RemoveListener(OnLookAt);
            }
        }

        public void DrawAtThisHitPos(Vector3 hitPosition)
        {
            if (useRaycastForUV)
            {
                Ray ray;
                var gazeProvider = CoreServices.InputSystem?.GazeProvider;
                if (gazeProvider != null)
                {
                    // Construct the ray using the gaze origin and direction.
                    ray = new Ray(gazeProvider.GazeOrigin, gazeProvider.GazeDirection);
                }
                else
                {
                    ray = new Ray(Camera.main.transform.position, hitPosition - Camera.main.transform.position);
                }

                RaycastHit hit;
                if (UnityEngine.Physics.Raycast(ray, out hit) && hit.collider.gameObject == gameObject)
                {
                    MeshCollider meshCollider = hit.collider as MeshCollider;
                    if (meshCollider != null && meshCollider.sharedMesh != null)
                    {
                        Vector2[] meshUVs = meshCollider.sharedMesh.uv;
                        if (meshUVs != null && meshUVs.Length > 0)
                        {
                        }

                        Vector2 hitUV = hit.textureCoord;
                        StartCoroutine(DrawAt(hitUV));
                    }
                    else
                    {
                        Vector2? hitPosUV = GetCursorPosInTexture(hitPosition);
                        if (hitPosUV != null)
                        {
                            StartCoroutine(DrawAt(hitPosUV.Value));
                        }
                    }
                }
            }
            else
            {
                Vector2? hitPosUV = GetCursorPosInTexture(hitPosition);
                if (hitPosUV != null)
                {
                    StartCoroutine(DrawAt(hitPosUV.Value));
                }
            }
        }

        public void ClearDrawing()
        {
            // Loop through all tracked markers and destroy them.
            foreach (GameObject marker in spawnedMarkers)
            {
                Destroy(marker);
            }
            // Clear the list to remove all the now-destroyed marker references.
            spawnedMarkers.Clear();

            if (myDrawTex != null)
            {
                Color clearColor = new Color(0, 0, 0, 0);
                for (int x = 0; x < myDrawTex.width; x++)
                {
                    for (int y = 0; y < myDrawTex.height; y++)
                    {
                        myDrawTex.SetPixel(x, y, clearColor);
                    }
                }
                myDrawTex.Apply();
                neverDrawnOn = true;
            }
        }

        bool neverDrawnOn = true;

        private IEnumerator DrawAt(Vector2 posUV)
        {
            if (MyDrawTexture != null)
            {
                if (neverDrawnOn)
                {
                    for (int ix = 0; ix < MyDrawTexture.width; ix++)
                    {
                        for (int iy = 0; iy < MyDrawTexture.height; iy++)
                        {
                            MyDrawTexture.SetPixel(ix, iy, new Color(0, 0, 0, 0));
                        }
                    }
                    neverDrawnOn = false;
                }

                yield return null;

                StartCoroutine(ComputeHeatmapAt(posUV, true, true));
                yield return null;

                StartCoroutine(ComputeHeatmapAt(posUV, true, false));
                yield return null;

                StartCoroutine(ComputeHeatmapAt(posUV, false, true));
                yield return null;

                StartCoroutine(ComputeHeatmapAt(posUV, false, false));
                yield return null;

                MyDrawTexture.Apply();
            }
        }

        private IEnumerator ComputeHeatmapAt(Vector2 currPosUV, bool positiveX, bool positiveY)
        {
            yield return null;

            Vector2 center = new Vector2(currPosUV.x * MyDrawTexture.width, currPosUV.y * MyDrawTexture.height);
            int sign_x = (positiveX) ? 1 : -1;
            int sign_y = (positiveY) ? 1 : -1;
            int start_x = (positiveX) ? 0 : 1;
            int start_y = (positiveY) ? 0 : 1;

            for (int dx = start_x; dx < MyDrawTexture.width; dx++)
            {
                float tx = currPosUV.x * MyDrawTexture.width + dx * sign_x;
                if ((tx < 0) || (tx >= MyDrawTexture.width))
                    break;

                for (int dy = start_y; dy < MyDrawTexture.height; dy++)
                {
                    float ty = currPosUV.y * MyDrawTexture.height + dy * sign_y;
                    if ((ty < 0) || (ty >= MyDrawTexture.height))
                        break;

                    Color? newColor = null;
                    if (ComputeHeatmapColorAt(new Vector2(tx, ty), center, out newColor))
                    {
                        if (newColor.HasValue)
                        {
                            MyDrawTexture.SetPixel((int)tx, (int)ty, newColor.Value);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private bool ComputeHeatmapColorAt(Vector2 currPnt, Vector2 origPivot, out Color? col)
        {
            col = null;
            float spread = drawBrushSize;
            float amplitude = drawIntensity;
            float distCenterToCurrPnt = Vector2.Distance(origPivot, currPnt) / spread;

            float B = 2f;
            float scaledInterest = 1 / (1 + Mathf.Pow(Mathf.Epsilon, -(B * distCenterToCurrPnt)));
            float delta = scaledInterest / amplitude;
            if (delta < minThreshDeltaHeatMap)
                return false;

            Color baseColor = MyDrawTexture.GetPixel((int)currPnt.x, (int)currPnt.y);
            float normalizedInterest = Mathf.Clamp(baseColor.a + delta, 0, 1);

            if (HeatmapLookUpTable != null)
            {
                col = HeatmapLookUpTable.GetPixel((int)(normalizedInterest * (HeatmapLookUpTable.width - 1)), 0);
                col = new Color(col.Value.r, col.Value.g, col.Value.b, normalizedInterest);
            }
            else
            {
                col = Color.blue;
                col = new Color(col.Value.r, col.Value.g, col.Value.b, normalizedInterest);
            }
            return true;
        }

        private Renderer MyRenderer
        {
            get
            {
                if (myRenderer == null)
                {
                    myRenderer = GetComponent<Renderer>();
                }
                return myRenderer;
            }
        }

        public Texture2D MyDrawTexture
        {
            get
            {
                if (myDrawTex == null)
                {
                    InitializeDrawTexture();
                }
                return myDrawTex;
            }
        }

        private Vector2? GetCursorPosInTexture(Vector3 hitPosition)
        {
            Vector2? hitPointUV = null;
            try
            {
                Vector3 center = gameObject.transform.position;
                Vector3 halfsize = gameObject.transform.localScale / 2;
                Vector3 transfHitPnt = hitPosition - center;
                transfHitPnt = Quaternion.AngleAxis(-(this.gameObject.transform.rotation.eulerAngles.y - 180), Vector3.up) * transfHitPnt;
                transfHitPnt = Quaternion.AngleAxis(this.gameObject.transform.rotation.eulerAngles.x, Vector3.right) * transfHitPnt;
                float uvx = (Mathf.Clamp(transfHitPnt.x, -halfsize.x, halfsize.x) + halfsize.x) / (2 * halfsize.x);
                float uvy = (Mathf.Clamp(transfHitPnt.y, -halfsize.y, halfsize.y) / (2 * halfsize.y));
                hitPointUV = new Vector2(uvx, uvy);
            }
            catch (UnityEngine.Assertions.AssertionException)
            {
                Debug.LogError(">> AssertionException");
            }
            return hitPointUV;
        }
        #endregion
    }
}