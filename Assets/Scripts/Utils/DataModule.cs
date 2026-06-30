using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;
using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using static UnityEngine.GraphicsBuffer;

// TODO: Add continuous data saving, to reduce lag at the end of sessions
namespace Microsoft.MixedReality.Toolkit.MRTemplate
{
    public class DataModule
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

            public Vector3 localHeadPosition;
            public Vector3 localHeadForward;
            public Vector3 localEyeOrigin;
            public Vector3 localEyeDirection;
        }

        [System.Serializable]
        public class SessionData
        {
            public string objectName;
            public List<GazeData> gazeData = new List<GazeData>();
            public List<QuestionnaireAnswer> questionnaireAnswers = new List<QuestionnaireAnswer>();
        }

        [System.Serializable]
        public class QuestionnaireAnswer
        {
            public double timestamp;
            public string answer;
            public Vector3 estimatedGazePosition;
            public string targetName;
            public Vector3 globalGazePosition;
            public Vector3 headPosition;
            public Vector3 headForward;
            public Vector3 eyeOrigin;
            public Vector3 eyeDirection;
        }

        private string saveDir;
        private double startingTime;
        private SessionData currentSession = new SessionData();

        // Eye Gaze
        private GameObject modelGameObject;
        private Renderer targetRenderer;
        private Bounds localBounds;
        private StringBuilder pointcloudSB = new StringBuilder();

        // QNA
        private StringBuilder qnaSB = new StringBuilder();

        // Audio
        private List<string> savedFiles = new List<string>();

        // Heatmap / Model
        private MeshFilter meshFilter;

        // Eye tracker high samplinh rate
        private DateTime hardwareStartTime;

        public DataModule(string saveDir, double startingTime, GameObject modelGameObject, MeshFilter meshFilter, DateTime hardwareStartTime)
        {
            this.saveDir = saveDir;
            this.startingTime = startingTime;
            this.modelGameObject = modelGameObject;
            this.meshFilter = meshFilter;
            this.hardwareStartTime = hardwareStartTime;

            // Get Renderer & localbound
            targetRenderer = modelGameObject.GetComponent<Renderer>();
            localBounds = targetRenderer.localBounds;

            pointcloudSB.AppendLine("x,y,z,timestamp,globalX,globalY,globalZ," +
        "headX,headY,headZ,headForwardX,headForwardY,headForwardZ,eyeOriginX,eyeOriginY,eyeOriginZ," +
        "eyeDirectionX,eyeDirectionY,eyeDirectionZ," +
        "localHeadX,localHeadY,localHeadZ,localHeadForwardX,localHeadForwardY,localHeadForwardZ," +
        "localEyeOriginX,localEyeOriginY,localEyeOriginZ,localEyeDirX,localEyeDirY,localEyeDirZ");
        }

        public void ExportModelTransform(GameObject target)
        {
            if (target == null) return;

            StringBuilder transformSB = new StringBuilder();
            transformSB.AppendLine("TransformType,X,Y,Z");

            // World Space
            transformSB.AppendLine($"Position,{target.transform.position.x:F6},{target.transform.position.y:F6},{target.transform.position.z:F6}");
            transformSB.AppendLine($"EulerAngles,{target.transform.eulerAngles.x:F6},{target.transform.eulerAngles.y:F6},{target.transform.eulerAngles.z:F6}");
            transformSB.AppendLine($"LossyScale,{target.transform.lossyScale.x:F6},{target.transform.lossyScale.y:F6},{target.transform.lossyScale.z:F6}");

            // Local Space
            transformSB.AppendLine($"LocalPosition,{target.transform.localPosition.x:F6},{target.transform.localPosition.y:F6},{target.transform.localPosition.z:F6}");
            transformSB.AppendLine($"LocalEulerAngles,{target.transform.localEulerAngles.x:F6},{target.transform.localEulerAngles.y:F6},{target.transform.localEulerAngles.z:F6}");
            transformSB.AppendLine($"LocalScale,{target.transform.localScale.x:F6},{target.transform.localScale.y:F6},{target.transform.localScale.z:F6}");

            File.WriteAllText(Path.Combine(saveDir, "model_transform.csv"), transformSB.ToString());
        }

        // Record Eye Gaze Data
        public GazeData RecordGazeData(GameObject target)
        {
            // GET EYE GAZE PROVIDER
            var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
            if (eyeProvider == null) return new GazeData();

            // CREATE NEW GAZE DATA
            var gaze = new GazeData
            {
                timestamp = Time.unscaledTimeAsDouble - startingTime,
                headPosition = CameraCache.Main.transform.position,
                headForward = CameraCache.Main.transform.forward,
                eyeOrigin = eyeProvider.GazeOrigin,
                eyeDirection = eyeProvider.GazeDirection,
                hitPosition = eyeProvider.IsEyeTrackingEnabledAndValid ? eyeProvider.HitPosition : Vector3.zero,
                targetName = target != null ? target.name : "null",
            };

            // CHECK IF GAZE HIT ON SELECTED MODEL
            if (target != null && target.name == modelGameObject.name)
            {
                // CONVERT GAZE HIT FROM WORLD COORDINATE TO LOCAL COORDINATE
                gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);

                // --> CALCULATE LOCAL HEAD AND EYE COORDINATES
                Vector3 rawLocalHead = target.transform.InverseTransformPoint(gaze.headPosition);
                gaze.localHeadPosition = UnapplyUnityTransforms(rawLocalHead, target.transform.eulerAngles);

                Vector3 rawLocalHeadFwd = target.transform.InverseTransformDirection(gaze.headForward);
                gaze.localHeadForward = UnapplyUnityTransforms(rawLocalHeadFwd, target.transform.eulerAngles);

                Vector3 rawLocalEyeOrg = target.transform.InverseTransformPoint(gaze.eyeOrigin);
                gaze.localEyeOrigin = UnapplyUnityTransforms(rawLocalEyeOrg, target.transform.eulerAngles);

                Vector3 rawLocalEyeDir = target.transform.InverseTransformDirection(gaze.eyeDirection);
                gaze.localEyeDirection = UnapplyUnityTransforms(rawLocalEyeDir, target.transform.eulerAngles);

                // CHECK IF GAZE HIT IS WITHIN BOUNDS OF SELECTED MODEL
                if (localBounds.Contains(gaze.localHitPosition))
                {
                    // REVERT TRANSFORMS WHEN IMPORTING MODEL
                    gaze.localHitPosition = UnapplyUnityTransforms(gaze.localHitPosition, target.transform.eulerAngles);

                    // ADD GAZE DATA
                    pointcloudSB.AppendLine($"{gaze.localHitPosition.x:F6},{gaze.localHitPosition.y:F6},{gaze.localHitPosition.z:F6},{Time.unscaledTimeAsDouble - startingTime:F6}," +
        $"{gaze.hitPosition.x:F6},{gaze.hitPosition.y:F6},{gaze.hitPosition.z:F6}," +
        $"{gaze.headPosition.x:F6},{gaze.headPosition.y:F6},{gaze.headPosition.z:F6}," +
        $"{gaze.headForward.x:F6},{gaze.headForward.y:F6},{gaze.headForward.z:F6}," +
        $"{gaze.eyeOrigin.x:F6},{gaze.eyeOrigin.y:F6},{gaze.eyeOrigin.z:F6}," +
        $"{gaze.eyeDirection.x:F6},{gaze.eyeDirection.y:F6},{gaze.eyeDirection.z:F6}," +
        $"{gaze.localHeadPosition.x:F6},{gaze.localHeadPosition.y:F6},{gaze.localHeadPosition.z:F6}," +
        $"{gaze.localHeadForward.x:F6},{gaze.localHeadForward.y:F6},{gaze.localHeadForward.z:F6}," +
        $"{gaze.localEyeOrigin.x:F6},{gaze.localEyeOrigin.y:F6},{gaze.localEyeOrigin.z:F6}," +
        $"{gaze.localEyeDirection.x:F6},{gaze.localEyeDirection.y:F6},{gaze.localEyeDirection.z:F6}");
                }
            }
            else
            {
                gaze.localHitPosition = Vector3.zero;
            }

            return gaze;
        }

        public GazeData RecordHighSampleGazeData(ExtendedEyeGazeDataProvider.GazeReading reading, GameObject target)
        {
            var gaze = new GazeData
            {
                headPosition = reading.HeadPosition,
                headForward = reading.HeadForward,
                eyeOrigin = reading.EyePosition,
                eyeDirection = reading.GazeDirection,
                hitPosition = reading.IsValid ? reading.HitPosition : Vector3.zero,
                targetName = target != null ? target.name : "null",
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
            }
            else
            {
                gaze.timestamp = Time.unscaledTimeAsDouble - startingTime;
            }

            if (gaze.hitPosition != Vector3.zero)
            {
                Vector3 rawLocalPos = target.transform.InverseTransformPoint(gaze.hitPosition);
                gaze.localHitPosition = UnapplyUnityTransforms(rawLocalPos, target.transform.eulerAngles);
                Vector3 pos = gaze.localHitPosition;

                bool boundsCheck = localBounds.Contains(rawLocalPos);

                if (boundsCheck) gaze.targetName = target.name;

                if (boundsCheck)
                {
                    // --> CALCULATE LOCAL HEAD AND EYE COORDINATES (HIGH SAMPLING)
                    Vector3 rawLocalHead = target.transform.InverseTransformPoint(gaze.headPosition);
                    gaze.localHeadPosition = UnapplyUnityTransforms(rawLocalHead, target.transform.eulerAngles);

                    Vector3 rawLocalHeadFwd = target.transform.InverseTransformDirection(gaze.headForward);
                    gaze.localHeadForward = UnapplyUnityTransforms(rawLocalHeadFwd, target.transform.eulerAngles);

                    Vector3 rawLocalEyeOrg = target.transform.InverseTransformPoint(gaze.eyeOrigin);
                    gaze.localEyeOrigin = UnapplyUnityTransforms(rawLocalEyeOrg, target.transform.eulerAngles);

                    Vector3 rawLocalEyeDir = target.transform.InverseTransformDirection(gaze.eyeDirection);
                    gaze.localEyeDirection = UnapplyUnityTransforms(rawLocalEyeDir, target.transform.eulerAngles);

                    // We now use raw World Space values from the provider directly.
                    // No InverseTransformPoint (which converts to Camera Local Space).
                    Vector3 eyeOriginGlobal = gaze.eyeOrigin;
                    Vector3 eyeDirGlobal = gaze.eyeDirection;
                    Vector3 hitPosGlobal = gaze.hitPosition;

                    // --> UPDATE APPENDLINE TO INCLUDE LOCAL DATA
                    pointcloudSB.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{(gaze.timestamp):F9}," +
                        $"{hitPosGlobal.x:F6},{hitPosGlobal.y:F6},{hitPosGlobal.z:F6}," +
                        $"{gaze.headPosition.x:F6},{gaze.headPosition.y:F6},{gaze.headPosition.z:F6}," +
                        $"{gaze.headForward.x:F6},{gaze.headForward.y:F6},{gaze.headForward.z:F6}," +
                        $"{eyeOriginGlobal.x:F6},{eyeOriginGlobal.y:F6},{eyeOriginGlobal.z:F6}," +
                        $"{eyeDirGlobal.x:F6},{eyeDirGlobal.y:F6},{eyeDirGlobal.z:F6}," +
                        $"{gaze.localHeadPosition.x:F6},{gaze.localHeadPosition.y:F6},{gaze.localHeadPosition.z:F6}," +
                        $"{gaze.localHeadForward.x:F6},{gaze.localHeadForward.y:F6},{gaze.localHeadForward.z:F6}," +
                        $"{gaze.localEyeOrigin.x:F6},{gaze.localEyeOrigin.y:F6},{gaze.localEyeOrigin.z:F6}," +
                        $"{gaze.localEyeDirection.x:F6},{gaze.localEyeDirection.y:F6},{gaze.localEyeDirection.z:F6}");
                
                }
            }

            return gaze;
        }

        #region AUDIO
        private byte[] ConvertAudioClipToWAV(AudioClip clip)
        {
            if (clip == null || clip.samples == 0) return null;

            int channels = clip.channels;
            int sampleCount = clip.samples;
            int bitsPerSample = 16;
            int byteRate = clip.frequency * channels * (bitsPerSample / 8);
            int dataSize = sampleCount * channels * (bitsPerSample / 8);

            // Create WAV header
            byte[] header = new byte[44];
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("RIFF"), 0, header, 0, 4);
            BitConverter.GetBytes((int)(dataSize + 36)).CopyTo(header, 4);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("WAVE"), 0, header, 8, 4);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("fmt "), 0, header, 12, 4);
            BitConverter.GetBytes((int)16).CopyTo(header, 16);
            BitConverter.GetBytes((short)1).CopyTo(header, 20);
            BitConverter.GetBytes((short)channels).CopyTo(header, 22);
            BitConverter.GetBytes(clip.frequency).CopyTo(header, 24);
            BitConverter.GetBytes(byteRate).CopyTo(header, 28);
            BitConverter.GetBytes((short)(channels * (bitsPerSample / 8))).CopyTo(header, 32);
            BitConverter.GetBytes((short)bitsPerSample).CopyTo(header, 34);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes("data"), 0, header, 36, 4);
            BitConverter.GetBytes((int)dataSize).CopyTo(header, 40);

            // Extract samples and convert to short PCM
            float[] samples = new float[sampleCount * channels];
            clip.GetData(samples, 0);
            byte[] data = new byte[dataSize];

            for (int i = 0; i < samples.Length; i++)
            {
                short val = (short)Mathf.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
                Buffer.BlockCopy(BitConverter.GetBytes(val), 0, data, i * 2, 2);
            }

            byte[] wavBytes = new byte[header.Length + data.Length];
            Buffer.BlockCopy(header, 0, wavBytes, 0, header.Length);
            Buffer.BlockCopy(data, 0, wavBytes, header.Length, data.Length);
            return wavBytes;
        }

        public void SaveAudioData(AudioClip recordedAudio, int chunkIndex)
        {
            if (recordedAudio == null)
            {
                return;
            }

            byte[] wavData = ConvertAudioClipToWAV(recordedAudio);
            string audioFileName = $"session_audio_{chunkIndex}.wav";
            string fullPath = Path.Combine(saveDir, audioFileName);
            File.WriteAllBytes(fullPath, wavData);
            Debug.Log($"Audio chunk saved: {fullPath}");
            savedFiles.Add(audioFileName);
            chunkIndex++;
        }

        public void SaveFileList()
        {
            // IN CMD run: ffmpeg -f concat -safe 0 -i filelist.txt -c copy output.wav
            StringBuilder sb = new StringBuilder();
            foreach (string file in savedFiles)
            {
                sb.AppendLine("file '" + file + "'");
            }

            string listFilePath = Path.Combine(saveDir, "filelist.txt");
            File.WriteAllText(listFilePath, sb.ToString());
            Debug.Log("File list saved to: " + listFilePath);
        }
#endregion

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

        #region EXPORT DATA
        public void ExportPointCloud()
        {
            File.WriteAllText(Path.Combine(saveDir, "pointcloud.csv"), pointcloudSB.ToString());
        }

        public void Export3DModel(GameObject target)
        {
            Mesh mesh = meshFilter.sharedMesh;
            string objContent = MeshToString(mesh, target);
            File.WriteAllText(Path.Combine(saveDir, "model.obj"), objContent);
        }

        private string MeshToString(Mesh mesh, GameObject target)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("# Exported Gaze Object\n");

            Mesh tempMesh = MonoBehaviour.Instantiate(mesh);

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
        #endregion

        #region QNA
        public void OnQuestionnaireAnswered(string selectedAnswer, Vector3 localHitPosition, Vector3 globalHitPosition, Vector3 headPosition, Vector3 headForward, Vector3 eyeOrigin, Vector3 eyeDirection)
        {
            currentSession.questionnaireAnswers.Add(new QuestionnaireAnswer
            {
                timestamp = Time.unscaledTimeAsDouble - startingTime,
                answer = selectedAnswer,
                estimatedGazePosition = localHitPosition,
                globalGazePosition = globalHitPosition,
                headPosition = headPosition,
                headForward = headForward,
                eyeOrigin = eyeOrigin,
                eyeDirection = eyeDirection
            });
        }

        public void ExportQuestionnaireAnswers()
        {
            if (currentSession.questionnaireAnswers.Count == 0) return;

            StringBuilder qa_sb = new StringBuilder();
            qa_sb.AppendLine("estX,estY,estZ,answer,timestamp,estGlobalX,estGlobalY,estGlobalZ," +
                "headX,headY,headZ,headForwardX,headForwardY,headForwardZ,eyeOriginX,eyeOriginY,eyeOriginZ," +
                "eyeDirectionX,eyeDirectionY,eyeDirectionZ");

            foreach (var qa in currentSession.questionnaireAnswers)
            {
                qa_sb.AppendLine($"{qa.estimatedGazePosition.x:F6},{qa.estimatedGazePosition.y:F6},{qa.estimatedGazePosition.z:F6},{qa.answer},{qa.timestamp:F6}," +
                    $"{qa.globalGazePosition.x:F6},{qa.globalGazePosition.y:F6},{qa.globalGazePosition.z:F6}," +
                    $"{qa.headPosition.x:F6},{qa.headPosition.y:F6},{qa.headPosition.z:F6}," +
                    $"{qa.headForward.x:F6},{qa.headForward.y:F6},{qa.headForward.z:F6}," +
                    $"{qa.eyeOrigin.x:F6},{qa.eyeOrigin.y:F6},{qa.eyeOrigin.z:F6}," +
                    $"{qa.eyeDirection.x:F6},{qa.eyeDirection.y:F6},{qa.eyeDirection.z:F6}");
            }

            File.WriteAllText(Path.Combine(saveDir, "qa.csv"), qa_sb.ToString());
        }
        #endregion
    }
}
