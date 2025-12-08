using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using TMPro;
using static UnityEngine.Random;

public class ERCGazeRecorder : MonoBehaviour
{
    [System.Serializable]
    public class GazeData
    {
        public double timestamp;
        public long timestampTicks;
        public Vector3 headPosition;
        public Vector3 headForward;
        public Vector3 eyeOrigin;
        public Vector3 eyeDirection;
        public Vector3 hitPosition;
        public string targetName;
        public Vector3 localHitPosition;
    }

    private ExtendedEyeGazeDataProvider extendedEyeGazeDataProvider;
    public bool highSampleRate = true;

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
    private int continuousDivisions = 300;

    [SerializeField]
    private float continuousTime = 0.0f;

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

    [SerializeField]
    private double targetDuration = 1.5;

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
    private DateTime hardwareStartTime;
    private Renderer targetRenderer;
    private Bounds localBounds;
    private StringBuilder pc_sb = new StringBuilder();

    private DateTime lastRecordedTimestamp;
    private List<ExtendedEyeGazeDataProvider.GazeReading> readingBuffer = new List<ExtendedEyeGazeDataProvider.GazeReading>();

    void Start()
    {
        if (extendedEyeGazeDataProvider == null)
        {
            extendedEyeGazeDataProvider = FindObjectOfType<ExtendedEyeGazeDataProvider>();
            if (extendedEyeGazeDataProvider == null)
            {
                Debug.LogError("ERCGazeRecorder: Could not find ExtendedEyeGazeDataProvider in the scene!");
            }
        }

        lastRecordedTimestamp = DateTime.Now;

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

        if (highSampleRate)
        {
            // 90Hz = ~0.011111s per sample.
            // Estimate how many samples accumulated since last frame based on Time.unscaledDeltaTime
            // Add +2 for safety margin of ~22ms
            float expectedSamples = Time.unscaledDeltaTime * 90.0f;
            int dynamicLimit = Mathf.CeilToInt(expectedSamples) + 20;

            int sampleCount = extendedEyeGazeDataProvider.GetWorldSpaceGazeReadingsSince(lastRecordedTimestamp, ExtendedEyeGazeDataProvider.GazeType.Combined, readingBuffer, dynamicLimit);

            if (sampleCount > 0)
            {
                foreach (var reading in readingBuffer)
                {
                    RecordHighSampleGazeData(reading);
                    lastRecordedTimestamp = reading.Timestamp;
                }
            }
            else if (Application.isEditor)
            {
                var singleReading = extendedEyeGazeDataProvider.GetWorldSpaceGazeReading(ExtendedEyeGazeDataProvider.GazeType.Combined, DateTime.Now);
                RecordHighSampleGazeData(singleReading);
            }
            //Debug.Log("HIGH SAMPLING RATE");
        }
        else
        {
            var eyeTarget = EyeTrackingTarget.LookedAtEyeTarget;
            var gazedObject = eyeTarget != null ? eyeTarget.gameObject : null;

            RecordGazeData(gazedObject);
            //Debug.Log("NORMAL SAMPLING RATE");
        }

        if (timeInterval < 0)
        {
            if (multipleTarget)
            {
                if (numTargetAppeared == numTarget)
                {
                    ResetAll();
                    SaveAllData();

                    ERCGazeController.ToggleRecorded();
                }
                else
                {
                    timeInterval = targetDuration;
                    currentTarget.SetActive(false);

                    if (is3DObject)
                    {
                        if (numTargetAppeared % numTargetPerFace == 0)
                        {
                            currentFaceIndex = (currentFaceIndex + 1) % targetList3D.Count;
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

                    ERCGazeController.ToggleRecorded();
                }

                timeInterval = targetDuration;
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

        if (val && currentModel != null)
        {
            numTargetAppeared = 1;
            timeInterval = targetDuration;

            saveDir = Path.Combine(Application.persistentDataPath, sessionPath, currentModel.name);

            targetRenderer = currentModel.GetComponent<Renderer>();
            localBounds = targetRenderer.localBounds;
            pc_sb = new StringBuilder();

            pc_sb.AppendLine("localX,localY,localZ,globalX,globalY,globalZ,localtargetX,localtargetY,localtargetZ,globaltargetX,globaltargetY,globaltargetZ," +
                "headX,headY,headZ,headForwardX,headForwardY,headForwardZ,eyeOriginX,eyeOriginY,eyeOriginZ," +
                "eyeDirectionX,eyeDirectionY,eyeDirectionZ,timestamp,timestampTicks,targetName");

            //Debug.Log($"Recording Started. Target Model: {currentModel.name}. Save Path: {saveDir}. Bounds: {localBounds}");

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

        startingTime = Time.unscaledTimeAsDouble;

        // Reset to MinValue
        // We will set this to the EXACT timestamp of the first data packet we receive.
        // This ensures t=0.0 is always the first data point, ignoring timezones.
        hardwareStartTime = DateTime.MinValue;
        lastRecordedTimestamp = DateTime.Now;
    }

    private void RecordGazeData(GameObject target)
    {
        var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
        if (eyeProvider == null) return;

        var gaze = new GazeData
        {
            timestamp = Time.unscaledTimeAsDouble - startingTime,
            headPosition = CameraCache.Main.transform.position,
            headForward = CameraCache.Main.transform.forward,
            eyeOrigin = eyeProvider.GazeOrigin,
            eyeDirection = eyeProvider.GazeDirection,
            hitPosition = eyeProvider.IsEyeTrackingEnabledAndValid ? eyeProvider.HitPosition : Vector3.zero,
            targetName = target != null ? target.name : "null"
        };

        if (target != null && target.name == currentModel.name)
        {
            Vector3 tarTrans = (multipleTarget || isContinuous) ? currentTarget.transform.position : Vector3.zero;
            Vector3 tarTransLocal = target.transform.InverseTransformPoint(tarTrans);
            gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
            Vector3 pos = gaze.localHitPosition;
            //if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
            //{
            //    pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6}," +
            //        $"{gaze.hitPosition.x:F6},{gaze.hitPosition.y:F6},{gaze.hitPosition.z:F6}," +
            //        $"{tarTransLocal.x:F6},{tarTransLocal.y:F6},{tarTransLocal.z:F6}," +
            //        $"{tarTrans.x:F6},{tarTrans.y:F6},{tarTrans.z:F6}," +
            //        $"{gaze.headPosition.x:F6},{gaze.headPosition.y:F6},{gaze.headPosition.z:F6}," +
            //        $"{gaze.headForward.x:F6},{gaze.headForward.y:F6},{gaze.headForward.z:F6}," +
            //        $"{gaze.eyeOrigin.x:F6},{gaze.eyeOrigin.y:F6},{gaze.eyeOrigin.z:F6}," +
            //        $"{gaze.eyeDirection.x:F6},{gaze.eyeDirection.y:F6},{gaze.eyeDirection.z:F6}," +
            //        $"{(gaze.timestamp - startingTime):F6},{(currentTarget != null ? currentTarget.name : "null")}");
            //    zSum += pos.z;
            //    zNum += 1.0f;
            //}
            pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6}," +
                    $"{gaze.hitPosition.x:F6},{gaze.hitPosition.y:F6},{gaze.hitPosition.z:F6}," +
                    $"{tarTransLocal.x:F6},{tarTransLocal.y:F6},{tarTransLocal.z:F6}," +
                    $"{tarTrans.x:F6},{tarTrans.y:F6},{tarTrans.z:F6}," +
                    $"{gaze.headPosition.x:F6},{gaze.headPosition.y:F6},{gaze.headPosition.z:F6}," +
                    $"{gaze.headForward.x:F6},{gaze.headForward.y:F6},{gaze.headForward.z:F6}," +
                    $"{gaze.eyeOrigin.x:F6},{gaze.eyeOrigin.y:F6},{gaze.eyeOrigin.z:F6}," +
                    $"{gaze.eyeDirection.x:F6},{gaze.eyeDirection.y:F6},{gaze.eyeDirection.z:F6}," +
                    $"{(gaze.timestamp - startingTime):F6},{(currentTarget != null ? currentTarget.name : "null")}");
            zSum += pos.z;
            zNum += 1.0f;
        }
        else
        {
            gaze.localHitPosition = Vector3.zero;
        }
    }

    private void RecordHighSampleGazeData(ExtendedEyeGazeDataProvider.GazeReading reading)
    {
        var gaze = new GazeData
        {
            headPosition = reading.HeadPosition,
            headForward = reading.HeadForward,
            eyeOrigin = reading.EyePosition,
            eyeDirection = reading.GazeDirection,
            hitPosition = reading.IsValid ? reading.HitPosition : Vector3.zero,
            targetName = "null"
        };

        // Sync to First Packet
        if (reading.Timestamp != DateTime.MinValue)
        {
            // If this is the first packet of the session, lock the start time
            if (hardwareStartTime == DateTime.MinValue)
            {
                hardwareStartTime = reading.Timestamp;
            }

            // Calculate relative time from the locked start time
            gaze.timestamp = (reading.Timestamp - hardwareStartTime).TotalSeconds;
            gaze.timestampTicks = reading.Timestamp.Ticks;
        }
        else
        {
            gaze.timestamp = Time.unscaledTimeAsDouble - startingTime;
            gaze.timestampTicks = DateTime.Now.Ticks;
        }

        GameObject target = currentModel;

        if (target != null)
        {
            if (gaze.hitPosition != Vector3.zero)
            {
                Vector3 tarTrans = (multipleTarget || isContinuous) ? currentTarget.transform.position : Vector3.zero;
                Vector3 tarTransLocal = target.transform.InverseTransformPoint(tarTrans);

                Vector3 rawLocalPos = target.transform.InverseTransformPoint(gaze.hitPosition);
                gaze.localHitPosition = UnapplyUnityTransforms(rawLocalPos, target.transform.eulerAngles);
                Vector3 pos = gaze.localHitPosition;

                //bool boundsCheck = localBounds.Contains(rawLocalPos);

                //if (boundsCheck) gaze.targetName = target.name;

                //if (boundsCheck)
                //{
                //    // We now use raw World Space values from the provider directly.
                //    // No InverseTransformPoint (which converts to Camera Local Space).
                    
                //}

                Vector3 eyeOriginGlobal = gaze.eyeOrigin;
                Vector3 eyeDirGlobal = gaze.eyeDirection;
                Vector3 hitPosGlobal = gaze.hitPosition;

                pc_sb.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6}," +
                    $"{hitPosGlobal.x:F6},{hitPosGlobal.y:F6},{hitPosGlobal.z:F6}," + // Global Hit
                    $"{tarTransLocal.x:F6},{tarTransLocal.y:F6},{tarTransLocal.z:F6}," +
                    $"{tarTrans.x:F6},{tarTrans.y:F6},{tarTrans.z:F6}," +
                    $"{gaze.headPosition.x:F6},{gaze.headPosition.y:F6},{gaze.headPosition.z:F6}," +
                    $"{gaze.headForward.x:F6},{gaze.headForward.y:F6},{gaze.headForward.z:F6}," +
                    $"{eyeOriginGlobal.x:F6},{eyeOriginGlobal.y:F6},{eyeOriginGlobal.z:F6}," + // Global Eye
                    $"{eyeDirGlobal.x:F6},{eyeDirGlobal.y:F6},{eyeDirGlobal.z:F6}," + // Global Dir
                    $"{(gaze.timestamp):F9},{gaze.timestampTicks},{(currentTarget != null ? currentTarget.name : "null")}");
                zSum += pos.z;
                zNum += 1.0f;
            }
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
        StopAllCoroutines();
        readingBuffer.Clear();
    }

    public void SaveAllData()
    {
        StopAllCoroutines();
        readingBuffer.Clear();
        ExportPointCloud(currentModel);
        //Export3DModel(currentModel);
        SaveTargetCoordinates("target.csv");

        //Debug.Log("SAVED DATA AT: " + saveDir);
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
        Quaternion xRotation = Quaternion.AngleAxis(anglesInDegrees.x, Vector3.right);
        Quaternion yRotation = Quaternion.AngleAxis(anglesInDegrees.y, Vector3.up);
        Quaternion zRotation = Quaternion.AngleAxis(anglesInDegrees.z, Vector3.forward);

        Vector3 rotatedVector = xRotation * originalVector;
        rotatedVector = yRotation * rotatedVector;
        rotatedVector = zRotation * rotatedVector;

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

        for (int i = 0; i < tempMesh.subMeshCount; i++)
        {
            int[] triangles = tempMesh.GetTriangles(i);
            for (int j = 0; j < triangles.Length; j += 3)
            {
                int temp = triangles[j];
                triangles[j] = triangles[j + 2];
                triangles[j + 2] = temp;

                sb.Append($"f {triangles[j] + 1}/{triangles[j] + 1}/{triangles[j] + 1} " +
                            $"{triangles[j + 1] + 1}/{triangles[j + 1] + 1}/{triangles[j + 1] + 1} " +
                            $"{triangles[j + 2] + 1}/{triangles[j + 2] + 1}/{triangles[j + 2] + 1}\n");
            }
        }

        return sb.ToString();
    }
}