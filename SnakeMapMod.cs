using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms; // 用于 RoomSet
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sts2SnakeMapMod;


//禁用后处理

[HarmonyPatch(typeof(MapPostProcessing))]
public static class MapPostProcessingPatch
{
    [HarmonyPatch("CenterGrid")]
    [HarmonyPrefix]
    public static bool PrefixCenter(MapPoint?[,] grid, ref MapPoint?[,] __result) { __result = grid; return false; }

    [HarmonyPatch("SpreadAdjacentMapPoints")]
    [HarmonyPrefix]
    public static bool PrefixSpread(MapPoint?[,] grid, ref MapPoint?[,] __result) { __result = grid; return false; }

    [HarmonyPatch("StraightenPaths")]
    [HarmonyPrefix]
    public static bool PrefixStraighten(MapPoint?[,] grid, ref MapPoint?[,] __result) { __result = grid; return false; }
}

[HarmonyPatch(typeof(MapPathPruning), "PruneDuplicateSegments")]
public static class MapPruningPatch
{
    [HarmonyPrefix]
    public static bool Prefix() { return false; }
}


// 保证每一层都有两个 Boss

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
public static class ForceDoubleBossPatch
{
    [HarmonyPostfix]
    public static void Postfix(ActModel __instance, Rng rng)
    {
        try {
            // 反射获取当前层的房间数据
            var rooms = AccessTools.Field(typeof(ActModel), "_rooms").GetValue(__instance) as RoomSet;
            if (rooms == null) return;

            // 如果游戏本身没有生成第二个 Boss，强行塞一个进去
            if (rooms.SecondBoss == null)
            {
                // 找出所有和一号 Boss 不同的 Boss
                var availableBosses = __instance.AllBossEncounters.Where(e => e.Id != rooms.Boss.Id).ToList();
                if (availableBosses.Count > 0)
                {
                    rooms.SecondBoss = rng.NextItem(availableBosses);
                    GD.Print($"[SnakeMapMod] 强制追加双 BOSS 成功: {rooms.SecondBoss.Id.Entry}");
                }
            }
        } catch { }
    }
}

// 修改 StandardActMap 的生成判断
[HarmonyPatch(typeof(StandardActMap), "CreateFor")]
public static class ForceSecondBossNodePatch
{
    [HarmonyPrefix]
    public static void Prefix(ref bool replaceTreasureWithElites, MegaCrit.Sts2.Core.Runs.RunState runState)
    {
        // 只要房间数据里有 SecondBoss，它就会在地图上画出那个点
    }
}


[HarmonyPatch(typeof(StandardActMap), "AssignPointTypes")]
public static class SnakeMapPatch
{
    [HarmonyPostfix]
    public static void Postfix(StandardActMap __instance)
    {
        try {
            var gridProp = AccessTools.Property(typeof(StandardActMap), "Grid");
            var grid = gridProp.GetValue(__instance) as MapPoint[,];
            if (grid == null) return;

            int cols = grid.GetLength(0);
            int rows = grid.GetLength(1);

            // 安全清理全图连线
            List<MapPoint> allPoints = new List<MapPoint>();
            foreach (var p in grid) { if (p != null) allPoints.Add(p); }
            allPoints.Add(__instance.StartingMapPoint);
            allPoints.Add(__instance.BossMapPoint);
            if (__instance.SecondBossMapPoint != null) allPoints.Add(__instance.SecondBossMapPoint);

            foreach (var p in allPoints) {
                var children = p.Children?.ToList(); 
                if (children != null) {
                    foreach (var c in children) p.RemoveChildPoint(c);
                }
            }

            //  分层收集节点
            List<List<MapPoint>> rowLists = new List<List<MapPoint>>();
            for (int r = 1; r < rows; r++) { 
                List<MapPoint> pts = new List<MapPoint>();
                for (int c = 0; c < cols; c++) {
                    if (grid[c, r] != null) pts.Add(grid[c, r]);
                }
                if (pts.Count > 0) rowLists.Add(pts);
            }

            // 编织蛇形走位
            MapPoint current = __instance.StartingMapPoint;

            foreach (var rowList in rowLists) {
                bool moveRight = (rowList[0].coord.row % 2 != 0); 
                var sorted = moveRight ? rowList.OrderBy(p => p.coord.col).ToList() : rowList.OrderByDescending(p => p.coord.col).ToList();
                
                foreach (var p in sorted) {
                    current.AddChildPoint(p);
                    current = p;
                }
            }

            // 串联双 Boss
            current.AddChildPoint(__instance.BossMapPoint);
            if (__instance.SecondBossMapPoint != null) {
                __instance.BossMapPoint.AddChildPoint(__instance.SecondBossMapPoint);
            }

            GD.Print("[SnakeMapMod] 蛇形地图连线成功！");
        }
        catch (Exception e) {
            GD.PrintErr("[SnakeMapMod ERROR] " + e.Message);
        }
    }
}


// 放宽图标分配限制 (防止死循环)

[HarmonyPatch(typeof(StandardActMap), "IsValidPointType")]
public static class MapAlgorithmOptimizer
{
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result) { __result = true; return false; }
}