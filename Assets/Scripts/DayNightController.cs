using System;
using UnityEngine;

public class DayNightController : MonoBehaviour
{
    public const int SECONDS_IN_DAY = 86400;
    public const float SUN_UPDATE_INTERVAL_SECONDS = 3f;

    [Header("Sun and Moon Behavior")]
    [SerializeField] private Transform _sunTransform;
    [SerializeField] private Transform _moonTransform;
    [SerializeField] private float _moonMoveSpeed = 10f;

    private float _sunUpdateTimer = 0f;

    private void Start()
    {
        CalculateAndUpdateSunPosition();
        _sunUpdateTimer = 0f;
    }

    private void Update()
    {
        // The sun follows the current system time.
        _sunUpdateTimer += Time.deltaTime;
        if (_sunUpdateTimer >= SUN_UPDATE_INTERVAL_SECONDS)
        {
            CalculateAndUpdateSunPosition();
            _sunUpdateTimer = 0f;
        }

        // The moon moves at a constant rate in a specific direction with some variance applied.
        Vector3 rotationEuler = Vector3.zero;
        rotationEuler.x = _moonMoveSpeed * Time.deltaTime;

        _moonTransform.Rotate(rotationEuler, Space.World);

        // Clamp the angles to keep them in a low float range.
        Vector3 clampedMoonEuler = _moonTransform.eulerAngles;
        clampedMoonEuler.x = AngleUtils.ClampAngle(clampedMoonEuler.x);
        clampedMoonEuler.y = AngleUtils.ClampAngle(clampedMoonEuler.y);
        clampedMoonEuler.z = 0f;
        _moonTransform.eulerAngles = clampedMoonEuler;
    }

    private void CalculateAndUpdateSunPosition()
    {
        double totalSecondsElapsedToday = DateTime.Now.TimeOfDay.TotalSeconds;
        float sphericalCoefficientDeg = -Mathf.Rad2Deg * ((2f * Mathf.PI) + (Mathf.PI / 2f));
        float sunAngle = sphericalCoefficientDeg * ((float)totalSecondsElapsedToday / (float)SECONDS_IN_DAY);
        sunAngle = AngleUtils.ClampAngle(sunAngle);
        _sunTransform.eulerAngles = new Vector3(sunAngle, -90f, 0f);
    }
}
