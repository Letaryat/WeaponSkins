using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Players;

namespace WeaponSkins;

public partial class MenuService
{
    // Declared in the order the groups are listed. A renderer that draws a page of plain
    // submenus as tabs (CustomHUD's skins locker) opens the first one, so Rifles comes first.
    private enum WeaponGroup
    {
        Rifles,
        Pistols,
        SMGs,
        Shotguns,
        Snipers,
        Heavy,
        Other,
    }

    // By item definition index, which is stable across game updates; a weapon not listed
    // here still gets a page, under Other.
    private static WeaponGroup GroupOf(int definitionIndex) => definitionIndex switch
    {
        7 or 8 or 10 or 13 or 16 or 39 or 60 => WeaponGroup.Rifles,
        1 or 2 or 3 or 4 or 30 or 32 or 36 or 61 or 63 or 64 => WeaponGroup.Pistols,
        17 or 19 or 23 or 24 or 26 or 33 or 34 => WeaponGroup.SMGs,
        25 or 27 or 29 or 35 => WeaponGroup.Shotguns,
        9 or 11 or 38 or 40 => WeaponGroup.Snipers,
        14 or 28 => WeaponGroup.Heavy,
        _ => WeaponGroup.Other,
    };

    // The id of every group page. A tab renderer looks for it on the entries of Weapon Skins' page, so
    // a build that lists weapons there instead is not mistaken for a page of tabs.
    private const string GroupMenuId = "weaponskins.weapons.group";

    private static string GroupTitle(PlayerLocalizationService loc, WeaponGroup group) => group switch
    {
        WeaponGroup.Rifles => loc.MenuSkinsGroupRifles,
        WeaponGroup.Pistols => loc.MenuSkinsGroupPistols,
        WeaponGroup.SMGs => loc.MenuSkinsGroupSmgs,
        WeaponGroup.Shotguns => loc.MenuSkinsGroupShotguns,
        WeaponGroup.Snipers => loc.MenuSkinsGroupSnipers,
        WeaponGroup.Heavy => loc.MenuSkinsGroupHeavy,
        _ => loc.MenuSkinsGroupOther,
    };

    // Weapon Skins is a page of submenus, one per weapon group, and each group lists its weapons.
    // Without a tab renderer the groups are an extra level; with one they are the tab strip.
    public IMenu BuildWeaponSkinMenu(IPlayer player)
    {
        var loc = LocalizationService[player];
        var main = CreateMenu("weaponskins.weapons")
            .WithTitle(loc.MenuTitleSkins);

        var groups = EconService.WeaponToPaintkits.Keys
            .Where(weapon => Utilities.IsWeaponDefinitionIndex(EconService.Items[weapon].Index))
            .GroupBy(weapon => GroupOf(EconService.Items[weapon].Index))
            .OrderBy(group => group.Key);

        foreach (var group in groups)
        {
            var weapons = group.ToList();
            // The page's title is its tab's label: that is how a renderer tells which tab is open.
            var title = GroupTitle(loc, group.Key);
            main.Add(new SubmenuComponent(title, _ => BuildWeaponGroupMenu(player, title, weapons), GroupMenuId));
        }

        return main.Build();
    }

    private IMenu BuildWeaponGroupMenu(IPlayer player, string title, IReadOnlyList<string> weapons)
    {
        var main = CreateMenu(GroupMenuId)
            .WithTitle(title);

        foreach (var weapon in weapons)
        {
            var item = EconService.Items[weapon];
            var paintkits = EconService.WeaponToPaintkits[weapon];

            var itemName = EconService.GetLocalizedName(item.LocalizedNames, player.PlayerLanguage.Value);
            var submenuOption = CreateSubmenu(itemName, _ =>
            {
                var skinMenu = CreateMenu("weaponskins.weapons.paintkits").WithTitle(itemName);
                skinMenu.Add(CreateButton(LocalizationService[player].MenuReset,
                    p => Api.ResetWeaponSkin(p.SteamID, p.Controller.Team, (ushort)item.Index, true)));

                foreach (var paintkit in paintkits.OrderByDescending(p => p.Rarity.Id))
                {
                    var option = CreateRarityButton(
                        EconService.GetLocalizedName(paintkit.LocalizedNames, player.PlayerLanguage.Value),
                        paintkit.Rarity.Color.HexColor,
                        p => Api.UpdateWeaponSkin(p.SteamID, p.Controller.Team, (ushort)item.Index, skin => skin.Paintkit = paintkit.Index, true));
                    option.Tag = paintkit.Index;
                    skinMenu.Add(option);
                }

                var built = skinMenu.Build();
                TrackFocus(built, p => TryGetWeaponDataInHand(p, out var inHand) ? inHand.Paintkit : null);
                return built;
            });

            submenuOption.Tag = (ushort)item.Index;
            main.Add(submenuOption);
        }

        var menu = main.Build();
        TrackFocus(menu, p => TryGetWeaponInHand(p, out var weapon) && Utilities.IsWeaponDefinitionIndex(weapon.AttributeManager.Item.ItemDefinitionIndex) ? (ushort)weapon.AttributeManager.Item.ItemDefinitionIndex : null);
        return menu;
    }
}
