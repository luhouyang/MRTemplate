using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.MRTemplate
{
    /// <summary>
    /// Game object will face user by rotating at the same position
    /// This script is modified from FaceUser.cs so some parts may be redundant
    /// 
    /// ゲームオブジェクトは、その位置を保ったまま回転し、ユーザーの方向を向きます。
    /// このスクリプトはFaceUser.csの改変版であり、一部に冗長なコードが含まれる場合があります。
    /// </summary>
    [AddComponentMenu("Scripts/Utils/PromptFaceUser")]
    public class PromptFaceUser : MonoBehaviour
    {
        #region Serialized variables
        [Tooltip("Speed of rotation (degrees per second). Set to a high value for instant rotation.")]
        private float rotationSpeed = 5f;

        [Tooltip("Threshold in degrees for considering rotation complete (optional, for snapping).")]
        private float rotationThresholdDegrees = 1.0f; // Smaller threshold for closer snapping
        #endregion

        private Vector3 rotationDisplacement = new Vector3(0f, 0.005f, 0f);

        private GameObject targetToRotate = null;
        private GameObject objectWithCollider = null; // Kept for consistency, not directly used in core logic
        private Quaternion targetRotation; // The rotation we are trying to achieve
        private Vector3 targetPosition; // The position we are trying to achieve

        private void Start()
        {
            if (targetToRotate == null)
            {
                targetToRotate = gameObject;
            }

            if (objectWithCollider == null)
            {
                Collider coll = GetComponent<Collider>();
                if (coll == null)
                {
                    coll = GetComponentInChildren<Collider>();
                }
                if (coll != null)
                {
                    objectWithCollider = coll.gameObject;
                }
                else
                {
                    Debug.LogWarning("FaceUser: No collider found on this GameObject or its children.");
                }
            }

            targetRotation = targetToRotate.transform.rotation;
            targetPosition = targetToRotate.transform.position; // Initialize target position
        }

        public void Update()
        {
            if (CameraCache.Main == null)
            {
                Debug.LogWarning("FaceUser: CameraCache.Main is null. Cannot move or rotate.");
                return;
            }

            Vector3 cameraPosition = CameraCache.Main.transform.position;
            Vector3 currentPosition = targetToRotate.transform.position;
            Quaternion currentRotation = targetToRotate.transform.rotation;
            Transform mainCameraTransform = CameraCache.Main.transform;

            // Calculate the angle between camera and Game Object
            Vector3 directionToCamera = -(cameraPosition - currentPosition + rotationDisplacement).normalized;
            targetRotation = Quaternion.LookRotation(directionToCamera);

            float rotationStep = rotationSpeed * Time.deltaTime;
            targetToRotate.transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationStep);

            // Snap rotation if close enough
            if (Quaternion.Angle(currentRotation, targetRotation) < rotationThresholdDegrees)
            {
                targetToRotate.transform.rotation = targetRotation;
            }
        }
    }
}