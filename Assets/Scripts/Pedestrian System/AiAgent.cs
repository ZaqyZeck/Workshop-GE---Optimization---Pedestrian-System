using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleCity.AI
{
    [RequireComponent(typeof(Animator))]
    public class AiAgent : MonoBehaviour
    {
        public enum AgentState
        {
            Idle,       // Not initialized yet.
            Wandering,  // Moving around inside a park using flocking (boids).
            Traveling   // Following a sidewalk path towards another park.
        }

        public event Action OnDeath;

        Animator animator;

        [Header("Travel")]
        public float speed = 0.2f;
        public float rotationSpeed = 10f;

        [Header("Park wandering (boids)")]
        [SerializeField] private float minWanderSpeed = 0.05f;
        [SerializeField] private float maxWanderSpeed = 0.15f;
        [SerializeField] private float maxSteeringForce = 0.3f;
        [Tooltip("Same-type agents inside this radius are considered flock mates (cohesion / alignment).")]
        [SerializeField] private float neighbourRadius = 0.6f;
        [Tooltip("Distance this agent tries to keep from same-type agents.")]
        [SerializeField] private float desiredSeparation = 0.15f;
        [Tooltip("Agents of a different type inside this radius are avoided.")]
        [SerializeField] private float avoidRadius = 0.35f;
        [Tooltip("Width of the soft zone along the park border that pushes the agent back inside.")]
        [SerializeField] private float boundaryMargin = 0.2f;
        [Tooltip("How fast (degrees / second) the random wander direction changes.")]
        [SerializeField] private float wanderJitter = 180f;
        [Tooltip("Maximum angle (degrees) the wander direction may deviate from the current heading.")]
        [SerializeField] private float maxWanderAngle = 60f;

        [Header("Behaviour weights")]
        [SerializeField] private float separationWeight = 1.5f;
        [SerializeField] private float alignmentWeight = 0.5f;
        [SerializeField] private float cohesionWeight = 1f;
        [SerializeField] private float avoidanceWeight = 2.5f;
        [SerializeField] private float wanderWeight = 0.6f;
        [SerializeField] private float boundaryWeight = 3f;

        [Header("Park switching")]
        [Tooltip("Automatically walk to a park of a different type after staying for a random duration.")]
        [SerializeField] private bool autoChangePark = false;
        [Tooltip("Random range (seconds) an agent stays in a park before switching.")]
        [SerializeField] private Vector2 parkStayDuration = new Vector2(15f, 30f);

        List<Vector3> pathToGo = new List<Vector3>();
        int index = 0;
        Vector3 endPosition;

        public Color pathColor;
        PathVisualizer pathVisualizer;

        Rigidbody rb;

        private bool stop;

        // Boids state.
        Vector3 velocity;
        float wanderAngle;
        float parkStayTimer;
        Rect parkBounds; // XZ bounds of the current park (Rect.y = world Z).

        public AgentState State { get; private set; } = AgentState.Idle;

        /// <summary>Agents with the same type flock together, different types avoid each other.</summary>
        public int AgentType;

        public StructureModel CurrentPark { get; private set; }
        public ParkType CurrentParkType { get; private set; } = ParkType.None;
        /// <summary>Cached so the agent can still leave if its park gets removed.</summary>
        public Vector3Int CurrentParkRoadPosition { get; private set; }
        public StructureModel TargetPark { get; private set; }

        /// <summary>Planar velocity used by the boids of neighbouring agents.</summary>
        public Vector3 Velocity => velocity;

        public bool Stop
        {
            get { return stop; }
            set { 
                stop = value;
                if (stop)
                {
                    rb.linearVelocity = Vector3.zero;
                    animator.SetBool("Walk", false);
                }
                else
                {
                    animator.SetBool("Walk", true);
                }
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
        }

        private void Start()
        {
            pathVisualizer = FindObjectOfType<PathVisualizer>();
            pathColor = UnityEngine.Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f);
        }

        public void ShowPath()
        {
            if (pathVisualizer != null)
                pathVisualizer.ShowPath(pathToGo, this, pathColor);
        }

        /// <summary>
        /// Places the agent inside a park where it starts wandering.
        /// </summary>
        public void InitializeInPark(int agentType, StructureModel park)
        {
            //AgentType = agentType;
            velocity = transform.forward * minWanderSpeed;
            EnterPark(park);
        }

        /// <summary>
        /// Directs this agent to walk to a park of a different type (Green / Red / Yellow).
        /// Returns false if the agent is already traveling or no reachable park was found.
        /// </summary>
        public bool MoveToDifferentPark()
        {
            if (State != AgentState.Wandering || AiDirector.Instance == null)
                return false;

            return AiDirector.Instance.TrySendAgentToDifferentPark(this);
        }

        /// <summary>
        /// Called by AiDirector once a path towards the target park has been found.
        /// </summary>
        internal void StartTravel(StructureModel targetPark, List<Vector3> path)
        {
            if (path == null || path.Count == 0)
                return;

            LeaveCurrentPark();

            TargetPark = targetPark;
            pathToGo = path;
            index = 0;
            endPosition = pathToGo[index];
            State = AgentState.Traveling;
            Stop = false;
        }

        private void EnterPark(StructureModel park)
        {
            CurrentPark = park;
            CurrentParkType = park.ParkType;
            CurrentParkRoadPosition = park.RoadPosition;
            parkBounds = AiDirector.Instance.GetParkBounds(park);
            TargetPark = null;
            pathToGo = new List<Vector3>();
            wanderAngle = 0f;
            ResetParkStayTimer();

            AiDirector.Instance.RegisterAgentInPark(this, park);

            State = AgentState.Wandering;
            Stop = false;
        }

        private void LeaveCurrentPark()
        {
            if (AiDirector.Instance != null)
                AiDirector.Instance.UnregisterAgent(this);

            CurrentPark = null;
        }

        private void ResetParkStayTimer()
        {
            parkStayTimer = UnityEngine.Random.Range(parkStayDuration.x, parkStayDuration.y);
        }

        private void Update()
        {
            switch (State)
            {
                case AgentState.Traveling:
                    if (Stop == false)
                        PerformMovement();
                    break;

                case AgentState.Wandering:
                    Wander(Time.deltaTime);
                    break;
            }
        }

        #region Traveling

        private void PerformMovement()
        {
            if(pathToGo.Count> index)
            {
                float distanceToGo = MoveTheAgent();
                if(distanceToGo < 0.05f)
                {
                    index++;
                    if(index >= pathToGo.Count)
                    {
                        ArriveAtTargetPark();
                        return;
                    }
                    endPosition = pathToGo[index];
                }
            }
        }

        private float MoveTheAgent()
        {
            Vector3 endPositionCorrect = new Vector3(endPosition.x, transform.position.y, endPosition.z);
            //transform.position = Vector3.MoveTowards(transform.position, endPositionCorrect, step);
            rb.linearVelocity = transform.forward * speed;

            var lookDirection = endPositionCorrect - transform.position;
            if (lookDirection.sqrMagnitude > 0.000001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), Time.deltaTime * rotationSpeed);
            return Vector3.Distance(transform.position, endPositionCorrect);
        }

        private void ArriveAtTargetPark()
        {
            if (TargetPark == null)
            {
                // The target park was removed while walking towards it.
                Destroy(gameObject);
                return;
            }

            velocity = transform.forward * minWanderSpeed;
            EnterPark(TargetPark);
        }

        #endregion

        #region Wandering (boids)

        private void Wander(float deltaTime)
        {
            Vector3 position = transform.position;

            Vector3 steering = CalculateFlockingForce(position)
                + CalculateWanderForce(deltaTime) * wanderWeight
                + CalculateBoundaryForce(position) * boundaryWeight;

            velocity += steering * deltaTime;
            velocity.y = 0f;

            // Keep the speed between min and max so the walk animation always matches.
            float currentSpeed = velocity.magnitude;
            if (currentSpeed > maxWanderSpeed)
            {
                velocity = velocity / currentSpeed * maxWanderSpeed;
            }
            else if (currentSpeed < minWanderSpeed)
            {
                Vector3 direction = currentSpeed > 0.0001f ? velocity / currentSpeed : transform.forward;
                velocity = direction * minWanderSpeed;
            }

            KeepInsidePark(position);

            rb.linearVelocity = velocity;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(velocity), deltaTime * rotationSpeed);

            //if (autoChangePark)
            //{
            //    parkStayTimer -= deltaTime;
            //    if (parkStayTimer <= 0f && MoveToDifferentPark() == false)
            //    {
            //        // No reachable park of another type right now, try again later.
            //        ResetParkStayTimer();
            //    }
            //}
        }

        /// <summary>
        /// Separation + alignment + cohesion with same-type agents, avoidance of other types.
        /// Only agents in the same park are checked, which keeps the neighbour search small.
        /// </summary>
        private Vector3 CalculateFlockingForce(Vector3 position)
        {
            var parkMates = AiDirector.Instance.GetParkMates(this);

            float neighbourRadiusSqr = neighbourRadius * neighbourRadius;
            float separationSqr = desiredSeparation * desiredSeparation;
            float avoidRadiusSqr = avoidRadius * avoidRadius;

            Vector3 separation = Vector3.zero;
            Vector3 avoidance = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 center = Vector3.zero;
            int flockCount = 0;

            for (int i = 0; i < parkMates.Count; i++)
            {
                AiAgent other = parkMates[i];
                if (other == this)
                    continue;

                Vector3 offset = position - other.transform.position;
                offset.y = 0f;
                float distanceSqr = offset.sqrMagnitude;

                if (other.AgentType == AgentType)
                {
                    if (distanceSqr > neighbourRadiusSqr)
                        continue;

                    flockCount++;
                    center += other.transform.position;
                    alignment += other.Velocity;

                    // Push away harder the closer a flock mate is than the desired distance.
                    if (distanceSqr < separationSqr && distanceSqr > 0.000001f)
                    {
                        float distance = Mathf.Sqrt(distanceSqr);
                        separation += offset / distance * (1f - distance / desiredSeparation);
                    }
                }
                else if (distanceSqr < avoidRadiusSqr && distanceSqr > 0.000001f)
                {
                    float distance = Mathf.Sqrt(distanceSqr);
                    avoidance += offset / distance * (1f - distance / avoidRadius);
                }
            }

            Vector3 force = Vector3.zero;

            force += Steer(separation, Mathf.Clamp01(separation.magnitude)) * separationWeight;
            force += Steer(avoidance, Mathf.Clamp01(avoidance.magnitude)) * avoidanceWeight;

            if (flockCount > 0)
            {
                force += Steer(alignment / flockCount, 1f) * alignmentWeight;

                // Only pull towards the group when farther away than the desired distance.
                Vector3 toCenter = center / flockCount - position;
                toCenter.y = 0f;
                float cohesionRange = Mathf.Max(0.0001f, neighbourRadius - desiredSeparation);
                float cohesionStrength = Mathf.Clamp01((toCenter.magnitude - desiredSeparation) / cohesionRange);
                force += Steer(toCenter, cohesionStrength) * cohesionWeight;
            }

            return force;
        }

        /// <summary>
        /// Random meandering: steers towards the current heading rotated by a slowly drifting angle.
        /// </summary>
        private Vector3 CalculateWanderForce(float deltaTime)
        {
            wanderAngle += UnityEngine.Random.Range(-1f, 1f) * wanderJitter * deltaTime;
            wanderAngle = Mathf.Clamp(wanderAngle, -maxWanderAngle, maxWanderAngle);

            Vector3 heading = velocity.sqrMagnitude > 0.000001f ? velocity.normalized : transform.forward;
            Vector3 wanderDirection = Quaternion.Euler(0f, wanderAngle, 0f) * heading;
            return Steer(wanderDirection, 1f);
        }

        /// <summary>
        /// Soft force pushing the agent back inside when it gets close to the park border.
        /// </summary>
        private Vector3 CalculateBoundaryForce(Vector3 position)
        {
            float margin = Mathf.Max(0.0001f, boundaryMargin);
            Vector3 push = Vector3.zero;

            if (position.x < parkBounds.xMin + margin)
                push.x = (parkBounds.xMin + margin - position.x) / margin;
            else if (position.x > parkBounds.xMax - margin)
                push.x = -(position.x - (parkBounds.xMax - margin)) / margin;

            if (position.z < parkBounds.yMin + margin)
                push.z = (parkBounds.yMin + margin - position.z) / margin;
            else if (position.z > parkBounds.yMax - margin)
                push.z = -(position.z - (parkBounds.yMax - margin)) / margin;

            return Steer(push, Mathf.Clamp01(push.magnitude));
        }

        /// <summary>
        /// Hard limit: never leave the park while wandering.
        /// </summary>
        private void KeepInsidePark(Vector3 position)
        {
            Vector3 clamped = position;
            if (position.x < parkBounds.xMin) { clamped.x = parkBounds.xMin; velocity.x = Mathf.Abs(velocity.x); }
            else if (position.x > parkBounds.xMax) { clamped.x = parkBounds.xMax; velocity.x = -Mathf.Abs(velocity.x); }

            if (position.z < parkBounds.yMin) { clamped.z = parkBounds.yMin; velocity.z = Mathf.Abs(velocity.z); }
            else if (position.z > parkBounds.yMax) { clamped.z = parkBounds.yMax; velocity.z = -Mathf.Abs(velocity.z); }

            if (clamped != position)
                transform.position = clamped;
        }

        /// <summary>
        /// Reynolds steering: (desired velocity - current velocity), limited by the max steering force.
        /// </summary>
        private Vector3 Steer(Vector3 direction, float strength)
        {
            direction.y = 0f;
            if (strength <= 0f || direction.sqrMagnitude < 0.00000001f)
                return Vector3.zero;

            Vector3 desiredVelocity = direction.normalized * maxWanderSpeed;
            return Vector3.ClampMagnitude(desiredVelocity - velocity, maxSteeringForce) * strength;
        }

        #endregion

        private void OnDestroy()
        {
            if (AiDirector.Instance != null)
                AiDirector.Instance.UnregisterAgent(this);

            OnDeath?.Invoke();
        }
    }
}
