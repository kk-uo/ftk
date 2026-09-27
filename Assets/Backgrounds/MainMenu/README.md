# 主菜单动态背景

- 当前底图：`main_menu_clean_plate.png`（1672 × 941），由内置 imagegen 编辑生成，移除披风并补全后方城景与人物护甲。
- 独立披风：`main_menu_cape.png`（1671 × 941，RGBA 透明图），由内置 imagegen 提取生成。
- 已移除被分层底图替代的两张旧主菜单背景，仅保留当前使用的底图和披风。
- `MainMenuLivingBackground`：C# 局部网格变形，仅左右触手和破旗摆动；中央灯柱、光环及侧窗持续变亮/变暗。
- `MainMenuCapeLayer`：独立透明披风网格，固定肩部、摆动左侧自由下摆。破洞和摆开的边缘透出补全底图，不再变形披风后方建筑。忽略极低 alpha 噪点后，用布料可见包围框定位；Nearest 采样。
- `MainMenuAtmosphere`：实时远近雨层、涟漪和缓慢漂移的薄雾，置于菜单文字下方。
- `MainMenuCrtFilter`：在背景和天气之后、菜单之前绘制实时 CRT 滤镜，包含细横向扫描线、轻微色偏、暗角和管屏圆角。不会弯曲底图，也不会覆盖菜单文字或拦截点击。无需修改源图片。
- 底图以等比覆盖方式适配窗口，所有局部动画使用原图归一化坐标；更换构图时须重新调整权重区域。
- 披风已经分层，触手和破旗仍为底图局部小幅网格变形；不是骨骼动画。UI 与建筑主体不作整体摆动。

## 生成提示词（内置工具，lighting-weather）

Edit target: the supplied game background. Remove ALL baked-in rainfall, diagonal rain streaks, white rain lines and falling water droplets everywhere, especially the dark left foreground. This is a clean still plate for realtime animated rain to be overlaid in Godot. Preserve the exact wide 16:9 composition, camera, positions and silhouettes of the warrior and his torn red cape at center foreground, all giant curved mechanical tentacles, huge crimson moon upper right, ruined Chinese cyberpunk palace, cables, banners, dark left negative space. Preserve crisp pixel-art detail, red/cyan lighting, wet ground reflections and atmospheric clouds, but NO visible falling rain lines whatsoever. No text, logos, menus, UI or frame. Do not change subject placement, add objects, or redesign the scene.

## 独立披风生成提示词（内置工具）

Use case: background-extraction. Edit target is this exact 1672x941 pixel art game background. Deliver a FULL CANVAS transparent RGBA overlay, same 16:9 aspect ratio and EXACT registration/placement as input. Isolate ONLY the warrior's torn flowing red and dark burgundy cape. Keep cape pixel art, silhouette and folds unchanged, at ORIGINAL SCALE and ORIGINAL POSITION (shoulder anchored around x=830 y=475, cloak extending left to x=590 and down to y=705). Keep the red shoulder cloth which connects to the flying cloak. Remove everything else to genuine transparent alpha: all city, fog, ground, head, hair, neck, metallic armor, hands, legs and sword. Holes between torn cloth ribbons must also be transparent. Do NOT crop to the cape; preserve full original canvas and its empty transparent space. Absolutely no checkerboard painted into pixels, no white/black background, no added rain. This PNG will be overlaid directly on the original scene and animated as an independent cloth layer.

## 披风后方补全底图提示词（内置工具）

Use case: precise-object-edit. Make a clean background plate for layering the warrior's cape separately. In this exact supplied 1672x941 pixel art background, REMOVE ONLY THE RED CAPE/CLOAK from the central warrior: remove shoulder red cloth and all flowing cloth and ragged ribbons extending to the LEFT of the warrior (original cape x590..855, y470..710), revealing continuous stationary ruined city, cyan-gray fog, stonework and the warrior's plain dark armored back under the cloak. Keep the warrior's head, hair, shoulders, torso, armor, legs, hands, sword and position exactly unchanged. Reconstruct plausible armor underneath the cape near the shoulder; reconstruct the city/fog in exposed left-hand empty space. Preserve ALL other parts of the original composition and image unchanged: giant moon, tentacles, city, foreground, flags, lights, colors, pixel style. Do NOT move or enlarge the warrior. NO cape and NO loose red cloth attached to him left over anywhere. No rain. No text, UI or border. Full scene, same framing and size.
