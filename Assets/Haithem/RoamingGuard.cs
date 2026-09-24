using UnityEngine;

public class RoamingGuard : MonoBehaviour
{
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float retargetWindow = 3f;
    [SerializeField] private float fieldRadius = 9f;

    private Rigidbody _rb;
    private Vector3 waypoint;
    private float roamClock;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            Debug.LogError($"{gameObject.name}: a Rigidbody is required on this object.");
            enabled = false;
            return;
        }

        ResetPosition();
    }

    public void ResetPosition()
    {
        Vector2 landing = Random.insideUnitCircle * (fieldRadius - 0.75f);
        _rb.position = new Vector3(landing.x, transform.position.y, landing.y);
        ChooseNextPoint();
    }

    private void ChooseNextPoint()
    {
        Vector2 landing = Random.insideUnitCircle * (fieldRadius - 0.75f);
        waypoint = new Vector3(landing.x, transform.position.y, landing.y);
        roamClock = 0f;
    }

    private void FixedUpdate()
    {
        roamClock += Time.fixedDeltaTime;

        if (Vector3.Distance(_rb.position, waypoint) < 0.25f || roamClock >= retargetWindow)
            ChooseNextPoint();

        Vector3 step = (waypoint - _rb.position).normalized * patrolSpeed * Time.fixedDeltaTime;
        Vector3 ahead = _rb.position + step;
        ahead.x = Mathf.Clamp(ahead.x, -fieldRadius + 0.5f, fieldRadius - 0.5f);
        ahead.z = Mathf.Clamp(ahead.z, -fieldRadius + 0.5f, fieldRadius - 0.5f);

        _rb.MovePosition(ahead);
    }

    public Vector3 Velocity => _rb.linearVelocity;
}