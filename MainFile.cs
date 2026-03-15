using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace SnakeMapMod;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    internal const string ModId = "SnakeMapMod";

    public static void Initialize()
    {
        Harmony harmony = new Harmony(ModId);
        harmony.PatchAll();

        GD.Print("------------------------------------------");
        GD.Print("蛇形地图已加载");
        GD.Print("------------------------------------------");
    }
}