# 欢乐小岛（Fun Island）

一个用 Unity 6.3 LTS（6000.3.25f1）程序化生成的**低多边形风格小岛地图**，进入场景后**直接以第一人称游玩**：在岛上自由行走、跳跃、奔跑，探索灯塔、浮空岛和瀑布。

## 地图内容

- 蓝色海洋 + 中央小岛（沙滩 → 草地 → 丘陵）
- 一条穿过小岛中央的河流 + 跨河木质小桥
- 圆顶树、椰子树、岩石、彩色小野花
- 红白配色的海边灯塔
- 两座浮空小岛，带树木和流入大海的瀑布
- 天空中漂浮的白色云朵
- 已配置平行光（太阳）与第一人称玩家（出生在小桥附近的草地上）

## 如何游玩

1. 打开 **Unity Hub** → **Add → Add project from disk** → 选择本文件夹 `FunIsland`
2. 用已安装的 **Unity 6000.3.25f1（Unity 6.3 LTS）** 打开
3. 双击场景 `Assets/Scenes/FunIsland.unity`，点 **Play** —— 立即进入第一人称视角

**操作方式**

| 按键 | 功能 |
|---|---|
| W / A / S / D | 前后左右移动 |
| 鼠标 | 旋转视角（第一人称） |
| 空格 | 跳跃（可以跳上丘陵） |
| Shift | 加速奔跑 |
| ESC | 释放鼠标指针 |

## 重新生成地图

场景和材质都已生成完毕，直接可用。若想重新生成（或误删后恢复）：

菜单 **Tools → Fun Island → Build Map + Capture Preview**

会自动重建场景（包含第一人称玩家）并重新渲染预览图 `Assets/Preview_FunIsland.png`。

> ⚠️ 注意：重建会生成全新场景，**手动摆放的物体（如绿色方块）会被清掉**，需要重新摆放。
> 只想给现有场景补一个玩家（不重建、保留手动摆放的物体）时，用菜单 **Tools → Fun Island → Add First-Person Player**。

## 目录结构

```
FunIsland/
├── Assets/
│   ├── Editor/
│   │   ├── FunIslandMapBuilder.cs   # 地图生成脚本（含第一人称玩家）
│   │   └── FpsSetup.cs              # 向现有场景原地添加玩家的脚本
│   ├── Scripts/FirstPersonController.cs  # 第一人称控制器（WASD/跳跃/加速/视角）
│   ├── Materials/                   # 地图使用的材质
│   ├── Scenes/FunIsland.unity       # 地图场景（已含玩家）
│   ├── Preview_FunIsland.png        # 地图俯瞰预览图
│   └── Preview_FirstPerson.png      # 第一人称视角预览图
├── Packages/manifest.json           # 内置渲染管线工程
└── ProjectSettings/                 # 工程设置（6000.3.25f1）
```
