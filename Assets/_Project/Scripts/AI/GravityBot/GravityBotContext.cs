using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FailSafeZero.Interactions;

namespace FailSafeZero.AI.Gravity
{
    /// <summary>
    /// The bot's "blackboard": everything the brain knows about the world this tick.
    /// Sensors (GravityBot.Sense) write it; actions only read it. Keeping perception
    /// apart from decisions means actions never repeat expensive searches.
    /// </summary>
    public class GravityBotContext
    {
        public GravityBot Bot;
        public GravityHands Hands;
        public NavMeshAgent Agent;

        // What it knows about the player
        public Transform Player;
        public FirstPersonController PlayerController;
        public bool CanSeePlayer;
        public float DistanceToPlayer = float.MaxValue;
        public Vector3 LastKnownPlayerPosition;
        public bool HasLastKnownPosition;
        public float TimeSinceSeen = 999f;
        public bool PlayerIsFloating;

        // Its own state (0..1)
        public float Energy01 = 1f;
        public float Health01 = 1f;
        public float TimeSinceDamaged = 999f;

        // The world around it
        public readonly List<Throwable> NearbyThrowables = new List<Throwable>();
        public float Now;
    }
}
