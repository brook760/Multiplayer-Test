using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine;

public class Movement : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 100f;
    
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Settings")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference attackAction;

    private Rigidbody rb;
    [SerializeField] private Animator animator;
    private Vector2 inputVector;
    private bool isGrounded;

    // --- Server Tracking Variables ---
    // We use a NetworkVariable to sync the grounded state smoothly from server to clients
    private NetworkVariable<bool> networkIsGrounded = 
       new NetworkVariable<bool>(
            false, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {

        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        if(rb != null && !IsServer){
            rb.isKinematic = true; // Clients won't do physics calculations, so we make the Rigidbody kinematic to prevent desync issues
        }

        if (!IsOwner) return;

        if (moveAction != null) moveAction.action.Enable();

        if ((jumpAction != null))
        {
            jumpAction.action.Enable();
            jumpAction.action.performed +=  OnJumpInput;
        }

        if (attackAction != null)
        {
            attackAction.action.Enable();
            attackAction.action.performed += OnAttackInput;
        }
    }

    void Update()
    {
        HandleAnimations();

        if (!IsOwner) return;

        // Read input values smoothly every frame
        inputVector =  moveAction.action.ReadValue<Vector2>();
    }

    void FixedUpdate()
    {
        if (IsServer)
        {
            // Ground check visualization loop
            networkIsGrounded.Value  = Physics.CheckSphere(groundCheck.position, 0.3f, groundLayer);
        }
        if (IsOwner)
        {
            MoveServerRpc(inputVector);  
        }
    }

    [ServerRpc] // Client calls this, but it RUNS on the Server
    private void MoveServerRpc(Vector2 localInput)
    {
        if (rb == null) return;

        // Perform Rotation using A and D keys (X-axis of Vector2)
        float rotationAmount = localInput.x * rotationSpeed * Time.deltaTime;
        transform.Rotate(Vector3.up, rotationAmount);

        // We use inputVector.y (W and S keys)
        Vector3 movestep = transform.forward * localInput.y * moveSpeed* Time.deltaTime;

        //movestep.y = rb.linearVelocity.y; // Preserve vertical velocity for jumping/falling
        // Apply the velocity update cleanly to the Rigidbody
        rb.MovePosition(rb.position + movestep);
        //rb.linearVelocity = movestep;
    }
    private void OnJumpInput(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;
        if(!networkIsGrounded.Value) return;

        JumpServerRpc();
    }

    [ServerRpc]
    private void JumpServerRpc()
    {
        if (!networkIsGrounded.Value) return;
        
        rb.isKinematic = false; // Ensure physics is active for jumping
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        
        TriggerJumpClientRpc();

    }

    [ClientRpc]
    private void TriggerJumpClientRpc()
    {
        if (animator != null)  animator.SetTrigger("Jump");
    }

    private void OnAttackInput(InputAction.CallbackContext context)
    {
        if (!IsOwner) return;

        if (animator != null) animator.SetTrigger("Attack");

        // to everyone else so they see the swing/hitbox activate!
        PerformMeleeAttackServerRpc();
    }

    [ServerRpc] // Tells the local client to send a command packet straight to the host server
    private void PerformMeleeAttackServerRpc()
    {
        // The Server receives the message, and reflects it outwards to all connected players
        PerformMeleeAttackClientRpc();
    }

    [ClientRpc] // Tells the Server to force every single game instance running to run this method
    private void PerformMeleeAttackClientRpc()
    {
        // Skip the owner because they already processed their input locally 
        if (IsOwner) return; // Already played locally
        if (animator != null) animator.SetTrigger("Attack");

        // Network Sync Action: Trigger your attack animation or spawn your damage hitboxes here!
        Debug.Log("A remote peer performed a melee attack. Synchronizing visual effects.");
    }
    private void HandleAnimations()
    {
        if (animator == null) return;

        if (IsServer)
        {
            // Host checks values directly from Rigidbody velocity states
            float speed = Vector3.Dot(rb.linearVelocity, transform.forward);
            animator.SetFloat("Speed", Mathf.Abs(speed));
            animator.SetBool("IsGrounded", networkIsGrounded.Value);
        }
        else if (IsOwner)
        {
            // Client predicts local movement parameters for zero visual latency
            animator.SetFloat("Speed", Mathf.Abs(inputVector.y));
            animator.SetBool("IsGrounded", networkIsGrounded.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        // Clean up event hook listeners when leaving to prevent garbage memory build-up
        if (!IsOwner) return;

        if (jumpAction != null) jumpAction.action.performed -= OnJumpInput;
        if (attackAction != null) attackAction.action.performed -= OnAttackInput;
    }
}
