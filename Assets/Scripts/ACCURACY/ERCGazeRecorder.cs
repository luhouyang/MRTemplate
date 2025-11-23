using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
// using Microsoft.MixedReality.Toolkit.MRTemplate; // Commented out if not needed, or keep if your project uses it
using Microsoft.MixedReality.Toolkit.Utilities;
using TMPro;
using static UnityEngine.Random;

public class ERCGazeRecorder : MonoBehaviour
{
    [System.Serializable]
    public class GazeData
    {
        public double timestamp;
        public Vector3 headPosition;
        public Vector3 headForward;
        public Vector3 eyeOrigin;
        public Vector3 eyeDirection;
        public Vector3 hitPosition;
        public string targetName;
        public Vector3 localHitPosition;
    }

    // --- FIX: Reference to the provider ---
    [Header("References")]
    [SerializeField]
    private ExtendedEyeGazeDataProvider extendedEyeGazeDataProvider;
    // --------------------------------------

    [Header("Random Saccade Task")]
    [SerializeField]
    private int numTarget = 60;

    [SerializeField]
    private bool multipleTarget = true;

    [Header("3D Settings")]
    [SerializeField]
    private bool is3DObject = true;

    [SerializeField]
    private int numTargetPerFace = 10;

    private int currentFaceIndex = 0;

    [Header("Continuous Eye Movement Task")]
    [SerializeField]
    private bool isContinuous = false;

    [SerializeField]
    private int continuousDivisions = 300; // For 4 segments, 15 seconds each, update rate of 50ms

    [SerializeField]
    private float continuousTime = 0.0f; // For 4 segments, 15 seconds each, update rate of 50ms

    [SerializeField]
    private List<GameObject> segmentEndPoints = new List<GameObject>();

    private Vector3 initialPosition;
    private Vector3 movementVector;
    private int currentSegment = 0;
    private int movementSteps = 0;
    private float stepTimer = 0;

    [Header("Heatmap / Mesh Settings")]
    public MeshFilter meshFilter;

    [SerializeField]
    private List<GameObject> targetList = new List<GameObject>();

    [SerializeField]
    private List<int> targetFace3D = new List<int>();

    private List<List<GameObject>> targetList3D = new List<List<GameObject>>();

    [SerializeField]
    private GameObject currentModel;

    public string sessionPath;
    private int numTargetAppeared;
    private double timeInterval;
    private float zSum;
    private float zNum;
    private int currentIndex;
    private GameObject currentTarget;

    public bool isRecording;

    private string saveDir;
    private double startingTime;
    private Renderer targetRenderer;
    private Bounds localBounds;
    private StringBuilder pc_sb = new StringBuilder();

    void Start()
    {
        // --- FIX: Initialize the provider if not assigned in Inspector ---
        if (extendedEyeGazeDataProvider == null)
        {
            extendedEyeGazeDataProvider = FindObjectOfType<ExtendedEyeGazeDataProvider>();
            if (extendedEyeGazeDataProvider == null)
            {
                Debug.LogError("ERCGazeRecorder: Could not find ExtendedEyeGazeDataProvider in the scene!");
            }
        }
        // ----------------------------------------------------------------

        // deactivate all points
        for (int i = 0; i < targetList.Count; i++)
        {
            targetList[i].SetActive(false);
        }

        if (is3DObject)
        {
            List<GameObject> targetsTemp = new List<GameObject>();
            for (int i = 0; i < targetList.Count; i++)
            {
                if (targetFace3D.Contains(i))
                {
                    targetList3D.Add(targetsTemp);
                    targetsTemp = new List<GameObject>();
                }
                else
                {
                    targetsTemp.Add(targetList[i]);
                }
            }
            targetList3D.Add(targetsTemp);
        }

        if (isContinuous)
        {
            initialPosition = segmentEndPoints[0].transform.position;
        }
    }

    void Update()
    {
        if (!isRecording || currentModel == null) return;

        var eyeTarget = ExtendedEyeGazeDataProvider.LookedAtEyeTarget;
        var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

        RecordGazeData(gazedObject);

        if (timeInterval < 0)
        {
            if (multipleTarget)
            {
                if (numTargetAppeared == numTarget)
                {
                    ResetAll();
                    SaveAllData();

                    // ERCGazeController.ToggleRecorded(); // Check if this class exists in your project
                }
                else
                {
                    timeInterval = Range(100, 151) / 100.0;
                    currentTarget.SetActive(false);

                    if (is3DObject)
                    {
                        if (numTargetAppeared % numTargetPerFace == 0)
                        {
                            currentFaceIndex = (currentFaceIndex + 1) % targetList3D.Count;
                            Debug.Log(currentFaceIndex);
                        }

                        int nextIndex = Range(0, targetList3D[currentFaceIndex].Count);
                        while (currentIndex == nextIndex)
                        {
                            nextIndex = Range(0, targetList3D[currentFaceIndex].Count);
                        }
                        currentIndex = nextIndex;
                        currentTarget = targetList3D[currentFaceIndex][currentIndex];
                    }
                    else
                    {
                        int nextIndex = Range(0, targetList.Count);
                        while (currentIndex == nextIndex)
                        {
                            nextIndex = Range(0, targetList.Count);
                        }
                        currentIndex = nextIndex;
                        currentTarget = targetList[currentIndex];
                    }

                    currentTarget.SetActive(true);
                    numTargetAppeared++;
                }
            }
            else if (isContinuous && currentSegment < segmentEndPoints.Count - 1)
            {
                if (stepTimer < 0)
                {
                    stepTimer = continuousTime / (segmentEndPoints.Count - 1) / continuousDivisions;
                    if (movementSteps != continuousDivisions)
                    {
                        movementSteps += 1;
                        currentTarget.transform.position += movementVector;
                    }
                    else
                    {
                        movementSteps = 0;
                        currentSegment += 1;
                        if (currentSegment < segmentEndPoints.Count - 1) movementVector = (segmentEndPoints[currentSegment + 1].transform.position - segmentEndPoints[currentSegment].transform.position) / continuousDivisions;
                    }
                }
                else
                {
                    stepTimer -= Time.deltaTime;
                }
            }
            else
            {
                if (numTargetAppeared == numTarget || isContinuous)
                {
                    ResetAll();
                    SaveAllData();

                    // ERCGazeController.ToggleRecorded();
                }

                timeInterval = Range(100, 151) / 100.0;
                numTargetAppeared++;
            }

        }
        else
        {
            timeInterval -= Time.deltaTime;
        }
    }

    public void SetIsRecording(bool val)
    {
        isRecording = val;
        startingTime = Time.unscaledTimeAsDouble;

        if (val && currentModel != null)
        {
            numTargetAppeared = 1;
            timeInterval = Range(100, 151) / 100.0;

            saveDir = Path.Combine(Application.persistentDataPath, sessionPath, currentModel.name);

            targetRenderer = currentModel.GetComponent<Renderer>();
            localBounds = targetRenderer.localBounds;
            pc_sb = new StringBuilder();
            pc_sb.AppendLine("localX,localY,localZ,globalX,globalY,globalZ,localtargetX,localtargetY,localtargetZ,globaltargetX,globaltargetY,globaltargetZ," +
                "headX,headY,headZ,headForwardX,headForwardY,headForwardZ,eyeOriginX,eyeOriginY,eyeOriginZ," +
                "eyeDirectionX,eyeDirectionY,eyeDirectionZ,timestamp,targetName");

            Debug.Log($"Recording Started. Target Model: {currentModel.name}. Save Path: {saveDir}. Bounds: {localBounds}");

            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            if (multipleTarget)
            {
                if (is3DObject)
                {
                    currentFaceIndex = 0;
                    currentIndex = Range(0, targetList3D[currentFaceIndex].Count);
                    currentTarget = targetList3D[currentFaceIndex][currentIndex];
                }
                else
                {
                    currentIndex = Range(0, targetList.Count);
                    currentTarget = targetList[currentIndex];
                }
                currentTarget.SetActive(true);
            }
            else if (isContinuous)
            {
                stepTimer = -0.1f;
                movementSteps = 0;
                currentSegment = 0;
                currentTarget = segmentEndPoints[0];
                initialPosition = segmentEndPoints[0].transform.position;
                movementVector = (segmentEndPoints[currentSegment + 1].transform.position - segmentEndPoints[currentSegment].transform.position) / continuousDivisions;
                currentTarget.SetActive(true);
            }
        }
    }


    private void RecordGazeData(GameObject target)
    {
        if (extendedEyeGazeDataProvider == null) return;

        DateTime timestamp = DateTime.Now;
        var eyeProvider = extendedEyeGazeDataProvider.GetCameraSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Combined, timestamp);

        var gaze = new GazeData
        {
            timestamp = Time.unscaledTimeAsDouble - startingTime,
            headPosition = CameraCache.Main.transform.position,
            headForward = CameraCache.Main.transform.forward,
            eyeOrigin = eyeProvider.EyePosition,
            eyeDirection = eyeProvider.GazeDirection,
            hitPosition = eyeProvider.IsValid ? eyeProvider.HitPosition : Vector3.zero,
            targetName = target != null ? target.name : "null"
        };

        if (target != null)
        {
            if (target.name == currentModel.name)
            {
                Vector3 tarTrans = (multipleTarget || isContinuous) ? currentTarget.transform.position : Vector3.zero;
                Vector3 tarTransLocal = target.transform.InverseTransformPoint(tarTrans);
                gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
                gaze.localHitPosition = UnapplyUnityTransforms(gaze.localHitPosition, target.transform.eulerAngles);
                Vector3 pos = gaze.localHitPosition;

                // --- DEBUGGING ---
                bool boundsCheck = localBounds.Contains(pos);
                bool nameCheck = gaze.targetName == target.name;
                bool nullCheck = gaze.targetName != "null";

                if (boundsCheck && nameCheck && nullCheck)
                {
                    pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6}," +
                        $"{gaze.hitPosition.x:F6},{gaze.hitPosition.y:F6},{gaze.hitPosition.z:F6}," +
                        $"{tarTransLocal.x:F6},{tarTransLocal.y:F6},{tarTransLocal.z:F6}," +
                        $"{tarTrans.x:F6},{tarTrans.y:F6},{tarTrans.z:F6}," +
                        $"{gaze.headPosition.x:F6},{gaze.headPosition.y:F6},{gaze.headPosition.z:F6}," +
                        $"{gaze.headForward.x:F6},{gaze.headForward.y:F6},{gaze.headForward.z:F6}," +
                        $"{gaze.eyeOrigin.x:F6},{gaze.eyeOrigin.y:F6},{gaze.eyeOrigin.z:F6}," +
                        $"{gaze.eyeDirection.x:F6},{gaze.eyeDirection.y:F6},{gaze.eyeDirection.z:F6}," +
                        $"{(gaze.timestamp):F6},{(currentTarget != null ? currentTarget.name : "null")}");
                    zSum += pos.z;
                    zNum += 1.0f;
                    // Debug.Log("SUCCESS: Data recorded for frame.");
                }
                else
                {
                    Debug.LogWarning($"REJECTED: Bounds: {boundsCheck} ({pos}), NameMatch: {nameCheck} ({gaze.targetName} vs {target.name}), Valid: {nullCheck}");
                }
            }
            else
            {
                Debug.Log($"MISMATCH: Gaze Object ({target.name}) != Current Model ({currentModel.name})");
            }
        }
        else
        {
            // Debug.Log("Target is null (No object looked at)");
            gaze.localHitPosition = Vector3.zero;
        }
    }

    public void ResetAll()
    {
        if (isRecording)
        {
            if (isContinuous)
            {
                currentTarget.transform.position = initialPosition;
                currentTarget.SetActive(false);
            }
            if (multipleTarget)
            {
                currentTarget.SetActive(false);
            }
            SetIsRecording(false);
        }
        StopAllCoroutines(); // Ensure any ongoing audio recording coroutines are stopped
    }

    public void SaveAllData()
    {
        ExportPointCloud(currentModel);
        Export3DModel(currentModel);
        SaveTargetCoordinates("target.csv");
        Debug.Log("SAVED DATA AT: " + saveDir);
    }

    public void ExportPointCloud(GameObject target)
    {
        File.WriteAllText(Path.Combine(saveDir, "pointcloud.csv"), pc_sb.ToString());
    }

    public void SaveTargetCoordinates(string fileName)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("localX,localY,localZ,rotationW,rotationX,rotationY,rotationZ,scaleX,scaleY,scaleZ,targetName");
        foreach (GameObject target in targetList)
        {
            Vector3 pos = target.transform.localPosition;
            Quaternion rot = target.transform.rotation;
            pos = new Vector3(pos.x, pos.y, pos.z + (zSum / zNum));
            sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{rot.w:F6},{rot.x:F6},{rot.y:F6},{rot.z:F6},{target.transform.localScale.x:F6},{target.transform.localScale.y:F6},{target.transform.localScale.z:F6},{target.name}");
        }
        File.WriteAllText(Path.Combine(saveDir, fileName), sb.ToString());
    }

    public void Export3DModel(GameObject target)
    {
        Mesh mesh = meshFilter.sharedMesh;
        string objContent = MeshToString(mesh, target);
        File.WriteAllText(Path.Combine(saveDir, "model.obj"), objContent);
    }

    private Vector3 UnapplyUnityTransforms(Vector3 originalVector, Vector3 anglesInDegrees)
    {
        // REVERSE ANY ROTATION ON MODEL
        Quaternion xRotation = Quaternion.AngleAxis(anglesInDegrees.x, Vector3.right);
        Quaternion yRotation = Quaternion.AngleAxis(anglesInDegrees.y, Vector3.up);
        Quaternion zRotation = Quaternion.AngleAxis(anglesInDegrees.z, Vector3.forward);

        Vector3 rotatedVector = xRotation * originalVector;
        rotatedVector = yRotation * rotatedVector;
        rotatedVector = zRotation * rotatedVector;

        // NEGATE X TO FLIP THE X-AXIS
        return new Vector3(-rotatedVector.x, rotatedVector.y, rotatedVector.z);
    }

    private string MeshToString(Mesh mesh, GameObject target)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("# Exported Gaze Object\n");

        Mesh tempMesh = Instantiate(mesh);

        Vector3[] transVertices = new Vector3[tempMesh.vertexCount];
        for (int i = 0; i < tempMesh.vertices.Length; i++)
        {
            transVertices[i] = UnapplyUnityTransforms(tempMesh.vertices[i], target.transform.eulerAngles);
        }
        tempMesh.vertices = transVertices;

        tempMesh.RecalculateNormals();

        foreach (Vector3 vertex in tempMesh.vertices)
        {
            sb.Append($"v {vertex.x:F6} {vertex.y:F6} {vertex.z:F6}\n");
        }

        foreach (Vector3 normal in tempMesh.normals)
        {
            sb.Append($"vn {normal.x:F6} {normal.y:F6} {normal.z:F6}\n");
        }

        foreach (Vector2 uv in tempMesh.uv)
        {
            sb.Append($"vt {uv.x:F6} {uv.y:F6}\n");
        }

        // Write out faces (with winding order flipped)
        for (int i = 0; i < tempMesh.subMeshCount; i++)
        {
            int[] triangles = tempMesh.GetTriangles(i);
            for (int j = 0; j < triangles.Length; j += 3)
            {
                // Swap first and third index to reverse triangle winding
                int temp = triangles[j];
                triangles[j] = triangles[j + 2];
                triangles[j + 2] = temp;

                // Output face
                sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
                            $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
                            $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
            }
        }

        return sb.ToString();
    }
}