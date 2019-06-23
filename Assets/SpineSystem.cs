﻿using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

struct SpineJob: IJobProcessComponentData<Spine, Position, Rotation>
{
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> positions;

    [NativeDisableParallelForRestriction]
    public NativeArray<Quaternion> rotations;
    
    public float dT;
    public float bondDamping;
    public float angularBondDamping;

    public void Execute(ref Spine s, ref Position p, ref Rotation r)
    {
        // Is it the root of a spine?
        if (s.parent == -1)
        {
            return;
        }
        Vector3 wantedPosition = positions[s.parent] + rotations[s.parent] * s.offset;
        p.Value = Vector3.Lerp(p.Value, wantedPosition, bondDamping * dT);

        Vector3 myPos = p.Value;
        Quaternion wantedQuaternion = Quaternion.LookRotation(positions[s.parent] - myPos);
        r.Value = Quaternion.Slerp(r.Value, wantedQuaternion, angularBondDamping * dT);
    }
}

[BurstCompile]
struct CopyTransformsToSpineJob : IJobProcessComponentData<Position, Rotation, Spine>
{
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> positions;
    [NativeDisableParallelForRestriction]
    public NativeArray<Quaternion> rotations;

    public void Execute(ref Position p, ref Rotation r, ref Spine s)
    {
        positions[s.spineId] = p.Value;
        rotations[s.spineId] = r.Value;
    }

}

[BurstCompile]
struct CopyTransformsFromSpineJob : IJobProcessComponentData<Position, Rotation, Spine>
{
    [NativeDisableParallelForRestriction]
    public NativeArray<Vector3> positions;

    [NativeDisableParallelForRestriction]
    public NativeArray<Quaternion> rotations;

    public void Execute(ref Position p, ref Rotation r, ref Spine s)
    {
        p.Value = positions[s.spineId];
        r.Value = rotations[s.spineId];
    }
}

public class SpineSystem : JobComponentSystem
{
    BoidBootstrap bootstrap;

    public NativeArray<Vector3> positions;
    public NativeArray<Quaternion> rotations;

    public const int MAX_SPINES = 10000;
    public int numSpines = 0;

    protected override void OnCreateManager()
    {
        base.OnCreateManager();

        bootstrap = GameObject.FindObjectOfType<BoidBootstrap>();

        positions = new NativeArray<Vector3>(MAX_SPINES, Allocator.Persistent);
        rotations = new NativeArray<Quaternion>(MAX_SPINES, Allocator.Persistent);
        numSpines = 0;

    }
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var ctj = new CopyTransformsToSpineJob()
        {
            positions = this.positions,
            rotations = this.rotations
        };

        var ctjHandle = ctj.Schedule(this, inputDeps);

        var spineJob = new SpineJob()
        {
            angularBondDamping = bootstrap.angularDamping,
            bondDamping = bootstrap.bondDamping,
            dT = Time.deltaTime            
        };
        var spineHandle = spineJob.Schedule(this, ctjHandle);

        var cfj = new CopyTransformsFromSpineJob()
        {
            positions = this.positions,
            rotations = this.rotations
        };
        return cfj.Schedule(this, spineHandle);
    }

}