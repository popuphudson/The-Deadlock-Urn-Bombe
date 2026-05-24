using System.Numerics;
using DeadworksManaged.Api;
using DeadworksManaged.Api.Sounds;

namespace TheUrnMan;

public class TheUrnMan : DeadworksPluginBase {
    public override string Name => "TheUrnMan";

    private readonly Vector3[] _bombPoses = [
        new(-431.0625f, -15.3125f, 256.0625f),
        new(22.125f, 19.90625f, 256.03125f),
        new(470.375f, 26.625f, 256.03125f),
        new(794.625f, -769.625f, 217f),
        new(-1054.6562f, -326.5625f, 24.0625f),
        new(-1076.75f, 298.96875f, 104.0625f),
        new(-832.59375f, 828.40625f, 183.21875f),
        new(-41.65625f, 1071.625f, 416.0625f),
        new(1801.7188f, 1384.7188f, 168f),
        new(1202.0312f, 656.4375f, 296f),
        new(1674.125f, 316.8125f, 296f),
        new(2162.5938f, 224.90625f, 296f),
        new(1604.6875f, -243.71875f, 424f),
        new(2148.3438f, -597.5f, 424.03125f),
        new(817.375f, -641.25f, 424.0625f),
        new(47.84375f, -1105.375f, 416.03125f),
        new(-536.53125f, -952.5f, 436.9375f),
        new(-1709.8438f, -1338.4062f, 168f),
        new(-1076.25f, -659f, 296.03125f),
        new(-1752.4062f, -270.5f, 296.03125f),
        new(-2220.47f, 430.94f, 411.28f),
        new(0, -410, 261.31f),
        new(0, 410, 261.31f)
    ];

    private bool _flickSwitch = false;
    private bool _nuked = false;

    public override void OnStartupServer() {
        ConVar.Find("citadel_crate_respawn_interval")?.SetFloat(120);
        ConVar.Find("citadel_crate_spawn_initial_delay")?.SetFloat(60);
    }

    public override void OnPrecacheResources() {
        Precache.AddResource("particles/abilities/bebop/bebop_sticky_bomb_proj.vpcf");
        Precache.AddResource("particles/abilities/bebop/bebop_sticky_bomb_explode.vpcf");
        Precache.AddResource("particles/explosion.vpcf");
        Precache.AddResource("particles/mushroom.vpcf");
        Precache.AddResource("particles/urn_explosion.vpcf");
        Precache.AddResource("particles/urn_nuke.vpcf");
    }

    public override void OnLoad(bool __isReload) {
        Console.WriteLine("We say goodbye moon men!");
    }

    public override void OnUnload() {
        Console.WriteLine("We say goodbye moon men! We say good bye!");
    }

    public override void OnGameFrame(bool __simulating, bool __firstTick, bool __lastTick) {
        _flickSwitch = false;
        _nuked = false;
    }

    [GameEventHandler("ability_added")]
    public HookResult AbilityAdded(AbilityAddedEvent __event) {
        var ability = __event.Ability;
        if (ability == null) return HookResult.Continue;
        if (ability.Name == "ability_golden_idol") {
            _flickSwitch = true;
            Console.WriteLine("Golden Earn Given");
        }
        return HookResult.Continue;
    }
    
    [GameEventHandler("ability_removed")]
    public HookResult AbilityRemoved(AbilityRemovedEvent __event) {
        var ability = __event.Ability;
        if (ability == null) return HookResult.Continue;
        if (ability.Name == "ability_golden_idol" && _flickSwitch && !_nuked) {
            var user = __event.Userid;
            if (user == null) return HookResult.Continue;
            Console.WriteLine($"DEPOSITED TO TEAM: {user.TeamNum}");
            HandleUrnDepositForTeam(user.TeamNum);
            _nuked = true;
        }
        return HookResult.Continue;
    }

    private void HandleUrnDepositForTeam(int __teamNum) {
        BigNuke(__teamNum);
        CarpetBomb(__teamNum);
        
    }

    private void BigNuke(int __teamNum) {
        var urnParticle = CParticleSystem.Create("particles/urn_nuke.vpcf").AtPosition(new Vector3(0, 0, 650)).Spawn();
        Timer.Sequence(__step => {
            if(__step.Run < 4) {
                Sounds.Play("PlayerAlert.LowHealth", RecipientFilter.All);
                return __step.Wait(1.Seconds());
            }

            if (urnParticle != null) {
                urnParticle.Destroy();
            }

            var explosionParticle = CParticleSystem.Create("particles/urn_explosion.vpcf").AtPosition(new Vector3(0, 0, 1000))
                .Spawn();
            if (explosionParticle != null) {
                Timer.Once(6.Seconds(), () => {
                    explosionParticle.Destroy();
                });
            }
            Sounds.Play("Mods.Unstable.Concoction.Explode", RecipientFilter.All);
            foreach (var playerPawn in Players.GetAllPawns()) {
                if(playerPawn.TeamNum == __teamNum) continue;
                var trace = CGameTrace.Create();
                Trace.SimpleTrace(
                    new Vector3(0, 0, 1000),
                    playerPawn.EyePosition,
                    RayType_t.Line,
                    RnQueryObjectSet.All,
                    MaskTrace.Solid, 
                    MaskTrace.Trigger, 
                    MaskTrace.Empty,
                    CollisionGroup.Always,
                    ref trace,
                    playerPawn);
                if (trace.DidHit) {
                    continue;
                }
                var damageInfo = new CTakeDamageInfo(999999, playerPawn, playerPawn);
                damageInfo.DamageFlags = TakeDamageFlags.AllowSuicide | TakeDamageFlags.IgnoreResistances;
                playerPawn.TakeDamage(damageInfo);
            }
            
            return __step.Done();
        });
    }

    private void CarpetBomb(int __teamNum) {
        foreach (var pos in _bombPoses) {
            var beBombParticle = CParticleSystem.Create("particles/abilities/bebop/bebop_sticky_bomb_proj.vpcf").AtPosition(pos).Spawn();
            if(beBombParticle == null) continue;
            Timer.Sequence(__step => {
                if (__step.Run < 6) {
                    Sounds.PlayAt("Bebop.StickyBomb.Detonate", beBombParticle.EntityIndex, RecipientFilter.All, volume:0.5f);
                    return __step.Wait(0.5.Seconds());
                }
                return __step.Done();
            });
            Timer.Once(3.Seconds(), () => {
                beBombParticle.Destroy();
                var beBombExplodeParticle = CParticleSystem.Create("particles/abilities/bebop/bebop_sticky_bomb_explode.vpcf").AtPosition(pos).Spawn();
                if (beBombExplodeParticle != null) {
                    Timer.Once(1.Seconds(), () => {
                        beBombExplodeParticle.Destroy();
                    });
                    Sounds.PlayAt("Bebop.StickyBomb.Explode", beBombExplodeParticle.EntityIndex, RecipientFilter.All);
                }

                foreach (var playerPawn in Players.GetAllPawns()) {
                    if(playerPawn.TeamNum == __teamNum) continue;
                    var trace = CGameTrace.Create();
                    Trace.SimpleTrace(
                        pos,
                        playerPawn.EyePosition,
                        RayType_t.Line,
                        RnQueryObjectSet.All,
                        MaskTrace.Solid, 
                        MaskTrace.Trigger, 
                        MaskTrace.Empty,
                        CollisionGroup.Always,
                        ref trace,
                        playerPawn);
                    if (trace.DidHit || (pos-playerPawn.Position).Length() > 600) {
                        continue;
                    }
                
                    var damageInfo = new CTakeDamageInfo(999999, playerPawn, playerPawn);
                    damageInfo.DamageFlags = TakeDamageFlags.AllowSuicide | TakeDamageFlags.IgnoreResistances;
                    playerPawn.TakeDamage(damageInfo);
                }
            });
        }
    }

    [Command("test", ServerOnly = true, ConsoleOnly = true)]
    public void Test() {
        HandleUrnDepositForTeam(2);
    }
}