using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// IMPORTANT: This GameObject MUST also have a NetworkTransform component
// (Unity.Netcode.Components.NetworkTransform) attached in the Inspector.
// Without it, transform.Rotate / rb.MovePosition changes made on the server
// inside the ServerRpc will NEVER be replicated to any client (including the
// owner), so the object will appear to not move even though input is being
// received and processed correctly.
[RequireComponent(typeof(Rigidbody))]
public class Movement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float jumpForce = 5f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference attackAction;

    private Rigidbody rb;
    private Animator animator;

    private Vector2 inputVector;

    // Written ONLY on the server, read by everyone.
    private NetworkVariable<bool> networkIsGrounded =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    // Replicated speed so remote clients can animate this object correctly.
    // Without this, only the owner's local Update() ever calls
    // HandleAnimations(), so every other client sees a frozen idle pose
    // for anyone else's character.
    private NetworkVariable<float> networkSpeed =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        // Only the server simulates physics. Everyone else is kinematic
        // and just visually follows whatever NetworkTransform replicates.
        if (!IsServer && rb != null)
        {
            rb.isKinematic = true;
        }

        // Animation parameters are driven from replicated NetworkVariables
        // below (see Update), so this runs for every instance, not just
        // the owner.

        if (!IsOwner) return;

        moveAction?.action.Enable();
        jumpAction?.action.Enable();
        attackAction?.action.Enable();

        if (jumpAction != null) jumpAction.action.performed += OnJumpInput;
        if (attackAction != null) attackAction.action.performed += OnAttackInput;
    }

    void Update()
    {
        // Animations are driven from replicated state for ALL instances,
        // so this runs regardless of ownership.
        HandleAnimations();

        if (!IsOwner) return;

        inputVector = moveAction.action.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        // Only the machine that is actually running as the server should
        // ever touch server-authoritative NetworkVariables. IsServer is
        // true on the host's process and on a dedicated server process,
        // and is false on every pure client - this check alone is correct.
        // If you saw a write-permission error on a "Client" instance, it
        // means that instance's process actually has IsServer == true
        // (e.g. it's running as Host, not Client) - double check how you
        // started that instance (Host vs Client) in your NetworkManager /
        // bootstrap UI, since this code path is only reachable when
        // IsServer is true.
        if (IsServer)
        {
            UpdateGroundedState();
            UpdateNetworkSpeed();
        }

        if (IsOwner)
        {
            SubmitMovementServerRpc(inputVector);
        }
    }

    // ---------------- MOVEMENT ----------------

    [ServerRpc]
    private void SubmitMovementServerRpc(Vector2 input)
    {
        if (rb == null) return;

        float rotation = input.x * rotationSpeed * Time.fixedDeltaTime;
        transform.Rotate(0, rotation, 0);

        Vector3 move = transform.forward * input.y * moveSpeed * Time.fixedDeltaTime;

        rb.MovePosition(rb.position + move);

        // NOTE: transform.Rotate / rb.MovePosition above only change the
        // SERVER's copy of this transform. A NetworkTransform component on
        // this GameObject is what actually replicates position/rotation to
        // every client. There is nothing further to do here in code - just
        // make sure that component is attached and configured to sync
        // Position + Rotation, with "Server Authoritative" mode on (which
        // matches this script's server-authoritative movement).
    }

    // ---------------- JUMP ----------------

    private void OnJumpInput(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if (!networkIsGrounded.Value) return;

        JumpServerRpc();
    }

    [ServerRpc]
    private void JumpServerRpc()
    {
        if (!networkIsGrounded.Value) return;
        if (rb == null) return;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        JumpClientRpc();
    }

    [ClientRpc]
    private void JumpClientRpc()
    {
        animator?.SetTrigger("Jump");
    }

    // ---------------- ATTACK ----------------

    private void OnAttackInput(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        animator?.SetTrigger("Attack");

        AttackServerRpc();
    }

    [ServerRpc]
    private void AttackServerRpc()
    {
        AttackClientRpc();
    }

    [ClientRpc]
    private void AttackClientRpc()
    {
        if (IsOwner) return;

        animator?.SetTrigger("Attack");
    }

    // ---------------- GROUND CHECK (SERVER ONLY) ----------------

    private void UpdateGroundedState()
    {
        if (groundCheck == null) return;

        networkIsGrounded.Value =
            Physics.CheckSphere(groundCheck.position, 0.25f, groundLayer);
    }

    // ---------------- SPEED REPLICATION (SERVER ONLY) ----------------

    private void UpdateNetworkSpeed()
    {
        if (rb == null) return;

        float speed = Vector3.Dot(rb.linearVelocity, transform.forward);
        networkSpeed.Value = Mathf.Abs(speed);
    }

    // ---------------- ANIMATION (RUNS ON EVERY INSTANCE) ----------------

    private void HandleAnimations()
    {
        if (animator == null) return;

        animator.SetFloat("Speed", networkSpeed.Value);
        animator.SetBool("IsGrounded", networkIsGrounded.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (jumpAction != null) jumpAction.action.performed -= OnJumpInput;
        if (attackAction != null) attackAction.action.performed -= OnAttackInput;
    }
}