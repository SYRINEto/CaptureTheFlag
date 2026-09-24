using UnityEngine;

public class ObstacleController : MonoBehaviour
{
    [Header("Wander Settings")]
    [SerializeField] private float moveSpeed = 0.15f;
    [SerializeField] private float changeDirectionTime = 3f;
    [SerializeField] private float destinationReachedThreshold = 0.3f;

    [Header("Arena Bounds (matches AgentLearn)")]
    [SerializeField] private float minX = -5.26f;
    [SerializeField] private float maxX = 4.98f;
    [SerializeField] private float minZ = -6.48f;
    [SerializeField] private float maxZ = 3.76f;

    [Header("Reset Behavior")]
    [SerializeField] private bool randomizeSpawnOnReset = false;

    private Vector3 spawnPosition;
    private Vector3 targetPosition;
    private float waitTimer;
    private bool waiting;
    private Vector3 velocity;

    public Vector3 Velocity => velocity;
    public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }

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
    public void ResetObstacle()
    {
        waiting = false;
        velocity = Vector3.zero;
        if (randomizeSpawnOnReset)
        {
            transform.localPosition = new Vector3(
                Random.Range(minX + 0.8f, maxX - 0.8f),
                spawnPosition.y,
                Random.Range(minZ + 0.8f, maxZ - 0.8f)
            );
        }
        else
        {
            transform.localPosition = spawnPosition;
        }
        PickNewDestination();
    }

    /// <summary>
    /// Alias to match DefenderController naming conventions.
    /// </summary>
    public void ResetDefender()
    {
        ResetObstacle();

        
    }

}
