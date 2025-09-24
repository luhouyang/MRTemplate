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

namespace Microsoft.MixedReality.Toolkit.MRTemplate
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
        public Vector3 estimatedGamePosition;
        public string targetName;
    }

    public class DataModule : MonoBehaviour
    {
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

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        // Record Eye Gaze Data
        private void RecordGazeData(GameObject target)
        {
            /* GET EYE GAZE PROVIDER */
            var eyeProvider = CoreServices.InputSystem?.EyeGazeProvider;
            if (eyeProvider == null) return;

            /* CREATE NEW GAZE DATA */
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

            /* CHECK IF GAZE HIT ON SELECTED MODEL */
            if (target != null && target.name == modelGameObject.name)
            {
                /* CONVERT GAZE HIT FROM WORLD COORDINATE TO LOCAL COORDINATE */
                gaze.localHitPosition = target.transform.InverseTransformPoint(gaze.hitPosition);
                Vector3 pos = gaze.localHitPosition;

                /* CHECK IF GAZE HIT IS ON SELECTED MODEL */
                if (localBounds.Contains(pos) && gaze.targetName == target.name && gaze.targetName != "null")
                {
                    /* REVERT TRANSFORMS WHEN IMPORTING MODEL */
                    pos = UnapplyUnityTransforms(pos, target.transform.eulerAngles);

                    /* ADD GAZE DATA */
                    pointcloudSB.AppendLine($"{pos.x:F6},{pos.y:F6},{pos.z:F6},{gaze.timestamp:F6},{gaze.headPosition:F6},{gaze.headForward:F6},{gaze.eyeOrigin:F6},{gaze.eyeDirection:F6}");
                }
            }
            else
            {
                gaze.localHitPosition = Vector3.zero;
            }
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

        private void SaveAudioData(AudioClip recordedAudio, int chunkIndex)
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

        private void SaveFileList()
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
            /* REVERSE ANY ROTATION ON MODEL */
            Quaternion xRotation = Quaternion.AngleAxis(anglesInDegrees.x, Vector3.right);
            Quaternion yRotation = Quaternion.AngleAxis(anglesInDegrees.y, Vector3.up);
            Quaternion zRotation = Quaternion.AngleAxis(anglesInDegrees.z, Vector3.forward);

            Vector3 rotatedVector = xRotation * originalVector;
            rotatedVector = yRotation * rotatedVector;
            rotatedVector = zRotation * rotatedVector;

            /* NEGATE X TO FLIP THE X-AXIS */
            return new Vector3(-rotatedVector.x, rotatedVector.y, rotatedVector.z);
        }

        #region EXPORT DATA
        public void ExportPointCloud(GameObject target)
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
        #endregion

        #region QNA
        private void OnQuestionnaireAnswered(string selectedAnswer, Vector3 localHitPosition)
        {
            currentSession.questionnaireAnswers.Add(new QuestionnaireAnswer
            {
                timestamp = Time.unscaledTimeAsDouble - startingTime,
                answer = selectedAnswer,
                estimatedGamePosition = localHitPosition
            });
        }

        private void SaveQuestionnaireAnswers()
        {
            if (currentSession.questionnaireAnswers.Count == 0) return;

            StringBuilder qa_sb = new StringBuilder();
            qa_sb.AppendLine("estX,estY,estZ,answer,timestamp");

            foreach (var qa in currentSession.questionnaireAnswers)
            {
                qa_sb.AppendLine($"{qa.estimatedGamePosition.x:F6},{qa.estimatedGamePosition.y:F6},{qa.estimatedGamePosition.z:F6},{qa.answer},{qa.timestamp:F6}");
            }

            File.WriteAllText(Path.Combine(saveDir, "qa.csv"), qa_sb.ToString());
        }
        #endregion
    }
}
