using UnityEngine;

public class ZoneDefender : MonoBehaviour
{
    [Header("Patrol Area")]
    [SerializeField] private BoxCollider defenderZone; // Territory bounds

    [Header("Movement Settings")]
    [SerializeField] private float speed = 3.5f;
    [SerializeField] private float obstacleCheckDistance = 1.0f; // Front raycast distance

    private Vector3 targetPosition;
    private float initialY;

    private void Start()
    {
        initialY = transform.position.y;
        ResetDefender();
    }

    private void Update()
    {
        if (defenderZone == null) return;

        // 1. Raycast forward to detect incoming walls/obstacles early
        Vector3 rayOrigin = new Vector3(transform.position.x, initialY, transform.position.z);
        if (Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, obstacleCheckDistance))
        {
            if (hit.collider.CompareTag("Wall") || hit.collider.CompareTag("Obstacle"))
            {
                Bounce(hit.normal);
            }
        }

        // 2. Move towards current target
        Vector3 targetOnPlane = new Vector3(targetPosition.x, initialY, targetPosition.z);
        transform.position = Vector3.MoveTowards(transform.position, targetOnPlane, speed * Time.deltaTime);

        // 3. Smooth rotation towards movement direction
        Vector3 moveDirection = (targetOnPlane - transform.position).normalized;
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        // 4. Pick new target when destination is reached
        Vector3 currentFlat = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetFlat = new Vector3(targetPosition.x, 0, targetPosition.z);

        if (Vector3.Distance(currentFlat, targetFlat) < 0.3f)
        {
            GetNewTargetInZone();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Physics collision fallback: Bounce if physical contact occurs
        if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Obstacle"))
        {
            Vector3 contactNormal = collision.contacts[0].normal;
            Bounce(contactNormal);
        }
    }

    private void Bounce(Vector3 hitNormal)
    {
        hitNormal.y = 0; // Keep bounce strictly on the 2D floor plane
        if (hitNormal == Vector3.zero) hitNormal = -transform.forward;

        // Calculate bounce direction vector using reflection formula
        Vector3 moveDir = (targetPosition - transform.position).normalized;
        if (moveDir == Vector3.zero) moveDir = transform.forward;

        Vector3 bounceDirection = Vector3.Reflect(moveDir, hitNormal).normalized;

        // Set new target 4 units away in the bounce direction
        targetPosition = transform.position + bounceDirection * 4.0f;

        // Clamp target to remain inside defenderZone bounds
        if (defenderZone != null)
        {
            Bounds bounds = defenderZone.bounds;
            targetPosition.x = Mathf.Clamp(targetPosition.x, bounds.min.x, bounds.max.x);
            targetPosition.z = Mathf.Clamp(targetPosition.z, bounds.min.z, bounds.max.z);
        }
    }

    public void ResetDefender()
    {
        initialY = transform.position.y;
        if (defenderZone != null)
        {
            GetNewTargetInZone();
            transform.position = new Vector3(targetPosition.x, initialY, targetPosition.z);
        }
    }

    private void GetNewTargetInZone()
    {
        Bounds bounds = defenderZone.bounds;
        targetPosition = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            initialY,
            Random.Range(bounds.min.z, bounds.max.z)
        );
    }
}