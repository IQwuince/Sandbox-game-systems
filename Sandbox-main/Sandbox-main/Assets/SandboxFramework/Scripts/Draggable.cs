using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public enum RigidbodyStateChange
{
    Unchanged,
    SetKinematic,
    SetNonKinematic
}

[RequireComponent(typeof(Selectable))]
[DisallowMultipleComponent]
public class Draggable : MonoBehaviour
{
    [Header("Drag Settings")]
    public bool shouldPropagateDragEvents = true;
    public bool shouldIgnoreRigidbodySettingFromDragger = false;
    public bool removeFromParentAtAwake = false;

    [Header("Grid Snapping")]
    [Tooltip("If true the object will snap to a grid while dragging or on release depending on Snap On Release Only.")]
    public bool useGridSnapping = false;
    [Tooltip("World grid cell size. Use 0 for an axis to skip snapping on that axis.")]
    public Vector3 gridSize = Vector3.one;
    [Tooltip("If true, snapping is applied only when the drag ends (on release).")]
    public bool snapOnReleaseOnly = false;

    [Header("Rotation Snapping")]
    [Tooltip("Snap rotation while dragging / on release.")]
    public bool snapRotation = false;
    [Tooltip("Angle step in degrees used for rotation snapping on each axis.")]
    public Vector3 rotationSnapAngle = new Vector3(90f, 90f, 90f);

    private bool isBeingDragged = false;
    private Rigidbody rigidBody;
    private Vector3 throwVelocity = Vector3.zero;

    // Track previous target position for stable velocity calculation
    private Vector3 lastTargetPosition;

    void Awake()
    {
        if (removeFromParentAtAwake)
        {
            transform.SetParent(null, true);
        }
    }

    /// <summary>
    /// Called when dragging starts. Optionally modifies Rigidbody settings.
    /// </summary>
    public void StartDrag(RigidbodyStateChange stateChange)
    {
        if (!enabled) return;

        throwVelocity = Vector3.zero;

        rigidBody = GetComponent<Rigidbody>();
        OnGrab();
        isBeingDragged = true;

        // initialize lastTargetPosition so first velocity sample is stable
        lastTargetPosition = transform.position;

        ApplyRigidbodyStateChange(stateChange);
    }

    /// <summary>
    /// Changes the Rigidbody's kinematic state, if required, for all connected rigidbodies.
    /// </summary>
    private void ApplyRigidbodyStateChange(RigidbodyStateChange stateChange)
    {
        if (shouldIgnoreRigidbodySettingFromDragger || stateChange == RigidbodyStateChange.Unchanged)
            return;

        Weldable weldable = GetComponent<Weldable>();

        IReadOnlyList<Rigidbody> rigidbodies;
        if (weldable && weldable.weldType == WeldType.HierarchyBased)
        {
            // Find all rigidbodies in the connected hierarchy/weld
            rigidbodies = Utils.FindAllInHierarchyAndConnections<Rigidbody>(weldable);
        }
        else
        {
            rigidbodies = new List<Rigidbody> { rigidBody }.AsReadOnly();
        }

        foreach (var rb in rigidbodies)
        {
            if (rb == null) continue;
            rb.isKinematic = (stateChange == RigidbodyStateChange.SetKinematic);
        }
    }

    /// <summary>
    /// Updates the object's position and rotation during dragging.
    /// Call this with the desired world-space position and rotation for the dragged object.
    /// </summary>
    public void UpdateDrag(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (!enabled || !isBeingDragged) return;

        // Velocity measurement based on change in target position (not rigidbody position)
        float dt = Time.deltaTime;
        if (dt <= 0f) dt = Time.fixedDeltaTime;

        Vector3 measuredVelocity = (targetPosition - lastTargetPosition) / dt;
        throwVelocity = Vector3.Lerp(throwVelocity, measuredVelocity, 0.1f);
        lastTargetPosition = targetPosition;

        // If grid snapping is enabled and not only on release, apply snapping to the target before moving.
        Vector3 appliedPosition = targetPosition;
        Quaternion appliedRotation = targetRotation;

        if (useGridSnapping && !snapOnReleaseOnly)
        {
            appliedPosition = GetSnappedPosition(targetPosition);
            if (snapRotation) appliedRotation = GetSnappedRotation(targetRotation);
        }

        ApplyTransformation(appliedPosition, appliedRotation);
        CustomFixedJoint.UpdateJoint(transform);
    }

    /// <summary>
    /// Applies the transformation using Rigidbody or Transform.
    /// </summary>
    private void ApplyTransformation(Vector3 position, Quaternion rotation)
    {
        if (rigidBody != null)
        {
            MoveRigidbody(position, rotation);
        }
        else
        {
            MoveTransform(position, rotation);
        }
    }

    /// <summary>
    /// Moves the object using Transform. Handles parented objects correctly.
    /// </summary>
    private void MoveTransform(Vector3 position, Quaternion rotation)
    {
        if (transform.parent == null)
        {
            transform.position = position;
            transform.rotation = rotation;
        }
        else
        {
            Transform root = transform.root;

            Matrix4x4 currentLocalMatrix = root.worldToLocalMatrix * transform.localToWorldMatrix;
            Matrix4x4 desiredWorldMatrix = Matrix4x4.TRS(position, rotation, transform.lossyScale);
            Matrix4x4 newRootWorldMatrix = desiredWorldMatrix * currentLocalMatrix.inverse;

            root.position = newRootWorldMatrix.GetColumn(3);
            root.rotation = Quaternion.LookRotation(
                newRootWorldMatrix.GetColumn(2),
                newRootWorldMatrix.GetColumn(1)
            );
        }
    }

    /// <summary>
    /// Moves the object using Rigidbody methods.
    /// </summary>
    private void MoveRigidbody(Vector3 position, Quaternion rotation)
    {
        // Use MovePosition / MoveRotation to let physics apply interpolated movement
        rigidBody.MoveRotation(rotation);
        rigidBody.MovePosition(position);
    }

    /// <summary>
    /// Ends the dragging operation and restores Rigidbody settings if needed.
    /// </summary>
    public void EndDrag(RigidbodyStateChange stateChange, float throwMultiplier, float maxThrowVelocity)
    {
        if (!enabled) return;

        OnRelease();

        // Apply snapping on release if configured
        if (useGridSnapping && snapOnReleaseOnly)
        {
            Vector3 snappedPosition = GetSnappedPosition(transform.position);
            Quaternion snappedRotation = snapRotation ? GetSnappedRotation(transform.rotation) : transform.rotation;

            if (rigidBody != null)
            {
                // Move rigidbody to snapped transform immediately using MovePosition/MoveRotation.
                rigidBody.MovePosition(snappedPosition);
                rigidBody.MoveRotation(snappedRotation);
            }
            else
            {
                if (transform.parent == null)
                {
                    transform.position = snappedPosition;
                    transform.rotation = snappedRotation;
                }
                else
                {
                    // If parented, reapply using the same method as MoveTransform to keep hierarchy consistent
                    MoveTransform(snappedPosition, snappedRotation);
                }
            }
        }

        ApplyRigidbodyStateChange(stateChange);
        isBeingDragged = false;

        if (rigidBody != null && !rigidBody.isKinematic)
        {
            Vector3 newThrowVelocity = throwVelocity * throwMultiplier;
            if (newThrowVelocity.magnitude > maxThrowVelocity)
            {
                newThrowVelocity = newThrowVelocity.normalized * maxThrowVelocity;
            }

            // Apply measured throw velocity to physics
            rigidBody.linearVelocity = newThrowVelocity;
        }

        // Reset for next drag
        throwVelocity = Vector3.zero;
    }

    // Utility: Snap a world position to the configured grid
    private Vector3 GetSnappedPosition(Vector3 worldPos)
    {
        // Avoid division by zero per-axis: if gridSize component <= 0 we skip snapping that axis.
        float x = (gridSize.x > 0f) ? Mathf.Round(worldPos.x / gridSize.x) * gridSize.x : worldPos.x;
        float y = (gridSize.y > 0f) ? Mathf.Round(worldPos.y / gridSize.y) * gridSize.y : worldPos.y;
        float z = (gridSize.z > 0f) ? Mathf.Round(worldPos.z / gridSize.z) * gridSize.z : worldPos.z;
        return new Vector3(x, y, z);
    }

    // Utility: Snap a rotation to the configured angle steps (per-axis)
    private Quaternion GetSnappedRotation(Quaternion rot)
    {
        Vector3 e = rot.eulerAngles;
        float sx = (rotationSnapAngle.x > 0f) ? Mathf.Round(e.x / rotationSnapAngle.x) * rotationSnapAngle.x : e.x;
        float sy = (rotationSnapAngle.y > 0f) ? Mathf.Round(e.y / rotationSnapAngle.y) * rotationSnapAngle.y : e.y;
        float sz = (rotationSnapAngle.z > 0f) ? Mathf.Round(e.z / rotationSnapAngle.z) * rotationSnapAngle.z : e.z;
        return Quaternion.Euler(sx, sy, sz);
    }

    /// <summary>
    /// Finds all drag listeners affected by this object.
    /// </summary>
    private IDragListener[] GetConnectedDragListeners()
    {
        if (shouldPropagateDragEvents)
        {
            Weldable weldable = GetComponentInParent<Weldable>();
            if (weldable != null)
            {
                return Utils.FindAllInHierarchyAndConnections<IDragListener>(weldable).ToArray();
            }

            Transform root = transform.root;
            return root.GetComponentsInChildren<IDragListener>();
        }
        else
        {
            var result = new List<IDragListener>();
            var stack = new Stack<Transform>();
            stack.Push(transform);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current != transform && current.GetComponent<Weldable>() != null)
                    continue;

                foreach (var listener in current.GetComponents<IDragListener>())
                {
                    if (listener != null)
                        result.Add(listener);
                }

                foreach (Transform child in current)
                {
                    stack.Push(child);
                }
            }

            return result.ToArray();
        }
    }

    /// <summary>
    /// Notifies all listeners that the object was grabbed.
    /// </summary>
    private void OnGrab()
    {
        if (!enabled) return;

        foreach (DragListener dragListener in GetConnectedDragListeners())
        {
            dragListener.OnGrab();
        }
    }

    /// <summary>
    /// Notifies all listeners that the object was released.
    /// </summary>
    private void OnRelease()
    {
        if (!enabled) return;

        foreach (DragListener dragListener in GetConnectedDragListeners())
        {
            dragListener.OnRelease();
        }
    }
}