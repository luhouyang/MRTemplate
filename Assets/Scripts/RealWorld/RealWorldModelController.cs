using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using Microsoft.MixedReality.Toolkit;

// In early stages of development, many things will change
// TODO: Attach Live Heatmap to spatial mesh
// TODO: Learn more about spatial mesh for higher resolution and more persistent gaze mapping
namespace RealWorldModel
{
    public class RealWorldModelController : MonoBehaviour
    {
        [Tooltip("The maximum distance the gaze ray will extend.")]
        [SerializeField]
        private float maxGazeDistance = 20.0f;

        [Tooltip("LayerMask to filter what the gaze ray can hit. Include SpatialAwareness if you want to log hits on real-world meshes.")]
        [SerializeField]
        private LayerMask raycastLayerMask = Physics.DefaultRaycastLayers;

        [Tooltip("Interval (seconds) between logging gaze data to reduce file size.")]
        [SerializeField]
        private float loggingInterval = 0.1f; // Log ~10 times per second

        private IMixedRealityEyeGazeProvider eyeGazeProvider;
        private float lastLogTime;
        private string logFilePath;

        [System.Serializable]
        public class GazeRecord
        {
            public float time;
            public float gazeOriginX, gazeOriginY, gazeOriginZ;
            public float gazeDirectionX, gazeDirectionY, gazeDirectionZ;
            public bool hitSomething;
            public float hitPointX, hitPointY, hitPointZ;
            public float hitDistance;
            public string hitObjectName; // Will be "SpatialMesh" for environment hits, or the virtual object name
        }

        private List<GazeRecord> gazeDataBuffer = new List<GazeRecord>();

        // Start is called before the first frame update
        void Start()
        {
            if (CoreServices.InputSystem != null)
            {
                eyeGazeProvider = CoreServices.InputSystem.EyeGazeProvider;
                if (eyeGazeProvider == null)
                {
                    Debug.LogError("MRTK Eye Gaze Provider not found! Make sure Eye Tracking is enabled in MRTK profile.");
                }
            }
            else
            {
                Debug.LogError("MRTK Input System not found!");
            }

            // Define log file path. Application.persistentDataPath is suitable for device storage.
            string folderPath = Path.Combine(Application.persistentDataPath, "GazeData");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            logFilePath = Path.Combine(folderPath, $"GazeLog_{System.DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv");

            // Write CSV header
            File.AppendAllText(logFilePath, "Time,GazeOriginX,GazeOriginY,GazeOriginZ,GazeDirectionX,GazeDirectionY,GazeDirectionZ,HitSomething,HitPointX,HitPointY,HitPointZ,HitDistance,HitObjectName\n");

            lastLogTime = Time.time;
        }

        // Update is called once per frame
        void Update()
        {
            if (eyeGazeProvider == null || !eyeGazeProvider.IsEyeTrackingEnabled)
            {
                return;
            }

            if (Time.time - lastLogTime >= loggingInterval)
            {
                GazeRecord record = new GazeRecord();
                record.time = Time.time;
                record.gazeOriginX = eyeGazeProvider.GazeOrigin.x;
                record.gazeOriginY = eyeGazeProvider.GazeOrigin.y;
                record.gazeOriginZ = eyeGazeProvider.GazeOrigin.z;

                record.gazeDirectionX = eyeGazeProvider.GazeDirection.x;
                record.gazeDirectionY = eyeGazeProvider.GazeDirection.y;
                record.gazeDirectionZ = eyeGazeProvider.GazeDirection.z;

                RaycastHit hit;
                if (Physics.Raycast(eyeGazeProvider.GazeOrigin, eyeGazeProvider.GazeDirection, out hit, maxGazeDistance, raycastLayerMask))
                {
                    record.hitSomething = true;
                    record.hitPointX = hit.point.x;
                    record.hitPointY = hit.point.y;
                    record.hitPointZ = hit.point.z;
                    record.hitDistance = hit.distance;

                    // This is important for distinguishing spatial mesh from virtual objects
                    // Spatial mesh gameobjects in MRTK are usually named with GUIDs or are children of a "SpatialAwarenessMesh" parent.
                    // You might need to refine this based on your MRTK setup and how spatial mesh objects are named.
                    if (hit.collider.gameObject.name.Contains("SpatialMesh")) // Common naming convention for MRTK spatial meshes
                    {
                        record.hitObjectName = "SpatialMesh";
                    }
                    else if (hit.collider.gameObject.layer == LayerMask.NameToLayer("SpatialAwareness")) // Or by layer
                    {
                        record.hitObjectName = "SpatialMesh";
                    }
                    else
                    {
                        record.hitObjectName = hit.collider.gameObject.name;
                    }
                }
                else
                {
                    record.hitSomething = false;
                    record.hitPointX = 0; record.hitPointY = 0; record.hitPointZ = 0; // Default or NaN
                    record.hitDistance = maxGazeDistance; // Or infinity
                    record.hitObjectName = "None";
                }

                gazeDataBuffer.Add(record);
                lastLogTime = Time.time;

                // For small buffers, write directly; for large, consider a more robust writer or a thread
                if (gazeDataBuffer.Count >= 50) // Write in chunks to avoid frequent file access
                {
                    FlushBufferToCSV();
                }
            }
        }

        void OnApplicationQuit()
        {
            // Ensure any remaining data is written when the application quits
            FlushBufferToCSV();
            Debug.Log($"Gaze data saved to: {logFilePath}");
        }

        private void FlushBufferToCSV()
        {
            if (gazeDataBuffer.Count == 0) return;

            StringBuilder sb = new StringBuilder();
            foreach (var record in gazeDataBuffer)
            {
                sb.AppendLine($"{record.time},{record.gazeOriginX},{record.gazeOriginY},{record.gazeOriginZ}," +
                              $"{record.gazeDirectionX},{record.gazeDirectionY},{record.gazeDirectionZ}," +
                              $"{record.hitSomething},{record.hitPointX},{record.hitPointY},{record.hitPointZ}," +
                              $"{record.hitDistance},{record.hitObjectName}");
            }
            File.AppendAllText(logFilePath, sb.ToString());
            gazeDataBuffer.Clear();
        }
    }
}
