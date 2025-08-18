using MiraAPI.GameOptions;
using MiraAPI.Hud;
using MiraAPI.Utilities.Assets;
using TownOfUs.Assets;
using TownOfUs.Options.Modifiers.Alliance;
using TownOfUs.Options.Roles.Impostor;
using TownOfUs.Roles.Impostor;
using TownOfUs.Utilities;
using TownOfUs.Modifiers;
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

    public override bool CanUse()
    {
        return base.CanUse() && Usable && UsesLeft != 0;
    }

    // No aim overlay or glow; use default behavior

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
        // Select any alive player currently visible on the LOCAL PLAYER'S current screen bounds.
        // Through walls: ignore LOS, just check if within main camera viewport. No glow/highlight.
        if (MeetingHud.Instance)
        {
            return null;
        }

        var mainCam = Camera.main;
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

            if (mainCam == null)
            {
                continue;
            }

            var vp = mainCam.WorldToViewportPoint(p.GetTruePosition());
            if (!(vp.z > 0 && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f))
            {
                continue;
            }

            var score = (p.GetTruePosition() - PlayerControl.LocalPlayer.GetTruePosition()).sqrMagnitude;
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

    // no camera helpers; strictly use main view bounds
}

