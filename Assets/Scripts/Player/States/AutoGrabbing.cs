﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoGrabbing : StateBase<PlayerController>
{
    private float timeTracker = 0f;
    private float grabTime = 0f;

    private Vector3 grabPoint;
    private Vector3 startPosition;
    private Quaternion targetRot;
    private LedgeDetector ledgeDetector = LedgeDetector.Instance;
    private LedgeInfo ledgeInfo;

    public override void ReceiveContext(object context)
    {
        if (!(context is LedgeInfo))
            return;

        ledgeInfo = (LedgeInfo)context;
    }

    public override void OnEnter(PlayerController player)
    {
        if (ledgeInfo == null)
        {
            Debug.LogError("Autograbbing has no ledge info... you need to pass ledge info as context... going to in air");
            player.StateMachine.GoToState<InAir>();
            return;
        }

        player.MinimizeCollider();

        player.Anim.SetBool("isAutoGrabbing", true);
        player.ForceHeadLook = true;

        grabPoint = ledgeInfo.Point - player.transform.forward * player.hangForwardOffset;
        grabPoint.y = ledgeInfo.Point.y - player.hangUpOffset;

        Vector3 calcGrabPoint;

        if (ledgeInfo.Type == LedgeType.Monkey || ledgeInfo.Type == LedgeType.HorPole)
        {
            calcGrabPoint = grabPoint - Vector3.up * 0.14f;
            targetRot = player.transform.rotation;
        }
        else
        {
            calcGrabPoint = ledgeInfo.Point + player.grabUpOffset * Vector3.down - ledgeInfo.Direction * player.grabForwardOffset;
            targetRot = Quaternion.LookRotation(ledgeInfo.Direction);
        }

        startPosition = player.transform.position;

        player.Velocity = UMath.VelocityToReachPoint(player.transform.position,
                            calcGrabPoint,
                            player.runJumpVel,
                            player.gravity,
                            out grabTime);

        // So Lara doesn't do huge upwards jumps or snap when close
        if (grabTime < 0.3f || grabTime > 1.2f)
        {
            grabTime = Mathf.Clamp(grabTime, 0.4f, 1.2f);

            player.Velocity = UMath.VelocityToReachPoint(player.transform.position,
                                calcGrabPoint,
                                player.gravity,
                                grabTime);
        }

        timeTracker = Time.time;
    }

    public override void OnExit(PlayerController player)
    {
        player.MaximizeCollider();

        player.Anim.SetBool("isAutoGrabbing", false);
        player.ForceHeadLook = false;

        ledgeInfo = null;
    }

    public override void Update(PlayerController player)
    {
        player.ApplyGravity(player.gravity);

        player.HeadLookAt = ledgeInfo.Point;

        if (Time.time - timeTracker >= grabTime)
        {
            if (UMath.GetHorizontalMag(player.Velocity) > 1f)
                player.Anim.SetTrigger(HasFeetRoom() ? "DeepGrab" : "Grab");
            else
                player.Anim.SetTrigger("StandGrab");

            player.transform.position = grabPoint;
            player.transform.rotation = targetRot;

            if (ledgeInfo.Type == LedgeType.Free)
                player.StateMachine.GoToState<Freeclimb>();
            else if (ledgeInfo.Type == LedgeType.Monkey)
                player.StateMachine.GoToState<MonkeySwing>();
            else
                player.StateMachine.GoToState<Climbing>();
        }
    }

    public bool HasFeetRoom()
    {
        if (Physics.Raycast(grabPoint, ledgeInfo.Direction, 1f))
            return false;

        if (Physics.Raycast(grabPoint + Vector3.up * 1f, ledgeInfo.Direction, 1f))
            return false;

        return true;
    }
}
