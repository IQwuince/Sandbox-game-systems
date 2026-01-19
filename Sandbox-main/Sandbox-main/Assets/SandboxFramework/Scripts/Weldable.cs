using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the type of welding behavior between objects.
/// </summary>
public enum WeldType
{
    Undefined,
    HierarchyBased,
    PhysicsBased
}

[DisallowMultipleComponent]
public class Weldable : MonoBehaviour
{
    private WeldType currentWeldType = WeldType.Undefined;
    private readonly HashSet<Weldable> connections = new();

    // Joints created for physics-based welds. We track them so they can be cleaned up and so
    // a joint break can be handled.
    private readonly Dictionary<Weldable, List<Joint>> physicsJoints = new();

    [Header("Weld Settings")]
    [Tooltip("If true, this component will attempt an automatic weld to the first parent Weldable found on Start.")]
    public bool allowAutoWeld = false;

    [Tooltip("Break force applied to created FixedJoint(s). If an enemy or other physics interaction exceeds this, the joint will break.")]
    public float jointBreakForce = 1f;

    [Tooltip("Break torque applied to created FixedJoint(s).")]
    public float jointBreakTorque = 1f;

    public WeldType weldType => currentWeldType;

    private IEnumerator Start()
    {
        // Delay one frame so other components (like Rigidbody) have initialized.
        yield return null;

        if (allowAutoWeld)
            TryAutoHierarchyWeldWithAncestor();
    }

    /// <summary>
    /// Attempts to automatically weld this object to the first parent Weldable found in the hierarchy.
    /// The auto-weld chooses physics-based if either side has a Rigidbody.
    /// </summary>
    private void TryAutoHierarchyWeldWithAncestor()
    {
        Transform current = transform.parent;

        while (current != null)
        {
            var parentWeldable = current.GetComponent<Weldable>();
            if (parentWeldable != null && !IsConnected(parentWeldable))
            {
                // Choose a weld type that is compatible with rigidbodies.
                WeldType chosen = ChooseAppropriateWeldType(parentWeldable, WeldType.HierarchyBased);
                WeldTo(parentWeldable, chosen, true);
                break;
            }

            current = current.parent;
        }
    }

    /// <summary>
    /// Public helper for player-initiated welding. The API will pick a safe weld type (physics when either side has Rigidbody).
    /// Use this from your building/placement code when the player places a block.
    /// </summary>
    public void WeldByPlayerTo(Weldable target, Transform overlappingTransform = null)
    {
        if (target == null) return;
        WeldType chosen = ChooseAppropriateWeldType(target, WeldType.HierarchyBased);
        WeldTo(target, chosen, false, overlappingTransform);
    }

    /// <summary>
    /// Public helper for unwelding by player.
    /// </summary>
    public void UnweldByPlayer()
    {
        Unweld();
    }

    /// <summary>
    /// Internal weld function. It will attempt to set compatible types on both objects and apply the chosen weld.
    /// </summary>
    internal void WeldTo(Weldable target, WeldType weldType, bool isAutoWeld = false, Transform overlappingTransform = null)
    {
        if (!enabled || target == null || target == this)
            return;

        bool wasIsolated = connections.Count == 0;
        bool targetWasIsolated = target.connections.Count == 0;

        // If requested HierarchyBased but either has a Rigidbody, switch to PhysicsBased for safety.
        weldType = ChooseAppropriateWeldType(target, weldType);

        if (!TrySetWeldType(weldType) || !target.TrySetWeldType(weldType))
        {
            Debug.LogWarning($"Weld failed: type mismatch ({name} ↔ {target.name})");
            return;
        }

        if (IsConnected(target))
        {
            Debug.LogWarning($"{name} and {target.name} are already connected.");
            return;
        }

        AddConnection(target);
        target.AddConnection(this);

        if (weldType == WeldType.HierarchyBased)
        {
            // For hierarchy welding we parent this under the target (or the overlappingTransform if supplied).
            ApplyHierarchyWeld(target, overlappingTransform);
        }
        else if (weldType == WeldType.PhysicsBased)
        {
            ApplyPhysicsWeld(target);
        }

        NotifyOnWeld(wasIsolated);
        target.NotifyOnWeld(targetWasIsolated);
    }

    /// <summary>
    /// Unwelds this object from all connected weldables.
    /// </summary>
    internal void Unweld()
    {
        if (!enabled) return;

        bool wasGrouped = connections.Count > 0;
        NotifyOnUnweld(wasGrouped);

        List<Weldable> connectionsToRemove = new();

        foreach (var connection in connections)
        {
            connectionsToRemove.Add(connection);

            if (connection.connections.Remove(this))
            {
                bool connectionIsIsolatedAfterUnweld = connection.connections.Count == 0;
                connection.NotifyOnUnweld(connectionIsIsolatedAfterUnweld);
            }

            // Remove any physics joints we created for this connection
            if (physicsJoints.TryGetValue(connection, out var jointList))
            {
                foreach (var joint in jointList)
                {
                    if (joint != null) Destroy(joint);
                }
                physicsJoints.Remove(connection);
            }

            // Also remove entries on the other side if present
            if (connection.physicsJoints != null && connection.physicsJoints.TryGetValue(this, out var otherJointList))
            {
                foreach (var joint in otherJointList)
                {
                    if (joint != null) Destroy(joint);
                }
                connection.physicsJoints.Remove(this);
            }
        }

        if (currentWeldType == WeldType.HierarchyBased)
            RemoveHierarchyWelds(connections);
        else if (currentWeldType == WeldType.PhysicsBased)
            RemovePhysicsWelds(connections);

        connections.Clear();
        physicsJoints.Clear();

        if (connections.Count < 1) currentWeldType = WeldType.Undefined;
    }

    /// <summary>
    /// Applies hierarchy-based welding by reparenting to the target.
    /// </summary>
    private void ApplyHierarchyWeld(Weldable target, Transform overlappingTransform)
    {
        Transform targetTransform = overlappingTransform ?? target.transform;

        // Reparent any weldable ancestors so the group becomes a clean chain under the target.
        if (transform.parent != null)
        {
            ReparentWeldableAncestors();
        }

        transform.SetParent(targetTransform, true);
    }

    /// <summary>
    /// Applies physics-based welding using FixedJoint components that are breakable.
    /// </summary>
    private void ApplyPhysicsWeld(Weldable target)
    {
        Rigidbody thisRb = GetOrAddRigidbody(gameObject);
        Rigidbody targetRb = GetOrAddRigidbody(target.gameObject);

        // Create a joint on this object connected to the target rb
        FixedJoint joint = gameObject.AddComponent<FixedJoint>();
        joint.connectedBody = targetRb;
        joint.breakForce = jointBreakForce;
        joint.breakTorque = jointBreakTorque;

        // Optionally create a symmetric joint on the target to make cleanup/detection simpler
        FixedJoint jointOnTarget = target.gameObject.AddComponent<FixedJoint>();
        jointOnTarget.connectedBody = thisRb;
        jointOnTarget.breakForce = jointBreakForce;
        jointOnTarget.breakTorque = jointBreakTorque;

        // Track joints so they can be removed later
        if (!physicsJoints.TryGetValue(target, out var list)) physicsJoints[target] = list = new List<Joint>();
        list.Add(joint);

        if (!target.physicsJoints.TryGetValue(this, out var otherList)) target.physicsJoints[this] = otherList = new List<Joint>();
        otherList.Add(jointOnTarget);
    }

    /// <summary>
    /// Removes hierarchy-based welds by unparenting connected weldables.
    /// </summary>
    private void RemoveHierarchyWelds(IEnumerable<Weldable> connected)
    {
        // If this was parented under someone, unparent ourselves and any children that were part of the weld
        List<Weldable> children = GetChildWeldables();
        transform.SetParent(null, true);

        foreach (Weldable weldable in children)
        {
            weldable.transform.SetParent(null, true);
        }
    }

    /// <summary>
    /// Removes physics-based welds by destroying joint components we created.
    /// </summary>
    private void RemovePhysicsWelds(IEnumerable<Weldable> connected)
    {
        foreach (var other in connected)
        {
            if (physicsJoints.TryGetValue(other, out var list))
            {
                foreach (var joint in list)
                {
                    if (joint != null) Destroy(joint);
                }
            }

            if (other.physicsJoints.TryGetValue(this, out var otherList))
            {
                foreach (var joint in otherList)
                {
                    if (joint != null) Destroy(joint);
                }
            }
        }

        // Clean up any joints left on this object that target a now-removed connection
        foreach (var joint in GetComponents<Joint>())
        {
            // If a joint is left with a null connectedBody or connected to something not in connections, destroy it
            if (joint == null) continue;
            if (joint.connectedBody == null) Destroy(joint);
            else
            {
                Weldable connectedWeldable = joint.connectedBody.GetComponentInParent<Weldable>();
                if (connectedWeldable == null || !connections.Contains(connectedWeldable))
                {
                    Destroy(joint);
                }
            }
        }
    }

    /// <summary>
    /// Reparents all weldable ancestors to create a clean hierarchy chain.
    /// </summary>
    private void ReparentWeldableAncestors()
    {
        Weldable root = this;
        if (root == null) return;

        List<Transform> weldableAncestors = new();
        Transform current = root.transform;

        while (current != null)
        {
            Weldable weldable = current.GetComponent<Weldable>();
            if (weldable)
            {
                weldableAncestors.Add(current);
            }
            current = current.parent;
        }

        foreach (Transform t in weldableAncestors)
            t.SetParent(null, true);

        for (int i = weldableAncestors.Count - 1; i > 0; i--)
            weldableAncestors[i].SetParent(weldableAncestors[i - 1], true);
    }

    /// <summary>
    /// Gets or adds a Rigidbody to a GameObject.
    /// </summary>
    private static Rigidbody GetOrAddRigidbody(GameObject obj)
    {
        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = obj.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        return rb;
    }

    /// <summary>
    /// Returns the topmost Weldable in the parent hierarchy.
    /// </summary>
    private Weldable GetRootWeldable()
    {
        Transform current = transform;
        Weldable lastFound = null;

        while (current != null)
        {
            var weldable = current.GetComponent<Weldable>();
            if (weldable != null)
                lastFound = weldable;

            current = current.parent;
        }

        return lastFound;
    }

    /// <summary>
    /// Returns all Weldable components in the child hierarchy.
    /// </summary>
    private List<Weldable> GetChildWeldables()
    {
        var result = new List<Weldable>();
        var stack = new Stack<Transform>();

        foreach (Transform child in transform)
        {
            stack.Push(child);
        }

        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            Weldable weldable = current.GetComponent<Weldable>();

            if (weldable)
            {
                result.Add(weldable);
            }
            else
            {
                foreach (Transform child in current)
                {
                    stack.Push(child);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets all directly connected weldables.
    /// </summary>
    internal IReadOnlyCollection<Weldable> GetDirectConnections() => connections;

    /// <summary>
    /// Gets all connected weldables recursively (excluding self).
    /// </summary>
    internal HashSet<Weldable> GetAllConnectedRecursive()
    {
        var result = new HashSet<Weldable>();
        var stack = new Stack<Weldable>();
        stack.Push(this);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var conn in current.connections)
            {
                if (conn != this && result.Add(conn))
                {
                    stack.Push(conn);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns whether this Weldable is connected to another.
    /// </summary>
    internal bool IsConnected(Weldable other)
    {
        return connections.Contains(other);
    }

    /// <summary>
    /// Adds a connection to another Weldable.
    /// </summary>
    private void AddConnection(Weldable other)
    {
        if (other != null && other != this)
        {
            connections.Add(other);
        }
    }

    /// <summary>
    /// Attempts to set the weld type for this object.
    /// </summary>
    private bool TrySetWeldType(WeldType newType)
    {
        if (currentWeldType == WeldType.Undefined)
        {
            currentWeldType = newType;
            return true;
        }

        return currentWeldType == newType;
    }

    /// <summary>
    /// Retrieves all IWeldListener components in this object and its descendants,
    /// skipping children that have their own Weldable component.
    /// </summary>
    private IEnumerable<IWeldListener> GetDescendantWeldListeners()
    {
        foreach (var listener in GetComponents<IWeldListener>())
            yield return listener;

        var stack = new Stack<Transform>();
        stack.Push(transform);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            foreach (Transform child in current)
            {
                if (child.GetComponent<Weldable>() != null)
                    continue;

                foreach (var listener in child.GetComponents<IWeldListener>())
                    yield return listener;

                stack.Push(child);
            }
        }
    }

    /// <summary>
    /// Notifies listeners that this object has been welded.
    /// </summary>
    private void NotifyOnWeld(bool joinedWeldGroup)
    {
        foreach (var listener in GetDescendantWeldListeners())
        {
            if (joinedWeldGroup) listener.OnWeld();
            listener.OnAdded();
        }
    }

    /// <summary>
    /// Notifies listeners that this object has been unwelded.
    /// </summary>
    private void NotifyOnUnweld(bool leavedWeldGroup)
    {
        foreach (var listener in GetDescendantWeldListeners())
        {
            listener.OnRemoved();
            if (leavedWeldGroup) listener.OnUnweld();
        }
    }

    /// <summary>
    /// Helper to pick a weld type that is safe given rigidbodies on either side.
    /// If either side has a Rigidbody, prefer PhysicsBased welds.
    /// </summary>
    private WeldType ChooseAppropriateWeldType(Weldable target, WeldType preferred)
    {
        bool thisHasRb = GetComponentInChildren<Rigidbody>() != null;
        bool targetHasRb = target.GetComponentInChildren<Rigidbody>() != null;

        if (thisHasRb || targetHasRb)
            return WeldType.PhysicsBased;

        return preferred;
    }

    /// <summary>
    /// Unity callback when any joint on this GameObject breaks due to exceeding breakForce / breakTorque.
    /// We respond by unwelding this object (the physics joint has already been destroyed by Unity).
    /// </summary>
    private void OnJointBreak(float breakForce)
    {
        // When a joint breaks, Unity calls this on the GameObject that owned the joint.
        // The simplest robust behavior is to unweld this object so the group separates.
        Unweld();
    }
}