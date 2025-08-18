using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfUs.Assets;
using TownOfUs.Options.Modifiers.Alliance;
using TownOfUs.Options.Roles.Impostor;
using TownOfUs.Roles.Impostor;
using TownOfUs.Utilities;
using TownOfUs.Modifiers;
using TownOfUs.Modules;
using UnityEngine;

namespace TownOfUs.Buttons.Impostor;

public sealed class SniperShootButton : TownOfUsRoleButton<SniperRole, PlayerControl>, IKillButton
{
    public override string Name => "Snipe";
    public override string Keybind => Keybinds.SecondaryAction;
    public override Color TextOutlineColor => TownOfUsColors.Impostor;
    public override float Cooldown => OptionGroupSingleton<SniperOptions>.Instance.ShootCooldown + MapCooldown;
    public override int MaxUses => OptionGroupSingleton<SniperOptions>.Instance.MaxShots;
    public override LoadableAsset<Sprite> Sprite => TouImpAssets.SnipeSprite;

    public bool Usable { get; set; } = true;
    private ScreenFlash? _aimFlash;
    private readonly HashSet<byte> _highlighted = new();

    public override bool CanUse()
    {
        // Only usable if cameras/security minigame is open
        var mg = Minigame.Instance;
        var name = mg != null ? (mg.name ?? mg.GetType().Name) : string.Empty;
        var camsOpen = mg != null && (name.Contains("Surv") || name.Contains("Cam") || name.Contains("Security") ||
                                      name.Contains("task_cams") || name.Contains("SurvConsole") ||
                                      name.Contains("Surv_Panel"));

        return base.CanUse() && Usable && camsOpen && UsesLeft != 0;
    }

    protected override void FixedUpdate(PlayerControl playerControl)
    {
        // Light up screen subtly while cameras are open and snipe is available
        var canAim = CanUse();
        if (canAim)
        {
            _aimFlash ??= new ScreenFlash();
            _aimFlash.SetColour(new Color(1f, 1f, 1f, 0.12f));
            _aimFlash.SetActive(true);

            // Highlight any players visible within the currently active surveillance viewport(s)
            var visibleNow = GetPlayersVisibleOnActiveCameras();

            // turn off outlines for those no longer visible
            foreach (var pid in _highlighted.Except(visibleNow.Select(p => p.PlayerId)).ToList())
            {
                var pc = PlayerControl.AllPlayerControls.FirstOrDefault(x => x.PlayerId == pid);
                pc?.cosmetics.SetOutline(false, new Il2CppSystem.Nullable<Color>(Role.TeamColor));
                _highlighted.Remove(pid);
            }

            // enable outline for those now visible
            foreach (var p in visibleNow)
            {
                if (_highlighted.Add(p.PlayerId))
                {
                    p.cosmetics.SetOutline(true, new Il2CppSystem.Nullable<Color>(Role.TeamColor));
                }
            }
        }
        else
        {
            if (_aimFlash != null && _aimFlash.IsActive())
            {
                _aimFlash.SetActive(false);
            }

            // clear any lingering highlights
            foreach (var pid in _highlighted.ToList())
            {
                var pc = PlayerControl.AllPlayerControls.FirstOrDefault(x => x.PlayerId == pid);
                pc?.cosmetics.SetOutline(false, new Il2CppSystem.Nullable<Color>(Role.TeamColor));
                _highlighted.Remove(pid);
            }
        }

        base.FixedUpdate(playerControl);
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

        // subtle flash/light-up to indicate the shot
        Coroutines.Start(MiscUtils.CoFlash(new Color(1f, 1f, 1f, 1f), 0.1f, 0.15f));

        if (Target.HasModifier<FirstDeadShield>())
        {
            return;
        }

        if (Target.HasModifier<BaseShieldModifier>())
        {
            return;
        }

        SniperRole.RpcSniperShot(PlayerControl.LocalPlayer, Target);
    }

    public override PlayerControl? GetTarget()
    {
        // Select any alive player currently visible on the camera screen bounds.
        // Through walls: ignore LOS, just check if within viewport; draw outline feedback
        if (MeetingHud.Instance)
        {
            return null;
        }

        var cams = GetActiveSurveillanceCameras();
        var alive = Helpers.GetAlivePlayers();
        SetOutline(false);

        PlayerControl? best = null;
        float bestScore = float.MaxValue;
        foreach (var p in alive)
        {
            if (p.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            {
                continue;
            }

            bool visibleOnAny = false;
            float score = float.MaxValue;

            if (cams.Count == 0)
            {
                var main = Camera.main;
                if (main != null)
                {
                    var vp = main.WorldToViewportPoint(p.GetTruePosition());
                    visibleOnAny = vp.z > 0 && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                    score = (p.GetTruePosition() - PlayerControl.LocalPlayer.GetTruePosition()).sqrMagnitude;
                }
            }
            else
            {
                foreach (var c in cams)
                {
                    var vp = c.WorldToViewportPoint(p.GetTruePosition());
                    if (vp.z > 0 && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                    {
                        visibleOnAny = true;
                        // prefer closer to that surveillance camera's position
                        var d = (p.GetTruePosition() - (Vector2)c.transform.position).sqrMagnitude;
                        score = Mathf.Min(score, d);
                    }
                }
            }

            if (!visibleOnAny)
            {
                continue;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = p;
            }
        }

        Target = best;
        SetOutline(true);
        return Target;
    }

    private static List<Camera> GetActiveSurveillanceCameras()
    {
        var cams = new List<Camera>();
        if (Minigame.Instance == null)
        {
            return cams;
        }

        foreach (var c in Camera.allCameras)
        {
            if (!c.enabled)
            {
                continue;
            }
            var n = c.name ?? string.Empty;
            if (n.Contains("Surv", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Camera", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("PlanetSurveillance", StringComparison.OrdinalIgnoreCase) ||
                n.Contains("FungleSurveillance", StringComparison.OrdinalIgnoreCase))
            {
                cams.Add(c);
            }
        }

        return cams;
    }

    private static List<PlayerControl> GetPlayersVisibleOnActiveCameras()
    {
        var cams = GetActiveSurveillanceCameras();
        var visible = new List<PlayerControl>();
        foreach (var p in Helpers.GetAlivePlayers())
        {
            if (p.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            {
                continue;
            }
            if (cams.Count == 0)
            {
                var m = Camera.main;
                if (m == null)
                {
                    continue;
                }
                var vp = m.WorldToViewportPoint(p.GetTruePosition());
                if (vp.z > 0 && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                {
                    visible.Add(p);
                }
            }
            else
            {
                foreach (var c in cams)
                {
                    var vp = c.WorldToViewportPoint(p.GetTruePosition());
                    if (vp.z > 0 && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                    {
                        visible.Add(p);
                        break;
                    }
                }
            }
        }
        return visible;
    }
}

