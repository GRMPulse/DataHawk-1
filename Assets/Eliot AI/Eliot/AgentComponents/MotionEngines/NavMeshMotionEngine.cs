﻿using System;
using System.Collections;
using Eliot.Environment;
using UnityEngine;
#if UNITY_5_5_OR_NEWER
using UnityEngine.AI;
#endif

namespace Eliot.AgentComponents
{
	/// <summary>
	/// Motion Engine that uses Unity's NavMesh to find path.
	/// </summary>
	public class NavMeshMotionEngine : IMotionEngine
	{
		#region FIELDS
		/// Current state of Agent's Motion Component.
		private MotionState State
		{
			get { return _motion.State; }
			set { _motion.State = value; }
		}
		/// Link to Agent's Motion component.
		private Motion _motion;
		/// Animation component of the Agent.
		private AgentAnimation _agentAnimation;
		/// Link to Agent Component of gameObject.
		private Agent _agent;
		/// Link to the Agent's gameObject.
		private GameObject _gameObject;
		/// Link to Transform Component of gameObject.
		private Transform _transform;
		/// Link to NavMesh Component of gameObject.
		private NavMeshAgent _navMeshAgent;
		/// Index of a waypoint from a waypoint group that is being an Agent's target.
		private int _curWaypointIndex;
		/// Last time Agent used energy to move.
		private float _lastWalkCostUpdate;
		/// Last time Agent used energy to run.
		private float _lastRunCostUpdate;
		/// If true, Agent is allowed to dodge.
		private bool _canDodge = true;
		/// If true, Agent is not allowed to move in any way.
		private bool _locked;
		/// Make sure Agent's Motion never misses a target.
		private Transform _defaultTarget;
		/// Time at which Agent started staying at place while patroling.
		private float _patrolStartTime;
		/// Last time Agent's Motion component made a sound.
		private float _lastSoundTime;
		/// Last position picked as destination while moving around.
		private Vector3 _lastMoveAroundDestination;
		/// Action Interface for this Motion instance.
		private MotionActionInterface _actionInterface;
		/// Condition Interface for this Motion instance.
		private MotionConditionInterface _conditionInterface;
		/// Maximum distance at which Agent can pick random target position.
		private const float MaxWanderingAroundDistance = 25f;
		/// Defines the error of calculating if the Agent is at target position already.
		private const float DistanceError = 0.1f;
		#endregion

		#region INTERFACE METHODS IMPLEMENTATION
		/// <summary>
		/// Return the position of current Agent's target.
		/// </summary>
		/// <returns></returns>
		public Vector3 GetTarget()
		{
			return _navMeshAgent.destination;
		}

		/// <summary>
		/// Assign a new target position.
		/// </summary>
		/// <param name="pos"></param>
		public void SetTarget(Vector3 pos)
		{
			_navMeshAgent.SetDestination(pos);
		}

		/// <summary>
		/// Initialize an Agent so that it has all necessary parameters for using the engine.
		/// </summary>
		/// <param name="agent"></param>
		public void Init(Agent agent)
		{
			_motion = agent.Motion;
			_gameObject = agent.gameObject;
			_transform = _gameObject.GetComponent<Transform>();
			_lastMoveAroundDestination = _transform.position;
			_agent = agent;
			_agentAnimation = _agent.AgentAnimation;
			if (_motion.Type == MotionEngine.NavMesh)
			{
				_navMeshAgent = _gameObject.GetComponent<NavMeshAgent>();
				if (_gameObject.GetComponent<NavMeshAgent>())
					_navMeshAgent = _gameObject.GetComponent<NavMeshAgent>();
				else
					_navMeshAgent = _gameObject.AddComponent<NavMeshAgent>();
				_navMeshAgent.speed = _motion.MoveSpeed;
				_navMeshAgent.updateRotation = true;
			}
		}

		/// <summary>
		/// Check if the Agent's motion is currently locked.
		/// </summary>
		/// <returns></returns>
		public bool Locked()
		{
			return _locked;
		}

		/// <summary>
		/// Lock the Agent's Motion component.
		/// </summary>
		public void Lock()
		{
			_locked = true;
			if (_navMeshAgent)
				_navMeshAgent.speed = 0;
		}

		/// <summary>
		/// Unlock the Agent's Motion component.
		/// </summary>
		public void Unlock()
		{
			_locked = false;
		}

		/// <summary>
		/// Rotate Agent towards its target.
		/// </summary>
		/// <param name="targetPosition"></param>
		public void LookAtTarget(Vector3 targetPosition)
		{
			if (!_agent.AgentAnimation.ApplyRootMotion)
			{
				var newDir = Vector3.RotateTowards(_transform.forward,
					targetPosition - _transform.position, _motion.RotationSpeed * Time.deltaTime, 0F);
				_transform.rotation = Quaternion.LookRotation(newDir);
				_transform.eulerAngles = new Vector3(0, _transform.eulerAngles.y, 0);
			}
			else
			{
				var targetV = targetPosition - _transform.position;
				float angle = 0;
#if UNITY_2017_1_OR_NEWER
				angle = Vector3.SignedAngle(_transform.forward, targetV, _transform.up);
#else
				var unsignedangle = Vector3.Angle(targetV, _transform.forward);
				var sign = Mathf.Sign(Vector3.Dot(targetV, _transform.right));
				angle = sign * unsignedangle;
#endif

				var turnAmount = Mathf.Clamp(angle / 180f * _agent.Motion.RotationSpeed, -1f, 1f);
				_agent.AgentAnimation.Animator.SetFloat(_agent.AgentAnimation.Parameters.Turn, turnAmount);
			}
		}

		/// <summary>
		/// Stand and relax playing default animation.
		/// </summary>
		public void Idle()
		{
			if (_locked) return;
			State = MotionState.Idling;
			_motion.Animate(AnimationState.Idling);
			SetSpeed(0);
			if (_motion.Target != _transform.position) _motion.Target = _transform.position;
		}

		/// <summary>
		/// Stand still idling, with different animation from idle one.
		/// </summary>
		public void Stand()
		{
			if (_locked) return;
			State = MotionState.Standing;
			_motion.Animate(AnimationState.Idling);
			SetSpeed(0);
			if (_agent.Target != _transform)
				_agent.Target = _transform;
		}

	    /// <summary>
	    /// Walk to target, playing corresponding animation and audio. Idle if out of energy.
	    /// </summary>
	    public void WalkToTarget()
	    {
		    MoveTowards(_motion.TargetTransform, _motion.MoveSpeed, MotionState.Walking, AnimationState.Walking,
			    0.5f, _motion.MoveCost, Idle, ref _lastWalkCostUpdate, Idle);
		    _motion.PlayAudio(_motion.WalkingStepSounds, _motion.WalkingStepPing);
	    }

	    /// <summary>
	    /// Walk from target, playing corresponding animation and audio. Idle if out of energy.
	    /// </summary>
	    public void WalkFromTarget()
	    {
		    var deltaPos = _transform.position + (_transform.position - _agent.Target.position).normalized;
		    var targetPos = GetSamplePosition(deltaPos, 3f, NavMesh.AllAreas);
		    MoveTowards(targetPos, _motion.MoveSpeed, MotionState.WalkingAway,
			    AnimationState.Walking, 0.5f, _motion.MoveCost, Idle, ref _lastWalkCostUpdate, Idle);
		    _motion.PlayAudio(_motion.WalkingStepSounds, _motion.WalkingStepPing);
	    }

	    /// <summary>
	    /// Run to target, playing corresponding animation and audio. Walk to target if out of energy.
	    /// </summary>
	    public void RunToTarget()
	    {
		    MoveTowards(_motion.TargetTransform, _motion.RunSpeed, MotionState.Running, AnimationState.Running,
			    0.5f, _motion.RunCost, WalkToTarget, ref _lastRunCostUpdate, Idle);
		    _motion.PlayAudio(_motion.RunningStepSounds, _motion.RunningStepPing);
	    }

	    /// <summary>
	    /// Run from target, playing corresponding animation and audio. Walk from target if out of energy.
	    /// </summary>
	    public void RunFromTarget()
	    {
		    var deltaPos = _transform.position + (_transform.position - _agent.Target.position).normalized;
		    var targetPos = GetSamplePosition(deltaPos, 3f, NavMesh.AllAreas);
		    MoveTowards(targetPos, _motion.RunSpeed, MotionState.RunningAway,
			    AnimationState.Running, 0.5f, _motion.RunCost, WalkFromTarget, ref _lastRunCostUpdate, Idle);
		    _motion.PlayAudio(_motion.RunningStepSounds, _motion.RunningStepPing);
	    }

	    /// <summary>
	    /// Get random point on the NavMesh and walk to it.
	    /// </summary>
		public void WanderAround()
		{
			MoveAround(MotionState.Walking, WalkToTarget, randomly: true);
		}

		/// <summary>
	    /// Get random point on the NavMesh and run to it.
	    /// </summary>
	    public void RunAround()
	    {
		    MoveAround(MotionState.Running, RunToTarget, randomly: true);
	    }

		/// <summary>
		/// Set next waypoint as a target and walk to it. Repeat when Agent got to waypoint.
		/// </summary>
		public void WalkAroundWaypoints()
		{
			//For Agents without a WaypointsGroup reference, it is the same as WanderAround
			MoveAround(MotionState.Walking, WalkToTarget, randomly: !_agent.Waypoints);
		}

		/// <summary>
		/// Set next waypoint as a target and run to it. Repeat when Agent got to waypoint.
		/// </summary>
		public void RunAroundWaypoints()
		{
			//For Agents without a WaypointsGroup reference, it is the same as WanderAround
			MoveAround(MotionState.Running, RunToTarget, randomly: !_agent.Waypoints);
		}

	    /// <summary>
	    /// Get random point on the NavMesh and walk to it. Stand for specified amount of time. Walk again.
	    /// </summary>
	    public void Patrol()
	    {
		    Patrol(MotionState.Walking, WalkToTarget, Idle, randomly: true, waitTime: _motion.PatrolTime);
	    }

		/// <summary>
		/// Quick move in certain direction, with a corresponding animation.
		/// </summary>
		/// <param name="direction"></param>
		public void Dodge(string direction = "b")
		{
			if (_locked) return;
			if (!_canDodge) return;

			_canDodge = false;
			_agent.StartCoroutine(DodgeEnum(direction));

			_motion.PlayAudio(_motion.DodgingSounds, 0);
		}

		/// <summary>
		/// Let other Agents push this one around.
		/// </summary>
		/// <param name="pusherPos"></param>
		/// <param name="pushPower"></param>
		public void Push(Vector3 pusherPos, float pushPower)
		{
			var sudoTarget = _transform.position - pusherPos; sudoTarget.y = 0;
			_agent.StartCoroutine(WarpEnum(sudoTarget, pushPower / _motion.Weight, 0.1f));
		}

		#endregion

		#region UTILITY
		/// <summary>
		/// Change speed on all dependent components.
		/// </summary>
		/// <param name="speed"></param>
		private void SetSpeed(float speed)
		{
			if(_navMeshAgent)
				_navMeshAgent.speed = speed;

			if (_agentAnimation.AnimationMode == AnimationMode.Mecanim)
				_agentAnimation.SetSpeed(State);
		}

		/// <summary>
		/// Get position by projecting specified vector onto the walkable surface.
		/// </summary>
		/// <param name="position"></param>
		/// <param name="range"></param>
		/// <param name="areaMask"></param>
		/// <returns></returns>
		private Vector3 GetSamplePosition(Vector3 position, float range, int areaMask)
		{
			NavMeshHit hit;
			NavMesh.SamplePosition(position, out hit, range, areaMask);
			return hit.position;
		}

		/// <summary>
		/// Update Agent's position and animation on the way to a target position.
		/// </summary>
		/// <param name="targetTransform"></param>
		/// <param name="moveSpeed"></param>
		/// <param name="motionState"></param>
		/// <param name="animationState"></param>
		/// <param name="animationFadeSpeed"></param>
		/// <param name="energyCost"></param>
		/// <param name="actionOnNotEnoughEnergy"></param>
		/// <param name="updateTimeVariable"></param>
		/// <param name="stopAction"></param>
		private void MoveTowards(Transform targetTransform, float moveSpeed, MotionState motionState, AnimationState animationState,
			float animationFadeSpeed, int energyCost, Action actionOnNotEnoughEnergy, ref float updateTimeVariable,
			Action stopAction)
		{
			Vector3? targetPosition = null;
			if (targetTransform) targetPosition = targetTransform.position;
			MoveTowards(targetPosition, moveSpeed, motionState, animationState,
				animationFadeSpeed, energyCost, actionOnNotEnoughEnergy, ref updateTimeVariable, stopAction);
		}

		/// <summary>
		/// Update Agent's position and animation on the way to a target position.
		/// </summary>
		/// <param name="targetPosition"></param>
		/// <param name="moveSpeed"></param>
		/// <param name="motionState"></param>
		/// <param name="animationState"></param>
		/// <param name="animationFadeSpeed"></param>
		/// <param name="energyCost"></param>
		/// <param name="actionOnNotEnoughEnergy"></param>
		/// <param name="updateTimeVariable"></param>
		/// <param name="stopAction"></param>
		private void MoveTowards(Vector3? targetPosition, float moveSpeed, MotionState motionState, AnimationState animationState,
			float animationFadeSpeed, int energyCost, Action actionOnNotEnoughEnergy, ref float updateTimeVariable,
			Action stopAction)
		{
			if (_locked) return;
			if (targetPosition == null) return;
			if (Vector3.Distance(_agent.transform.position, targetPosition.Value) <= DistanceError)
			{
				stopAction();
				return;
			}
			State = motionState;
			_motion.Animate(animationState, animationFadeSpeed);
			SetSpeed(moveSpeed);
			if (energyCost == 0)
			{
				targetPosition = GetSamplePosition(targetPosition.Value, 10f, NavMesh.AllAreas);
				if (_motion.Target != targetPosition)
					_motion.Target = targetPosition.Value;
			}
			else
			{
				if (_agent.Resources.EnergyPoints < energyCost) { actionOnNotEnoughEnergy(); return; }
				targetPosition = GetSamplePosition(targetPosition.Value, 10f, NavMesh.AllAreas);
				if (_motion.Target != targetPosition) _motion.Target = targetPosition.Value;
				if (energyCost > 0 && Time.time > updateTimeVariable + 1)
				{
					_agent.Resources.UseEnergy(energyCost);
					updateTimeVariable = Time.time;
				}
			}
		}

		/// <summary>
		/// Set target position to a random point or to the next Waypoint in WaypointsGroup.
		/// </summary>
		/// <param name="moveAction"></param>
		/// <param name="random"></param>
		/// <returns></returns>
		private Vector3 SetTargetPoint(Action moveAction, bool random)
		{
			Vector3 point;
			if(random) RandomPoint(_transform.position, MaxWanderingAroundDistance, out point);
			else NextPoint(out point);
			_agent.Target = _motion.GetDefaultTarget(point);
			moveAction();
			return point;
		}

		/// <summary>
		/// Move to the next Waypoint or pick a random point and walk to it.
		/// Repeat when target position is reached.
		/// </summary>
		/// <param name="state"></param>
		/// <param name="moveAction"></param>
		/// <param name="randomly"></param>
		private void MoveAround(MotionState state, Action moveAction, bool randomly)
		{
			_agent.Target = _motion.GetDefaultTarget(_lastMoveAroundDestination);

			var agentPosition = _agent.transform.position;
			agentPosition.y = 0;
			var targetPosition = _lastMoveAroundDestination;
			targetPosition.y = 0;

			if (randomly && Vector3.Distance(agentPosition, targetPosition) >= MaxWanderingAroundDistance)
			{
				_lastMoveAroundDestination = SetTargetPoint(moveAction, true);
			}

			if (Vector3.Distance(agentPosition, targetPosition) <=
			    (_agent.Waypoints ? _agent.Waypoints.ThresholdDistance : 0.5f))
			{
				if (!randomly && _agent.Waypoints && _agent.ApplyChangesOnWaypoints && _motion.CurrentWaypoint().MakeChangesToAgent)
				{
					_agent.SetWaypointChanges(_motion.CurrentWaypoint());
				}
				_lastMoveAroundDestination = SetTargetPoint(moveAction, randomly);
			}
			else
			{
				moveAction();
				if (State != state)
					_lastMoveAroundDestination = SetTargetPoint(moveAction, randomly);
			}
		}

		/// <summary>
		/// Move to the next Waypoint or pick a random point and walk to it.
		/// Idle for specified amount of time.
		/// Repeat when target position is reached.
		/// </summary>
		/// <param name="state"></param>
		/// <param name="moveAction"></param>
		/// <param name="waitAction"></param>
		/// <param name="randomly"></param>
		/// <param name="waitTime"></param>
		private void Patrol(MotionState state, Action moveAction, Action waitAction, bool randomly, float waitTime)
		{
			if (!_agent.Target)
				SetTargetPoint(moveAction, randomly);
			if (!_agent.Target.GetComponent<Waypoint>())
				_agent.Target = _motion.GetDefaultTarget();

			var agentPosition = _agent.transform.position;
			agentPosition.y = 0;
			var targetPosition = _agent.Target.position;
			targetPosition.y = 0;
			if (Vector3.Distance(agentPosition, targetPosition) <=
			    (_agent.Waypoints ? _agent.Waypoints.ThresholdDistance : 0.5f)
			    || (randomly && Vector3.Distance(agentPosition, targetPosition) >= MaxWanderingAroundDistance))
			{
				if (Time.time <= _patrolStartTime + waitTime)
				{
					waitAction();
				}
				else
				{
					_patrolStartTime = Time.time;
					SetTargetPoint(moveAction, randomly);
				}
			}
			else
			{
				moveAction();
				if (State != state)
					SetTargetPoint(moveAction, randomly);
			}
		}

		/// <summary>
		/// Move Agent's position in a given direction for a certain amount of time.
		/// </summary>
		/// <param name="warpDir"></param>
		/// <param name="warpStrength"></param>
		/// <param name="warpTime"></param>
		/// <returns></returns>
		private IEnumerator WarpEnum(Vector3 warpDir, float warpStrength, float warpTime)
		{
			var finishTime = Time.time + warpTime;
			while (Time.time < finishTime){
				if (_navMeshAgent)
					_navMeshAgent.Warp(_transform.position + warpDir * warpStrength * Time.deltaTime);
				yield return null;
			}
		}

		/// <summary>
		/// Dodge for a certain amount of time.
		/// </summary>
		/// <param name="direction"></param>
		/// <returns></returns>
		private IEnumerator DodgeEnum(string direction)
		{
			yield return new WaitForSeconds(_motion.DodgeDelay);
			_locked = true;
			State = MotionState.Dodging;
			_motion.Animate(AnimationState.Dodging, 0);
			Vector3 dir;
			switch (direction)
			{
				case "f":case"forward": dir = _agent.Target.position - _transform.position; dir.y = 0; break;
				case "r":case"right": dir = _transform.right; dir.y = 0; break;
				case "l":case"left": dir =  -_transform.right; dir.y = 0; break;
				case "random":
				{
					var chance = UnityEngine.Random.Range(0, 100);
					if (chance >= 0 && chance < 25) dir = _transform.forward;
					else if (chance >= 25 && chance < 50) dir = _transform.right;
					else if (chance >= 50 && chance < 75) dir = -_transform.right;
					else dir = _transform.position - _agent.Target.position;
					dir.y = 0;
					break;
				}
				case "left_or_right":
				{
					var chance = UnityEngine.Random.Range(0, 100);
					if (chance >= 0 && chance < 50) dir = _transform.right;
					else dir = -_transform.right;
					dir.y = 0;
					break;
				}
				default: dir = _transform.position - _agent.Target.position; dir.y = 0; break;
			}
			_agent.StartCoroutine(WarpEnum(dir, _motion.DodgeSpeed, _motion.DodgeDuration));
			_locked = false;
			yield return new WaitForSeconds(_motion.DodgeCoolDown);
			_canDodge = true;
		}

		/// <summary>
		/// Return a random position in a specified radius from the given center.
		/// </summary>
		/// <param name="center"></param>
		/// <param name="range"></param>
		/// <param name="result"></param>
		private void RandomPoint(Vector3 center, float range, out Vector3 result)
		{
			NavMeshHit hit;
			bool success;
			if (_agent.Waypoints != null)
			{
				var point = _agent.Waypoints.RandomPoint();
				success = NavMesh.SamplePosition(point, out hit, 1.0f, NavMesh.AllAreas);
			}
			else
			{
				var randomPoint = center + UnityEngine.Random.insideUnitSphere * range;
				success = NavMesh.SamplePosition(randomPoint, out hit, 1.0f, NavMesh.AllAreas);
			}

			result = success ? hit.position : _transform.position;
		}

		/// <summary>
		/// Get next Waypoint in the WaypointsGroup.
		/// </summary>
		/// <param name="result"></param>
		private void NextPoint(out Vector3 result)
		{
			if (_agent.Waypoints != null)
			{
				NavMeshHit hit;
				NavMesh.SamplePosition(_agent.Waypoints.Points[_curWaypointIndex].transform.position, out hit, 1.0f,
					NavMesh.AllAreas);
				result = hit.position;
				if (_curWaypointIndex + 1 >= _agent.Waypoints.Points.Count) _curWaypointIndex = 0;
				else _curWaypointIndex++;
			}
			else result = _transform.position;
		}

		#endregion
	}
}
