using UnityEngine;

public class DefenderControllerAziz : MonoBehaviour
{
    [Header("Wander Settings")]
    [SerializeField] private float moveSpeed = 0.1f;
    [SerializeField] private float changeDirectionTime = 3f;
    [SerializeField] private float destinationReachedThreshold = 0.3f;

    [Header("Arena Bounds (should match AgentLearn's bounds)")]
    [SerializeField] private float minX = -5.26f;
    [SerializeField] private float maxX = 4.98f;
    [SerializeField] private float minZ = -6.48f;
    [SerializeField] private float maxZ = 3.76f;

    private Vector3 spawnPosition;
    private Vector3 targetPosition;
    private float waitTimer;
    private bool waiting;
    private Vector3 velocity;

    public Vector3 Velocity => velocity;

    private void Awake()
    {
        spawnPosition = transform.localPosition;
    }

    private void Start()
    {
        PickNewDestination();
    }

    private void Update()
    {
        if (waiting)
        {
            velocity = Vector3.zero;
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                waiting = false;
                PickNewDestination();
            }
            return;
        }

        Vector3 direction = targetPosition - transform.localPosition;
        direction.y = 0f;

        if (direction.magnitude <= destinationReachedThreshold)
        {
            waiting = true;
            waitTimer = changeDirectionTime;
            velocity = Vector3.zero;
            return;
        }

        Vector3 move = direction.normalized * moveSpeed * Time.deltaTime;
        transform.localPosition += move;
        velocity = move / Time.deltaTime;
    }

    private void PickNewDestination()
    {
        targetPosition = new Vector3(
            Random.Range(minX + 0.5f, maxX - 0.5f),
            transform.localPosition.y,
            Random.Range(minZ + 0.5f, maxZ - 0.5f)
        );
    }

    /// <summary>
    /// Called by AgentLearn at the start of every training episode.
    /// </summary>
    public void ResetDefender()
    {
        waiting = false;
        velocity = Vector3.zero;
        transform.localPosition = spawnPosition;
        PickNewDestination();
    }

    

}