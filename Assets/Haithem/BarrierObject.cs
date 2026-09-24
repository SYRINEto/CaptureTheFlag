using UnityEngine;

public class BarrierObject : MonoBehaviour
{
    [SerializeField] private float fieldRadius = 9f;
    [SerializeField] private float safeMargin = 1.5f;
    [SerializeField] private LayerMask solidMask;
    [SerializeField] private float clearanceProbe = 1f;

    private Rigidbody _rb;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            Debug.LogError($"{gameObject.name}: a Rigidbody is required on this object.");
            enabled = false;
        }
    }

    public void ResetBarrier(Collider zoneCollider = null, float zonePadding = 1f)
    {
        Vector3 spot = Vector3.zero;
        int rolls = 0;
        bool zoneHit;

        do
        {
            spot = new Vector3(
                Random.Range(-fieldRadius + safeMargin, fieldRadius - safeMargin),
                transform.position.y,
                Random.Range(-fieldRadius + safeMargin, fieldRadius - safeMargin)
            );
            rolls++;

            zoneHit = false;
            if (zoneCollider != null)
            {
                Bounds zone = zoneCollider.bounds;
                zone.Expand(zonePadding);
                zoneHit = zone.Contains(spot);
            }
        }
        while ((Physics.CheckSphere(spot, clearanceProbe, solidMask) || zoneHit) && rolls < 30);

        _rb.position = spot;

        _rb.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
}