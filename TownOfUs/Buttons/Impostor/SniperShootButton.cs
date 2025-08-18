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
        // Only usable if cameras/security minigame is open
        var mg = Minigame.Instance;
        var name = mg != null ? (mg.name ?? mg.GetType().Name) : string.Empty;
        var camsOpen = mg != null && (name.Contains("Surv") || name.Contains("Cam") || name.Contains("Security") ||
                                      name.Contains("task_cams") || name.Contains("SurvConsole") ||
                                      name.Contains("Surv_Panel"));

        return base.CanUse() && Usable && camsOpen && UsesLeft != 0;
    }

    protected override void OnClick()
    {
        if (Target == null)
        {
            return;
        }

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
        // Through walls: ignore LOS, just check if within viewport
        if (MeetingHud.Instance)
        {
            return null;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            return null;
        }

        var alive = Helpers.GetAlivePlayers();
        var best = alive
            .Where(p => p.PlayerId != PlayerControl.LocalPlayer.PlayerId)
            .Where(p =>
            {
                var vp = cam.WorldToViewportPoint(p.GetTruePosition());
                return vp.z > 0 && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
            })
            .OrderBy(p => (p.GetTruePosition() - PlayerControl.LocalPlayer.GetTruePosition()).sqrMagnitude)
            .FirstOrDefault();

        return best;
    }
}

