// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using UnityEngine;
using System;
using Microsoft.MixedReality.OpenXR;
using Microsoft.MixedReality.EyeTracking;
// Include MRTK namespace for EyeTrackingTarget and CoreServices fallback
using Microsoft.MixedReality.Toolkit;
using Microsoft.MixedReality.Toolkit.Input;

[DisallowMultipleComponent]
public class ExtendedEyeGazeDataProvider : MonoBehaviour
{
    // --- NEW: Static Accessor on this script ---
    /// <summary>
    /// The global static reference to the EyeTrackingTarget currently being looked at.
    /// Access this from anywhere: ExtendedEyeGazeDataProvider.LookedAtEyeTarget
    /// </summary>
    public static EyeTrackingTarget LookedAtEyeTarget { get; private set; }
    // -------------------------------------------

    public enum GazeType
    {
        Left,
        Right,
        Combined
    }

    public struct GazeReading
    {
        public bool IsValid;
        public Vector3 EyePosition;
        public Vector3 GazeDirection;
        public bool IsLookingAtAttachedObject;
        public Vector3 HitPosition;

        public GazeReading(bool isValid, Vector3 position, Vector3 direction, bool isLookingAtObj = false, Vector3 hitPos = default(Vector3))
        {
            IsValid = isValid;
            EyePosition = position;
            GazeDirection = direction;
            IsLookingAtAttachedObject = isLookingAtObj;
            HitPosition = hitPos;
        }
    }

    private Camera _mainCamera;
    private EyeGazeTrackerWatcher _watcher;
    private EyeGazeTracker _eyeGazeTracker;
    private EyeGazeTrackerReading _eyeGazeTrackerReading;
    private System.Numerics.Vector3 _trackerSpaceGazeOrigin;
    private System.Numerics.Vector3 _trackerSpaceGazeDirection;
    private GazeReading _gazeReading;
    private GazeReading _invalidGazeReading = new GazeReading(false, Vector3.zero, Vector3.zero, false, Vector3.zero);
    private bool _gazePermissionEnabled;
    private bool _readingSucceeded;
    private SpatialGraphNode _eyeGazeTrackerNode;
    private Pose _eyeGazeTrackerPose;
    private Matrix4x4 _eyeGazeTrackerSpaceToPlayspace = new Matrix4x4();
    private Matrix4x4 _eyeGazeTrackerSpaceToWorld = new Matrix4x4();
    private Transform _mixedRealityPlayspace;

    // Cache the collider for the "Attached Object" check
    private Collider _attachedCollider;

    [SerializeField]
    [Tooltip("Layers to include when checking for EyeTrackingTargets")]
    private LayerMask gazeTargetLayers = Physics.DefaultRaycastLayers;

    private const float MaxGazeDistance = 50.0f;

    private async void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera != null && _mainCamera.transform.parent != null)
        {
            _mixedRealityPlayspace = _mainCamera.transform.parent;
        }

        _attachedCollider = GetComponent<Collider>();

        Debug.Log("Initializing ExtendedEyeTracker");
#if ENABLE_WINMD_SUPPORT
        _gazePermissionEnabled = await AskForEyePosePermission();
#else
        _gazePermissionEnabled = true;
#endif

        if (!_gazePermissionEnabled)
        {
            Debug.LogError("Gaze is disabled");
            return;
        }

        _watcher = new EyeGazeTrackerWatcher();
        _watcher.EyeGazeTrackerAdded += _watcher_EyeGazeTrackerAdded;
        _watcher.EyeGazeTrackerRemoved += _watcher_EyeGazeTrackerRemoved;
        await _watcher.StartAsync();
    }

    /// <summary>
    /// Update loop to continuously update ExtendedEyeGazeDataProvider.LookedAtEyeTarget
    /// </summary>
    private void Update()
    {
        // --- MODIFICATION: Allow running even if _eyeGazeTracker is null (using Fallback) ---
        // if (!_gazePermissionEnabled || _eyeGazeTracker == null) return;

        // 1. Get the current Gaze Reading (Handles Fallback Internally)
        GazeReading currentReading = GetWorldSpaceGazeReading(GazeType.Combined, DateTime.Now);

        if (currentReading.IsValid)
        {
            RaycastHit hit;
            EyeTrackingTarget newTarget = null;

            // 2. Raycast to find EyeTrackingTarget
            if (Physics.Raycast(currentReading.EyePosition, currentReading.GazeDirection, out hit, MaxGazeDistance, gazeTargetLayers))
            {
                newTarget = hit.collider.GetComponent<EyeTrackingTarget>();
            }

            // 3. Update the Static Reference and trigger events
            if (newTarget != LookedAtEyeTarget)
            {
                if (LookedAtEyeTarget != null) LookedAtEyeTarget.OnLookAway?.Invoke();
                if (newTarget != null) newTarget.OnLookAtStart?.Invoke();

                LookedAtEyeTarget = newTarget;
            }
        }
        else
        {
            // Gaze lost: Clear the target
            if (LookedAtEyeTarget != null)
            {
                LookedAtEyeTarget.OnLookAway?.Invoke();
                LookedAtEyeTarget = null;
            }
        }
    }

    public GazeReading GetCameraSpaceGazeReading(GazeType gazeType)
    {
        return GetCameraSpaceGazeReading(gazeType, DateTime.Now);
    }

    public GazeReading GetCameraSpaceGazeReading(GazeType gazeType, DateTime timestamp)
    {
        _gazeReading = GetWorldSpaceGazeReading(gazeType, timestamp);
        if (!_gazeReading.IsValid) return _invalidGazeReading;

        _gazeReading.EyePosition = _mainCamera.transform.InverseTransformPoint(_gazeReading.EyePosition);
        _gazeReading.GazeDirection = _mainCamera.transform.InverseTransformDirection(_gazeReading.GazeDirection).normalized;

        if (_gazeReading.IsLookingAtAttachedObject)
        {
            _gazeReading.HitPosition = _mainCamera.transform.InverseTransformPoint(_gazeReading.HitPosition);
        }

        _gazeReading.IsValid = true;
        return _gazeReading;
    }

    public GazeReading GetWorldSpaceGazeReading(GazeType gazeType)
    {
        return GetWorldSpaceGazeReading(gazeType, DateTime.Now);
    }

    public GazeReading GetWorldSpaceGazeReading(GazeType gazeType, DateTime timestamp)
    {
        // ---------------------------------------------------------
        // PRIORITY 1: Try Extended Eye Tracker (Hardware / Remoting)
        // ---------------------------------------------------------
        if (_gazePermissionEnabled && _eyeGazeTracker != null)
        {
            _eyeGazeTrackerReading = _eyeGazeTracker.TryGetReadingAtTimestamp(timestamp);
            if (_eyeGazeTrackerReading != null)
            {
                _readingSucceeded = false;
                switch (gazeType)
                {
                    case GazeType.Left:
                        _readingSucceeded = _eyeGazeTrackerReading.TryGetLeftEyeGazeInTrackerSpace(out _trackerSpaceGazeOrigin, out _trackerSpaceGazeDirection);
                        break;
                    case GazeType.Right:
                        _readingSucceeded = _eyeGazeTrackerReading.TryGetRightEyeGazeInTrackerSpace(out _trackerSpaceGazeOrigin, out _trackerSpaceGazeDirection);
                        break;
                    case GazeType.Combined:
                        _readingSucceeded = _eyeGazeTrackerReading.TryGetCombinedEyeGazeInTrackerSpace(out _trackerSpaceGazeOrigin, out _trackerSpaceGazeDirection);
                        break;
                }

                if (_readingSucceeded && _eyeGazeTrackerNode.TryLocate(_eyeGazeTrackerReading.SystemRelativeTime.Ticks, out _eyeGazeTrackerPose))
                {
                    _eyeGazeTrackerSpaceToPlayspace.SetTRS(_eyeGazeTrackerPose.position, _eyeGazeTrackerPose.rotation, Vector3.one);
                    _eyeGazeTrackerSpaceToWorld = (_mixedRealityPlayspace != null) ?
                            _mixedRealityPlayspace.localToWorldMatrix * _eyeGazeTrackerSpaceToPlayspace :
                            _eyeGazeTrackerSpaceToPlayspace;

                    _gazeReading.EyePosition = _eyeGazeTrackerSpaceToWorld.MultiplyPoint3x4(ToUnity(_trackerSpaceGazeOrigin));
                    _gazeReading.GazeDirection = _eyeGazeTrackerSpaceToWorld.MultiplyVector(ToUnity(_trackerSpaceGazeDirection));

                    _gazeReading.IsLookingAtAttachedObject = CheckIfGazeHitsAttachedCollider(_gazeReading.EyePosition, _gazeReading.GazeDirection, out Vector3 hitPos);
                    _gazeReading.HitPosition = hitPos;
                    _gazeReading.IsValid = true;

                    return _gazeReading;
                }
            }
        }

        // ---------------------------------------------------------
        // PRIORITY 2: Fallback to MRTK Standard Input (Editor Mouse / Standard Gaze)
        // ---------------------------------------------------------
        if (CoreServices.InputSystem?.EyeGazeProvider != null)
        {
            // Only use fallback if we requested Combined gaze (Simulated gaze is usually combined)
            if (gazeType == GazeType.Combined)
            {
                _gazeReading.EyePosition = CoreServices.InputSystem.EyeGazeProvider.GazeOrigin;
                _gazeReading.GazeDirection = CoreServices.InputSystem.EyeGazeProvider.GazeDirection;

                // MRTK editor simulation often marks data valid even if using mouse
                _gazeReading.IsValid = CoreServices.InputSystem.EyeGazeProvider.IsEyeTrackingDataValid || Application.isEditor;

                if (_gazeReading.IsValid)
                {
                    _gazeReading.IsLookingAtAttachedObject = CheckIfGazeHitsAttachedCollider(_gazeReading.EyePosition, _gazeReading.GazeDirection, out Vector3 hitPos);
                    _gazeReading.HitPosition = hitPos;
                    return _gazeReading;
                }
            }
        }

        return _invalidGazeReading;
    }

    private bool CheckIfGazeHitsAttachedCollider(Vector3 origin, Vector3 direction, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (_attachedCollider == null) return false;

        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, MaxGazeDistance))
        {
            if (hit.collider == _attachedCollider)
            {
                hitPoint = hit.point;
                return true;
            }
        }
        return false;
    }

    private void _watcher_EyeGazeTrackerRemoved(object sender, EyeGazeTracker e)
    {
        _eyeGazeTracker = null;
        Debug.Log("EyeGazeTracker removed");
    }

    private async void _watcher_EyeGazeTrackerAdded(object sender, EyeGazeTracker e)
    {
        Debug.Log("EyeGazeTracker added");
        try
        {
            await e.OpenAsync(true);
            _eyeGazeTracker = e;
            var supportedFrameRates = _eyeGazeTracker.SupportedTargetFrameRates;
            _eyeGazeTracker.SetTargetFrameRate(supportedFrameRates[supportedFrameRates.Count - 1]);
            _eyeGazeTrackerNode = SpatialGraphNode.FromDynamicNodeId(e.TrackerSpaceLocatorNodeId);
        }
        catch (Exception ex)
        {
            Debug.LogError("Unable to open EyeGazeTracker\r\n" + ex.ToString());
        }
    }

#if ENABLE_WINMD_SUPPORT
    private async System.Threading.Tasks.Task<bool> AskForEyePosePermission()
    {
        var accessStatus = await Windows.Perception.People.EyesPose.RequestAccessAsync();
        return accessStatus == Windows.UI.Input.GazeInputAccessStatus.Allowed;
    }
#endif

    private static UnityEngine.Vector3 ToUnity(System.Numerics.Vector3 v) => new UnityEngine.Vector3(v.X, v.Y, -v.Z);
}