using Microsoft.MixedReality.Toolkit.Utilities;
using UnityEngine;

namespace Microsoft.MixedReality.Toolkit.MRTemplate
{
    /// <summary>
    /// Game object will always face the user (main camera) and stay at a fixed distance.
    /// 
    /// ゲームオブジェクトは、常にユーザー (メインカメラ) の方向を向き、固定された距離を保ちます。
    /// </summary>
    [AddComponentMenu("Scripts/Utils/FaceUser")]
    public class FaceUser : MonoBehaviour
    {
        #region Serialized variables
        [Tooltip("Speed of rotation (degrees per second). Set to a high value for instant rotation.")]
        private float rotationSpeed = 5f;

        [Tooltip("Threshold in degrees for considering rotation complete (optional, for snapping).")]
        private float rotationThresholdDegrees = 1.0f;

        [Tooltip("Distance to maintain from the main camera (user) in meters.")]
        private float followDistance = 0.9f;

        [Tooltip("Speed at which the object moves to maintain the follow distance.")]
        private float moveSpeed = 5f;
        #endregion

        private Vector3 rotationDisplacement = new Vector3(0f, 0.005f, 0f);

        // Horizontal & Vertical displacement of the Game Object, high value will cause the Game Object to go out of view
        // ゲームオブジェクトの水平方向および垂直方向の変位量です。値が大きい場合、ゲームオブジェクトがビューから外れる可能性があります。
        private float horizontalDisplacement = 0.0f; 
        private float verticalDisplacement = -0.05f;

        private GameObject targetToRotate = null;
        private GameObject objectWithCollider = null;
        private Quaternion targetRotation;
        private Vector3 targetPosition;

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

            // Rotation Logic
            // Calculate the direction from the target object to the main camera
            // Using (currentPosition - cameraPosition) to make the object's forward point *towards* the camera
            //
            // 回転ロジック
            // ターゲットオブジェクトからメインカメラへの方向を計算します。
            // (currentPosition - cameraPosition) を使用することで、オブジェクトのフォワード方向がカメラを「向く」ようにします。
            Vector3 directionToCamera = -(cameraPosition - currentPosition + rotationDisplacement).normalized;
            targetRotation = Quaternion.LookRotation(directionToCamera);

            float rotationStep = rotationSpeed * Time.deltaTime;
            targetToRotate.transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationStep);

            // Snap rotation if close enough
            if (Quaternion.Angle(currentRotation, targetRotation) < rotationThresholdDegrees)
            {
                targetToRotate.transform.rotation = targetRotation;
            }

            // Position Logic
            // Calculate the desired position in front of the camera, then displaced to the right
            // This places the object at the camera's position, then moves it 'followDistance' along the camera's forward vector
            // Then, it displaces the object along the camera's right vector by 'horizontalDisplacement'
            //
            // 位置ロジック
            // カメラの正面に、その後右側にずらした目的の位置を計算します。
            // これにより、オブジェクトがカメラの位置に配置され、その後カメラのフォワードベクトルに沿って 'followDistance' だけ移動します。
            // 次に、カメラのライトベクトルに沿って 'horizontalDisplacement' だけオブジェクトを変位させます。
            targetPosition = cameraPosition + mainCameraTransform.forward * followDistance + mainCameraTransform.right * horizontalDisplacement + mainCameraTransform.up * verticalDisplacement;

            // Smoothly move towards the target position
            float moveStep = moveSpeed * Time.deltaTime;
            targetToRotate.transform.position = Vector3.Lerp(currentPosition, targetPosition, moveStep);
        }
    }
}