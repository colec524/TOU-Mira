using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Gameplay;
using TownOfUs.Buttons.Impostor;
using TownOfUs.Options.Roles.Impostor;

namespace TownOfUs.Events.Impostor;

public static class SniperEvents
{
    [RegisterEvent]
    public static void RoundStartHandler(RoundStartEvent @event)
    {
        // Reset button uses at round boundaries (not just game start)
        var button = CustomButtonSingleton<SniperShootButton>.Instance;
        button.ExtraUses = 0;
        button.SetUses(OptionGroupSingleton<SniperOptions>.Instance.MaxShots);
        button.ResetCooldownAndOrEffect();
    }
}

