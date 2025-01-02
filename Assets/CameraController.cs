using UnityEngine;

public class CameraController : MonoBehaviour
{

    public Transform target;
    public float rotationSpeed = 2.0f;
    public float distance = 2.0f;

    private float currentAngle = 0.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (target == null)
        {
            Debug.LogError("Target is not set in CameraController");
        }

        UpdateCameraPosition();
    }

    void UpdateCameraPosition()
    {
        float x = target.position.x + distance * Mathf.Cos(currentAngle + Mathf.Deg2Rad);
        float z = target.position.z + distance * Mathf.Sin(currentAngle + Mathf.Deg2Rad);

        transform.position = new Vector3(x, target.position.y + 10.0f, z);
        transform.LookAt(target.position);
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        currentAngle += horizontal * rotationSpeed * Time.deltaTime;
        UpdateCameraPosition();
    }
}
