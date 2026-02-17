
namespace BodySim.Tests;
public class SkeletalSystemTests
{
    private BodyResourcePool CreatePoolWithResources()
    {
        var pool = new BodyResourcePool();
        pool.AddResource(BodyResourceType.Calcium, 100);
        pool.AddResource(BodyResourceType.Glucose, 100);
        pool.AddResource(BodyResourceType.Water, 100);
        return pool;
    }

    [Fact]
    public void DamageTaken_ReduceHealth()
    {
        var ResourcePool = new BodyResourcePool();
        var EventHub = new EventHub();
        var skeletalSystem = new SkeletalSystem(ResourcePool, EventHub);

        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        skeletalSystem.HandleMessage(new DamageEvent(BodyPartType.Head, 10));
        Assert.Equal(90, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Test damage Event trough EventHub
        EventHub.Emit(new DamageEvent(BodyPartType.Head, 10));
        Assert.Single(skeletalSystem.EventQueue); 
        skeletalSystem.Update();
        Assert.Equal(80, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Damage taken should not go below 0
        skeletalSystem.HandleMessage(new DamageEvent(BodyPartType.Head, 100));
        Assert.Equal(0, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void HealTaken_IncreaseHealth()
    {
        var ResourcePool = new BodyResourcePool();
        var EventHub = new EventHub();
        var skeletalSystem = new SkeletalSystem(ResourcePool, EventHub);

        // set health to 80
        skeletalSystem.HandleMessage(new DamageEvent(BodyPartType.Head, 20));

        Assert.Equal(80, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        skeletalSystem.HandleMessage(new HealEvent(BodyPartType.Head, 10));
        Assert.Equal(90, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Test heal Event trough EventHub
        EventHub.Emit(new HealEvent(BodyPartType.Head, 10));
        Assert.Single(skeletalSystem.EventQueue); 
        skeletalSystem.Update();
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);

        // Heal taken should not go above 100
        skeletalSystem.HandleMessage(new HealEvent(BodyPartType.Head, 100));
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void PropagateDamage_ReduceHealth()
    {
        var ResourcePool = new BodyResourcePool();
        var EventHub = new EventHub();
        var skeletalSystem = new SkeletalSystem(ResourcePool, EventHub);

        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.Neck)?.GetComponent(BodyComponentType.Health)?.Current);

        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.LeftShoulder)?.GetComponent(BodyComponentType.Health)?.Current);
        Assert.Equal(100, skeletalSystem.GetNode(BodyPartType.LeftHand)?.GetComponent(BodyComponentType.Health)?.Current);

        skeletalSystem.HandleMessage(new PropagateEffectEvent(BodyPartType.Head, new ImpactEffect(10)));
        Assert.Equal(90, skeletalSystem.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Health)?.Current);
        Assert.Equal(93, skeletalSystem.GetNode(BodyPartType.Neck)?.GetComponent(BodyComponentType.Health)?.Current);
    }

    [Fact]
    public void Damage_DegradesBoneIntegrity()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        var integrityBefore = skeletal.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Integrity)?.Current;
        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 20));
        var integrityAfter = skeletal.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Integrity)?.Current;

        Assert.Equal(100, integrityBefore);
        Assert.Equal(90, integrityAfter); // 20 * 0.5 = 10 integrity lost
    }

    [Fact]
    public void Damage_IncreasesCalciumDemand()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        var boneNode = skeletal.GetNode(BodyPartType.Head) as BoneNode;
        Assert.NotNull(boneNode);
        Assert.Equal(0, boneNode.CalciumHealingDemand);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 20));

        Assert.Equal(2f, boneNode.CalciumHealingDemand); // 20 * 0.1 = 2
        Assert.Equal(2f, boneNode.ResourceNeeds[BodyResourceType.Calcium]);
    }

    [Fact]
    public void FullHeal_ResetsCalciumDemand()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 20));
        var boneNode = skeletal.GetNode(BodyPartType.Head) as BoneNode;
        Assert.NotNull(boneNode);
        Assert.True(boneNode.CalciumHealingDemand > 0);

        // Heal fully
        skeletal.HandleMessage(new HealEvent(BodyPartType.Head, 20));
        Assert.Equal(0, boneNode.CalciumHealingDemand);
    }

    [Fact]
    public void Fracture_DisablesNode()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Deal lethal damage to head
        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 100));

        var boneNode = skeletal.GetNode(BodyPartType.Head) as BoneNode;
        Assert.NotNull(boneNode);
        Assert.True(boneNode.IsFractured);
        Assert.True(boneNode.Status.HasFlag(SystemNodeStatus.Disabled));
    }

    [Fact]
    public void Fracture_StopsRegeneration()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 100));
        var boneNode = skeletal.GetNode(BodyPartType.Head) as BoneNode;
        Assert.NotNull(boneNode);
        Assert.Equal(0, boneNode.GetComponent(BodyComponentType.Health)?.RegenRate);
    }

    [Fact]
    public void FractureEvent_ThroughEventHub()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        hub.Emit(new FractureEvent(BodyPartType.LeftHand));
        skeletal.Update();

        var boneNode = skeletal.GetNode(BodyPartType.LeftHand) as BoneNode;
        Assert.NotNull(boneNode);
        Assert.True(boneNode.IsFractured);
        Assert.True(boneNode.Status.HasFlag(SystemNodeStatus.Disabled));
    }

    [Fact]
    public void BoneSet_RestoresNodeAndEnablesHealing()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Fracture then set the bone
        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 100));
        var boneNode = skeletal.GetNode(BodyPartType.LeftHand) as BoneNode;
        Assert.NotNull(boneNode);
        Assert.True(boneNode.IsFractured);

        skeletal.HandleMessage(new BoneSetEvent(BodyPartType.LeftHand));
        Assert.False(boneNode.IsFractured);
        Assert.True(boneNode.Status.HasFlag(SystemNodeStatus.Healthy));
        Assert.True(boneNode.GetComponent(BodyComponentType.Health)?.RegenRate > 0);
        Assert.Equal(3f, boneNode.CalciumHealingDemand); // Increased for repair
    }

    [Fact]
    public void WeightBearing_Fracture_DisablesDownstream()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Fracture left thigh (weight-bearing)
        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftThigh, 100));

        // Left leg and foot should be disabled
        Assert.True(skeletal.GetNode(BodyPartType.LeftLeg)?.Status.HasFlag(SystemNodeStatus.Disabled));
        Assert.True(skeletal.GetNode(BodyPartType.LeftFoot)?.Status.HasFlag(SystemNodeStatus.Disabled));

        // Right side should be unaffected
        Assert.True(skeletal.GetNode(BodyPartType.RightThigh)?.Status.HasFlag(SystemNodeStatus.Healthy));
    }

    [Fact]
    public void WeightBearing_BoneSet_ReenablesDownstream()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Fracture then set left thigh
        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftThigh, 100));
        Assert.True(skeletal.GetNode(BodyPartType.LeftLeg)?.Status.HasFlag(SystemNodeStatus.Disabled));

        skeletal.HandleMessage(new BoneSetEvent(BodyPartType.LeftThigh));

        Assert.True(skeletal.GetNode(BodyPartType.LeftLeg)?.Status.HasFlag(SystemNodeStatus.Healthy));
        Assert.True(skeletal.GetNode(BodyPartType.LeftFoot)?.Status.HasFlag(SystemNodeStatus.Healthy));
    }

    [Fact]
    public void NonWeightBearing_Fracture_DoesNotDisableDownstream()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Fracture left shoulder (not weight-bearing)
        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftShoulder, 100));

        // Downstream arm parts should remain healthy
        Assert.True(skeletal.GetNode(BodyPartType.LeftUpperArm)?.Status.HasFlag(SystemNodeStatus.Healthy));
        Assert.True(skeletal.GetNode(BodyPartType.LeftForearm)?.Status.HasFlag(SystemNodeStatus.Healthy));
    }

    [Fact]
    public void MarrowBone_ProducesBlood()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Pelvis has marrow
        var pelvis = skeletal.GetNode(BodyPartType.Pelvis) as BoneNode;
        Assert.NotNull(pelvis);
        Assert.True(pelvis.HasMarrow);
        Assert.True(pelvis.ResourceProduction.ContainsKey(BodyResourceType.Blood));
        Assert.True(pelvis.ResourceProduction[BodyResourceType.Blood] > 0);
    }

    [Fact]
    public void MarrowBone_Fracture_StopsBloodProduction()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Pelvis, 100));
        var pelvis = skeletal.GetNode(BodyPartType.Pelvis) as BoneNode;
        Assert.NotNull(pelvis);
        Assert.Equal(0, pelvis.ResourceProduction[BodyResourceType.Blood]);
    }

    [Fact]
    public void MarrowBone_BoneSet_RestoresReducedBloodProduction()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Pelvis, 100));
        skeletal.HandleMessage(new BoneSetEvent(BodyPartType.Pelvis));

        var pelvis = skeletal.GetNode(BodyPartType.Pelvis) as BoneNode;
        Assert.NotNull(pelvis);
        Assert.Equal(0.25f, pelvis.ResourceProduction[BodyResourceType.Blood]); // Reduced rate
    }

    [Fact]
    public void AllBoneNodesHave_BoneDensity_And_Integrity()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        foreach (BodyPartType partType in Enum.GetValues<BodyPartType>())
        {
            var node = skeletal.GetNode(partType);
            Assert.NotNull(node);
            Assert.True(node.HasComponent(BodyComponentType.Health));
            Assert.True(node.HasComponent(BodyComponentType.BoneDensity));
            Assert.True(node.HasComponent(BodyComponentType.Integrity));
        }
    }

    [Fact]
    public void GetFractureCount_ReturnsCorrectCount()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        Assert.Equal(0, skeletal.GetFractureCount());

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 100));
        Assert.Equal(1, skeletal.GetFractureCount());

        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftHand, 100));
        Assert.Equal(2, skeletal.GetFractureCount());
    }

    [Fact]
    public void GetFracturedParts_ReturnsCorrectParts()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 100));
        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftFoot, 100));

        var fractured = skeletal.GetFracturedParts();
        Assert.Contains(BodyPartType.Head, fractured);
        Assert.Contains(BodyPartType.LeftFoot, fractured);
        Assert.Equal(2, fractured.Count);
    }

    [Fact]
    public void GetOverallIntegrity_DecreasesOnDamage()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        float before = skeletal.GetOverallIntegrity();
        Assert.Equal(100f, before);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 50));
        float after = skeletal.GetOverallIntegrity();
        Assert.True(after < before);
    }

    [Fact]
    public void DoubleFracture_IsIdempotent()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 100));
        skeletal.HandleMessage(new FractureEvent(BodyPartType.Head)); // Already fractured

        Assert.Equal(1, skeletal.GetFractureCount());
    }

    [Fact]
    public void MetabolicUpdate_ConsumesResources()
    {
        var pool = CreatePoolWithResources();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        float glucoseBefore = pool.GetResource(BodyResourceType.Glucose);
        skeletal.MetabolicUpdate();
        float glucoseAfter = pool.GetResource(BodyResourceType.Glucose);

        Assert.True(glucoseAfter < glucoseBefore);
    }

    [Fact]
    public void MetabolicUpdate_DetectsFractureFromZeroHealth()
    {
        var pool = CreatePoolWithResources();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Manually zero out health without going through HandleDamage
        var node = skeletal.GetNode(BodyPartType.Head);
        Assert.NotNull(node);
        node.GetComponent(BodyComponentType.Health)?.Decrease(100);

        skeletal.MetabolicUpdate();

        var boneNode = node as BoneNode;
        Assert.NotNull(boneNode);
        Assert.True(boneNode.IsFractured);
    }

    [Fact]
    public void Heal_RestoresSomeIntegrity()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        skeletal.HandleMessage(new DamageEvent(BodyPartType.Head, 40));
        var integrityAfterDamage = skeletal.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Integrity)?.Current;

        skeletal.HandleMessage(new HealEvent(BodyPartType.Head, 40));
        var integrityAfterHeal = skeletal.GetNode(BodyPartType.Head)?.GetComponent(BodyComponentType.Integrity)?.Current;

        Assert.NotNull(integrityAfterDamage);
        Assert.NotNull(integrityAfterHeal);
        Assert.True(integrityAfterHeal > integrityAfterDamage); // Heal restores 30% of heal value to integrity
    }

    [Fact]
    public void WeightBearing_BoneSet_DoesNotReenableFracturedChild()
    {
        var pool = new BodyResourcePool();
        var hub = new EventHub();
        var skeletal = new SkeletalSystem(pool, hub);

        // Fracture both thigh and leg
        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftThigh, 100));
        skeletal.HandleMessage(new DamageEvent(BodyPartType.LeftLeg, 100));

        // Set the thigh
        skeletal.HandleMessage(new BoneSetEvent(BodyPartType.LeftThigh));

        // Leg should still be disabled (it's fractured itself)
        var leg = skeletal.GetNode(BodyPartType.LeftLeg) as BoneNode;
        Assert.NotNull(leg);
        Assert.True(leg.IsFractured);
        Assert.True(leg.Status.HasFlag(SystemNodeStatus.Disabled));

        // Foot should be re-enabled (not fractured, just downstream)
        Assert.True(skeletal.GetNode(BodyPartType.LeftFoot)?.Status.HasFlag(SystemNodeStatus.Healthy));
    }
}
