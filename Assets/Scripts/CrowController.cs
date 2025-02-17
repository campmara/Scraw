using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// NOTE: THIS IS CURRENTLY ALL DEBUG CODE, NOT INTENDED FOR SHIPPING. PLEASE DO NOT USE ANY OF
/// THIS IN A SHIPPED GAME, IF THERE EVER IS ONE.
/// 
/// We wrote the quickest and dirtiest possible implementation of crow flight for the purposes of
/// testing and quick iteration. An effort will be made to compress this class and turn it into
/// higher quality systems for managing flying.
/// </summary>
public class CrowController : MonoBehaviour
{
    [SerializeField] private Rigidbody _crowRigidbody;
    [SerializeField] private Transform _headTransform;
    [SerializeField] private Transform _leftHandTransform;
    [SerializeField] private Transform _rightHandTransform;
    [SerializeField] private float _handsAboveHeadThreshold = -0.1f; // y-distance from head y that we start tracking a "flap".
    [SerializeField] private float _extraFlapStrength = 110f;
    [SerializeField] private float _flapForwardCompensation = 0.6f;
    [SerializeField] private float _maxTraversableGroundAngle = 55f;
    [SerializeField] private float _maxSnapToGroundVelocity = 5f;
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private float _glideMinHandDotWithForward = -0.5f;
    [SerializeField] private float _glideMaxHandDotWithForward = 0.5f;
    [SerializeField] private float _glideMinHandDistance = 0.5f;
    [SerializeField] private float _glideForwardScalar = 6.3f;
    [SerializeField] private float _glideTurnScalar = 0.025f;

    [Header("Audio")]
    [SerializeField] private AudioSource _scrawAudioSource;
    [SerializeField] private AudioSource _flapAudioSource;

    [Header("DEBUG Labels")]
    [SerializeField] private GameObject _debugLabelGroup;
    [SerializeField] private TMP_Text _isFlapReadyLabel;
    [SerializeField] private TMP_Text _lastFlapSpeedLabel;
    [SerializeField] private TMP_Text _isWingspanToTheSidesLabel;
    [SerializeField] private TMP_Text _areHandsFarEnoughLabel;
    [SerializeField] private TMP_Text _leftHandAngleLabel;
    [SerializeField] private TMP_Text _rightHandAngleLabel;
    [SerializeField] private TMP_Text _isGroundedLabel;

    private XRInputActions _xrInputActions;

    private bool _isFlapReady = false;
    private Vector3 _lastLeftHandPosition = Vector3.zero;
    private Vector3 _lastRightHandPosition = Vector3.zero;
    private Vector3 _neckPos = Vector3.zero;

    private bool IsOnGround => _groundContactCount > 0 || _crowRigidbody.IsSleeping();
    private int _groundContactCount = 0;
    private float _minDotProductGround;
    private int _physicsStepsSinceLastGrounded = 0;

    private void Start()
    {
        _xrInputActions = new XRInputActions();
        _xrInputActions.Enable();
        _xrInputActions.XRILeftInteraction.XButton.performed += context => ButtonPressed();
        _xrInputActions.XRILeftInteraction.YButton.performed += context => AlignBodyOrientationToHead();
        _xrInputActions.XRIRightInteraction.AButton.performed += context => ButtonPressed();
        _xrInputActions.XRIRightInteraction.BButton.performed += context => AlignBodyOrientationToHead();

        AlignBodyOrientationToHead();
    }

    private void OnValidate()
    {
        _minDotProductGround = Mathf.Cos(_maxTraversableGroundAngle * Mathf.Deg2Rad);
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.BackQuote))
        {
            _debugLabelGroup.SetActive(!_debugLabelGroup.activeSelf);
        }
    }

    private void FixedUpdate()
    {
        _physicsStepsSinceLastGrounded += 1;
        if (IsOnGround || SnapToGround())
        {
            _physicsStepsSinceLastGrounded = 0;
        }

        Vector3 leftHandPos = _leftHandTransform.position;
        Vector3 rightHandPos = _rightHandTransform.position;
        Vector3 headPos = _headTransform.position;
        Vector3 bodyPos = _crowRigidbody.transform.position;

        Vector3 leftHandVelocity = leftHandPos - _lastLeftHandPosition;
        Vector3 rightHandVelocity = rightHandPos - _lastRightHandPosition;

        float flapThreshold = headPos.y + _handsAboveHeadThreshold;
        if (!_isFlapReady && leftHandPos.y >= flapThreshold && rightHandPos.y >= flapThreshold)
        {
            _isFlapReady = true;
        }

        if (_isFlapReady && leftHandPos.y < flapThreshold && rightHandPos.y < flapThreshold)
        {
            float flapSpeed = (leftHandVelocity.magnitude + rightHandVelocity.magnitude) / 2f;
            Vector3 flapDirection = (leftHandVelocity.normalized + rightHandVelocity.normalized) / 2f;
            flapDirection.Normalize();

            // Add an arbitrary forward force to the flap direction, because by default when we
            // wave our arms that downward direction sort of pushes us backward, rather than forward.
            flapDirection -= _crowRigidbody.transform.forward * _flapForwardCompensation;

            _crowRigidbody.AddForce(-flapDirection * flapSpeed * _extraFlapStrength, ForceMode.Impulse);
            _flapAudioSource.Play();

            _isFlapReady = false;
            _lastFlapSpeedLabel.text = $"Last Flap Speed: {flapSpeed}";
        }
        _isFlapReadyLabel.text = _isFlapReady ? "Is Flap Ready? YES" : "Is Flap Ready? NO";

        // Calculate a "neck" position. This is an arbitrary point along the head's y transform
        // determined by the average y-distances of each hand from the y position of the head.
        //
        // It's a rather unintuitive way to figure out where the neck is, but there is a thought
        // behind this. When we go to calculate the tilt values for mid-glide turning later, it
        // helps for the accuracy of those angular values to have a point that lies closer to
        // between the two hands than what the head position would provide. This is also better
        // than calculating the neck position with just a static float scalar value applied to a
        // down vector added to the head position.
        //
        // The shortcoming of this point is that it won't very accurately represent the "neck"
        // when our hands are not out to the sides in flying position, but we don't really care
        // since this value SHOULD ONLY be used in calculating the glide tilt under those
        // ideal conditions anyway.
        float leftHandYDistanceFromHead = leftHandPos.y - headPos.y;
        float rightHandYDistanceFromHead = rightHandPos.y - headPos.y;
        float handAverageYDistanceFromHead = (leftHandYDistanceFromHead + rightHandYDistanceFromHead) / 2f;
        _neckPos = headPos + (_headTransform.up * handAverageYDistanceFromHead);

        float handDistanceSqr = (rightHandPos - leftHandPos).sqrMagnitude;
        bool areHandsFarEnough = handDistanceSqr > (_glideMinHandDistance * _glideMinHandDistance);
        _areHandsFarEnoughLabel.text = areHandsFarEnough ? "Are Hands Far Enough? YES" : "Are Hands Far Enough? NO";

        Vector3 leftHandDirectionFromNeck = leftHandPos - _neckPos;
        Vector3 rightHandDirectionFromNeck = rightHandPos - _neckPos;
        Vector3 forwardOfNeck = _neckPos + _crowRigidbody.transform.forward;
        Vector3 neckForwardNormalized = (forwardOfNeck - _neckPos).normalized;
        float leftDotNeckForward = Vector3.Dot(leftHandDirectionFromNeck.normalized, neckForwardNormalized);
        float rightDotNeckForward = Vector3.Dot(rightHandDirectionFromNeck.normalized, neckForwardNormalized);
        bool isWingspanToTheSides = leftDotNeckForward < _glideMaxHandDotWithForward &&
                                    leftDotNeckForward > _glideMinHandDotWithForward &&
                                    rightDotNeckForward < _glideMaxHandDotWithForward &&
                                    rightDotNeckForward > _glideMinHandDotWithForward;
        _isWingspanToTheSidesLabel.text = isWingspanToTheSides ? "Is Wingspan To The Sides? YES" : "Is Wingspan To The Sides? NO";

        _isGroundedLabel.text = IsOnGround ? "Is Grounded? YES" : "Is Grounded? NO";

        if (areHandsFarEnough && isWingspanToTheSides && !IsOnGround)
        {
            // Higher hand distances give higher speeds favoring people with larger wingspans ;~).
            _crowRigidbody.AddForce(_crowRigidbody.transform.forward * _glideForwardScalar * handDistanceSqr, ForceMode.Force);

            float oppositeGravity = -Physics.gravity.y;
            float slowedGravityForce = (oppositeGravity - 0.2f);
            _crowRigidbody.AddForce(new Vector3(0f, slowedGravityForce, 0f), ForceMode.Acceleration);

            Vector3 rightOfNeck = _neckPos + _crowRigidbody.transform.right;
            Vector3 neckRightNormalized = (rightOfNeck - _neckPos).normalized;
            Vector3 neckLeftNormalized = -neckRightNormalized;

            Vector3 leftArmNormDirFromNeck = leftHandDirectionFromNeck.normalized;
            Vector3 rightArmNormDirFromNeck = rightHandDirectionFromNeck.normalized;

            float leftHandRadians = Mathf.Atan(leftArmNormDirFromNeck.y - neckLeftNormalized.y);
            float leftHandAngle = Mathf.Rad2Deg * leftHandRadians;
            _leftHandAngleLabel.text = $"Left Hand Angle: {leftHandAngle}";

            float rightHandRadians = Mathf.Atan(rightArmNormDirFromNeck.y - neckRightNormalized.y);
            float rightHandAngle = Mathf.Rad2Deg * rightHandRadians;
            _rightHandAngleLabel.text = $"Right Hand Angle: {rightHandAngle}";

            // TODO(mara): Right now, we only utilize the left hand angle to guide the torque force.
            // Either calculate the torque using both angles, or accept just one and stop
            // calculating the other.
            float yTorque = (leftHandAngle) * _glideTurnScalar;
            _crowRigidbody.AddTorque(new Vector3(0f, yTorque, 0f), ForceMode.Force);
        }

        _lastLeftHandPosition = leftHandPos;
        _lastRightHandPosition = rightHandPos;
        _groundContactCount = 0;
    }

    private bool SnapToGround()
    {
        if (_physicsStepsSinceLastGrounded > 1)
        {
            return false;
        }
        float velocityMag = _crowRigidbody.linearVelocity.magnitude;
        if (velocityMag > _maxSnapToGroundVelocity)
        {
            return false;
        }
        if (!UnityEngine.Physics.Raycast(_crowRigidbody.position, -_crowRigidbody.transform.up, out RaycastHit hit, 1f, _groundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        float upDot = Vector3.Dot(_crowRigidbody.transform.up, hit.normal);
        if (upDot < _minDotProductGround)
        {
            return false;
        }

        _groundContactCount = 1;
        float dot = Vector3.Dot(_crowRigidbody.linearVelocity, hit.normal);
        if (dot > 0f)
        {
            _crowRigidbody.linearVelocity = (_crowRigidbody.linearVelocity - hit.normal * dot).normalized * velocityMag;
        }
        return true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        EvaluateCollision(collision);
    }

    private void EvaluateCollision(Collision collision)
    {
        int layer = collision.gameObject.layer;
        for (int i = 0; i < collision.contactCount; ++i)
        {
            Vector3 normal = collision.GetContact(i).normal;
            float upDot = Vector3.Dot(_crowRigidbody.transform.up, normal);
            if (upDot >= _minDotProductGround)
            {
                _groundContactCount += 1;
            }
        }
    }

    private void AlignBodyOrientationToHead()
    {
        Vector3 newCrowEuler = _crowRigidbody.transform.eulerAngles;
        newCrowEuler.y = _headTransform.eulerAngles.y;
        _crowRigidbody.transform.eulerAngles = newCrowEuler;
    }

    private void ButtonPressed()
    {
        _scrawAudioSource.Play();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(_neckPos, 0.025f);

        Vector3 rightOfNeck = (_neckPos + (_crowRigidbody.transform.right));
        Gizmos.DrawLine(_neckPos, rightOfNeck);
    }
}
